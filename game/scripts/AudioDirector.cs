using Godot;
using Hollowfall.Core.Assets;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;
using Hollowfall.Core.Physics;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// Plays the game's sounds. They are synthesised in Core (<see cref="Synth"/>), turned into engine streams once, and
/// played from a small pool of players: sounds with a place in the world come from there, the rest are heard in the head.
/// Volumes follow the settings, and the ambience fades in when the player is underground.
/// </summary>
public partial class AudioDirector : Node
{
    private const int Voices = 20;

    private readonly Dictionary<SoundId, AudioStreamWav> _streams = [];
    private readonly List<AudioStreamPlayer> _flat = [];
    private readonly List<AudioStreamPlayer3D> _spatial = [];
    private AudioStreamPlayer _ambience = null!;
    private double _ambienceLevel;
    private int _next;
    private readonly Stride _stride = new();

    public GameSettings Settings { get; set; } = new();

    /// <summary>The listener's position, used to place sounds that have no position of their own (and for the sim lookups).</summary>
    public Func<Vector3> Listener { get; set; } = () => Vector3.Zero;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // menu sounds and the drone play while the game is paused
        foreach (var id in Enum.GetValues<SoundId>()) _streams[id] = ToStream(id);

        for (int i = 0; i < Voices; i++)
        {
            var flat = new AudioStreamPlayer();
            var spatial = new AudioStreamPlayer3D { MaxDistance = 60, UnitSize = 6, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance };
            AddChild(flat);
            AddChild(spatial);
            _flat.Add(flat);
            _spatial.Add(spatial);
        }

