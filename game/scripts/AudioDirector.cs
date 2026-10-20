using Godot;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;

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
            LoopMode = Synth.Loops(id) ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = samples.Length,
        };
    }

    /// <summary>Seconds of audio in a sound's engine stream, for tests.</summary>
    public double Length(SoundId id) => _streams[id].GetLength();

    private float EffectDb(SoundId id) => Mathf.LinearToDb((float)Math.Max(1e-4, Settings.MasterVolume * Settings.EffectsVolume * SoundMap.Gain(id)));

    /// <summary>Plays a sound in the head, or from <paramref name="at"/> in the world.</summary>
    public void Play(SoundId id, Vector3? at = null)
    {
        if (!IsInsideTree() || Settings.MasterVolume * Settings.EffectsVolume <= 0.001) return;
        int voice = _next++ % Voices;
        if (at is { } position)
        {
            var player = _spatial[voice];
            player.Stream = _streams[id];
            player.VolumeDb = EffectDb(id);
            player.GlobalPosition = position;
            player.Play();
        }
        else
        {
            var player = _flat[voice];
            player.Stream = _streams[id];
            player.VolumeDb = EffectDb(id);
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

    private static Vector3 V(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>Whether the player is underground, where the drone plays.</summary>
    public bool Underground { get; set; }

    /// <summary>Fades the drone in underground and applies the volume settings. Runs while paused, so the volume sliders are heard at once.</summary>
    public override void _Process(double delta)
    {
        _ambienceLevel = Mathf.MoveToward((float)_ambienceLevel, Underground ? 1f : 0f, (float)(delta * 0.6));
        double level = Settings.MasterVolume * Settings.AmbienceVolume * _ambienceLevel * SoundMap.Gain(SoundId.Ambience);
        _ambience.VolumeDb = level <= 1e-4 ? -80 : Mathf.LinearToDb((float)level);
    }
}