        _ambience = new AudioStreamPlayer { Stream = _streams[SoundId.Ambience], VolumeDb = -80 };
        AddChild(_ambience);
        _ambience.Play();
    }

    private static AudioStreamWav ToStream(SoundId id)
    {
        var loop = Synth.Loops(id) ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
        if (GD.Load<AudioStreamWav>("res://" + StaticAssets.SoundFile(id)) is { } stored)
        {
            stored.LoopMode = loop;
            stored.LoopBegin = 0;
            stored.LoopEnd = stored.Data.Length / 2;
            return stored;
        }

        var samples = Synth.Render(id);
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short v = (short)Math.Clamp((int)Math.Round(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Synth.SampleRate,
            Stereo = false,
            Data = bytes,
            LoopMode = loop,
            LoopBegin = 0,
            LoopEnd = samples.Length,
        };
    }

    /// <summary>Seconds of audio in a sound's engine stream, for tests.</summary>
    public double Length(SoundId id) => _streams[id].GetLength();

    private float EffectDb(SoundId id) => Mathf.LinearToDb((float)Math.Max(1e-4, Settings.MasterVolume * Settings.EffectsVolume * SoundMap.Gain(id)));

    /// <summary>Plays a sound in the head, or from <paramref name="at"/> in the world, optionally quieter (<paramref name="volume"/>, 0 to 1) or at another pitch.</summary>
    public void Play(SoundId id, Vector3? at = null, float volume = 1f, float pitch = 1f)
    {
        if (!IsInstanceValid(this) || !IsInsideTree() || Settings.MasterVolume * Settings.EffectsVolume <= 0.001) return;
        int voice = _next++ % Voices;
        float db = EffectDb(id) + Mathf.LinearToDb(Math.Max(volume, 1e-4f));
        if (at is { } position)
        {
            var player = _spatial[voice];
            player.Stream = _streams[id];
            player.VolumeDb = db;
            player.PitchScale = pitch;
            player.GlobalPosition = position;
            player.Play();
        }
        else
        {
            var player = _flat[voice];
            player.Stream = _streams[id];
            player.VolumeDb = db;
            player.PitchScale = pitch;
            player.Play();
        }
    }

    /// <summary>Plays whatever combat event just happened.</summary>
    public void OnEvent(CombatEvent e, CombatSim sim)
    {
        if (SoundMap.For(e) is not { } id) return;
        Vector3? at = e switch
        {
            EnemyHit h => V(h.Point),
            ProjectileFired p => V(p.Position),
            ProjectileGone g => V(g.Position),
            EnemyTelegraph t => sim.Enemies.FirstOrDefault(x => x.Id == t.Id) is { } en ? V(en.Feet) + Vector3.Up : null,
            _ => null,
        };
        Play(id, at);
    }

    /// <summary>
    /// Footsteps: call once per physics step while the game runs (not while paused, which is why this is not in <see cref="_Process"/>).
    /// The stride timer and the choice of sound are Core's; this finds the block underfoot and plays the result.
    /// </summary>
    public void Walk(double delta, PlayerBody body, bool notWalking, Func<int, int, int, BlockType> blockAt)
    {
        var kind = _stride.Update(delta, body.VelocityX, body.VelocityZ, body.VelocityY, body.OnGround, !notWalking);
        if (kind == StepKind.None) return;

        int x = (int)Math.Floor(body.X), z = (int)Math.Floor(body.Z);
        var below = blockAt(x, (int)Math.Floor(body.Y - 0.05), z);
        var atFeet = blockAt(x, (int)Math.Floor(body.Y + 0.1), z);
        var id = Footsteps.SoundFor(Footsteps.Underfoot(below, atFeet));
        LastStep = id;
        Play(id, null, (float)_stride.Volume, (float)_stride.Pitch);
    }

    /// <summary>The sound the last footstep asked for, for tests and the showcase.</summary>
    public SoundId? LastStep { get; private set; }

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>
    /// Set by the <see cref="MusicDirector"/> once the adaptive music is playing: its drone layer is the bed from then on (the Ambience
    /// slider scales that layer), so the old looping drone goes quiet instead of doubling it.
    /// </summary>
    public bool BedReplaced { get; set; }

    /// <summary>Whether the player is underground, where the drone plays.</summary>
    public bool Underground { get; set; }

    private readonly Dictionary<SoundId, (AudioStreamPlayer Player, double Level)> _loops = [];

    /// <summary>
    /// Sets how loud a looping sound is, 0 to 1 (the fire's crackle, the cat's purr). The host calls this each frame with a level from the distance to the source;
    /// the loop starts when the level rises above silence and stops when it falls to it.
    /// </summary>
    public void SetLoopLevel(SoundId id, double level)
    {
        if (!IsInsideTree() || !Synth.Loops(id) || id == SoundId.Ambience) return;
        if (!_loops.TryGetValue(id, out var loop))
        {
            var player = new AudioStreamPlayer { Stream = _streams[id], VolumeDb = -80 };
            AddChild(player);
            loop = (player, 0);
        }
        _loops[id] = (loop.Player, Math.Clamp(level, 0, 1));
    }

    /// <summary>The level a looping sound was last set to (for tests).</summary>
    public double LoopLevel(SoundId id) => _loops.TryGetValue(id, out var l) ? l.Level : 0;

    /// <summary>Whether a looping sound is playing now (for tests).</summary>
    public bool LoopPlaying(SoundId id) => _loops.TryGetValue(id, out var l) && l.Player.Playing;

    private void UpdateLoops()
    {
        foreach (var (id, (player, level)) in _loops)
        {
            double volume = Settings.MasterVolume * Settings.EffectsVolume * SoundMap.Gain(id) * level;
            if (volume <= 1e-4)
            {
                if (player.Playing) player.Stop();
                continue;
            }
            if (!player.Playing) player.Play();
            player.VolumeDb = Mathf.LinearToDb((float)volume);
        }
    }

    /// <summary>Fades the drone in underground and applies the volume settings. Runs while paused, so the volume sliders are heard at once.</summary>
    public override void _Process(double delta)
    {
        _ambienceLevel = Mathf.MoveToward((float)_ambienceLevel, Underground && !BedReplaced ? 1f : 0f, (float)(delta * 0.6));
        double level = Settings.MasterVolume * Settings.AmbienceVolume * _ambienceLevel * SoundMap.Gain(SoundId.Ambience);
        _ambience.VolumeDb = level <= 1e-4 ? -80 : Mathf.LinearToDb((float)level);
        UpdateLoops();
    }
}
