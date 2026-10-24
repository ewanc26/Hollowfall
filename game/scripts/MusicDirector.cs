using System.Diagnostics;
using Godot;
using Hollowfall.Core.Audio.Music;
using Hollowfall.Core.Input;

namespace Hollowfall.Game;

/// <summary>
/// Plays the adaptive music. Everything about the music is Core's (<see cref="MusicBank"/> renders it, <see cref="MusicMixer"/>
/// decides how loud each layer is); this node renders the loops on a background task at startup, hands them to the engine as one
/// <see cref="AudioStreamSynchronized"/> per context (so every layer stays in step), sets the layer volumes each frame and plays the
/// stingers on a player of their own. It runs while the game is paused, like <see cref="AudioDirector"/>.
/// </summary>
public partial class MusicDirector : Node
{
    private static readonly MusicContext[] Contexts = Enum.GetValues<MusicContext>();
    private static readonly MusicLayer[] Layers = Enum.GetValues<MusicLayer>();

    private sealed record Rendered(byte[][][] Loops, byte[][] Stingers, double Milliseconds);

    private MusicMixer _mixer = new();
    private readonly float[,] _db = new float[Contexts.Length, Layers.Length];
    private readonly double[,] _gain = new double[Contexts.Length, Layers.Length];
    private readonly AudioStreamSynchronized[] _sync = new AudioStreamSynchronized[Contexts.Length];
    private readonly Dictionary<Stinger, AudioStreamWav> _stingers = [];
    private AudioStreamPlayer _stingerPlayer = null!;
    private Task<Rendered>? _render;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public GameSettings Settings { get; set; } = new();

    /// <summary>The world seed the music is written from.</summary>
    public long Seed { get; set; }

    /// <summary>The audio director whose old drone this replaces once the music is ready.</summary>

    /// <summary>What the music should follow: the host sets it every frame.</summary>
    public MusicState State { get; set; } = new();

    /// <summary>Whether the host feeds <see cref="State"/> from the game each frame. Tests turn it off to drive the music by hand.</summary>
    public bool FollowGame { get; set; } = true;

    /// <summary>The loops are rendered, the streams built and the players running.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Why the music is not coming, if the background render failed.</summary>
    public string? Failure { get; private set; }

    /// <summary>Milliseconds the background render took (all contexts, in parallel).</summary>
    public double RenderMilliseconds { get; private set; }

    /// <summary>Milliseconds the main thread spent turning the rendered loops into engine streams.</summary>
    public double BuildMilliseconds { get; private set; }

    /// <summary>Bytes of 16-bit audio the engine holds for the loops and the stingers.</summary>
    public long BufferBytes { get; private set; }

    public MusicMixer Mixer => _mixer;

    /// <summary>Starts the mix again from silence (tests use it to begin from a known state).</summary>
    public void ResetMix() => _mixer = new MusicMixer();

    /// <summary>The linear gain last set on a layer of a context's stream (before the music and master volume).</summary>
    public double GainOf(MusicContext c, MusicLayer l) => _gain[(int)c, (int)l];

    /// <summary>The decibels last set on a layer of a context's stream.</summary>
    public float DbOf(MusicContext c, MusicLayer l) => _db[(int)c, (int)l];

    /// <summary>How many streams a context's synchronised stream holds.</summary>
    public int StreamCount(MusicContext c) => _sync[(int)c]?.StreamCount ?? 0;

    /// <summary>The master times the music volume: what the whole music is scaled by.</summary>
    public double Level => Settings.MasterVolume * Settings.MusicVolume;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _stingerPlayer = new AudioStreamPlayer { Name = "Stingers" };
        AddChild(_stingerPlayer);
        long seed = Seed;
        _render = Task.Run(() => RenderAll(seed));
    }

    private static Rendered RenderAll(long seed)
    {
        var watch = Stopwatch.StartNew();
        var loops = new byte[Contexts.Length][][];
        // Each context is rendered and turned into 16-bit bytes on its own thread, so the float loops are dropped as soon as they are encoded.
        Parallel.For(0, Contexts.Length, i =>
        {
            var floats = MusicLayers.RenderAll(MusicScores.For(Contexts[i], seed));
            loops[i] = floats.Select(MusicOffline.ToPcm16).ToArray();
        });
        var stingers = Enum.GetValues<Stinger>().Select(s => MusicOffline.ToPcm16(MusicStingers.Render(s, seed))).ToArray();
        return new Rendered(loops, stingers, watch.Elapsed.TotalMilliseconds);
    }

    private static AudioStreamWav ToStream(byte[] pcm, bool loop) => new()
    {
        Format = AudioStreamWav.FormatEnum.Format16Bits,
        MixRate = MusicTuning.SampleRate,
        Stereo = false,
        Data = pcm,
        LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
        LoopBegin = 0,
        LoopEnd = pcm.Length / 2,
    };

    private void Build(Rendered r)
    {
        var watch = Stopwatch.StartNew();
        long bytes = 0;
        for (int c = 0; c < Contexts.Length; c++)
        {
            var sync = new AudioStreamSynchronized { StreamCount = Layers.Length };
            for (int l = 0; l < Layers.Length; l++)
            {
                sync.SetSyncStream(l, ToStream(r.Loops[c][l], true));
                sync.SetSyncStreamVolume(l, -80);
                _db[c, l] = -80;
                bytes += r.Loops[c][l].Length;
            }
            _sync[c] = sync;
            // Start every context's player in the same frame: they hold the same loop length, so they stay in step.
            var player = new AudioStreamPlayer { Name = $"Music{Contexts[c]}", Stream = sync };
            AddChild(player);
        }
        foreach (var s in Enum.GetValues<Stinger>())
        {
            _stingers[s] = ToStream(r.Stingers[(int)s], false);
            bytes += r.Stingers[(int)s].Length;
        }
        foreach (var child in GetChildren())
            if (child is AudioStreamPlayer { Stream: not null } p && p != _stingerPlayer) p.Play();

        RenderMilliseconds = r.Milliseconds;
        BufferBytes = bytes;
        BuildMilliseconds = watch.Elapsed.TotalMilliseconds;
        IsReady = true;
        GD.Print($"Hollowfall: music ready: rendered in {RenderMilliseconds:F0} ms on a background task, built in {BuildMilliseconds:F0} ms, {bytes / 1048576.0:F1} MB of audio, {_clock.ElapsedMilliseconds} ms after startup");
    }

    /// <summary>Plays a stinger over the music, at the music volume.</summary>
    public void PlayStinger(Stinger s)
    {
        if (!IsReady || Level <= 0.001 || !_stingers.TryGetValue(s, out var stream)) return;
        _stingerPlayer.Stream = stream;
        _stingerPlayer.VolumeDb = Mathf.LinearToDb((float)Math.Max(1e-4, Level * Hollowfall.Core.Audio.Music.MusicEvents.Gain(s)));
        _stingerPlayer.Play();
    }

    /// <summary>The last stinger asked for, for tests.</summary>
    public Stinger? LastStinger { get; private set; }

    public void Trigger(Stinger s)
    {
        LastStinger = s;
        PlayStinger(s);
    }

    public override void _Process(double delta)
    {
        if (!IsReady)
        {
            if (_render is { IsCompleted: true })
            {
                if (_render.IsFaulted) { Failure = _render.Exception?.GetBaseException().Message ?? "failed"; GD.PrintErr($"Hollowfall: the music could not be rendered: {Failure}"); _render = null; }
                else { Build(_render.Result); _render = null; }
            }
            return;
        }

        _mixer.Update(State, delta);
        double level = Level;
        double drone = Settings.AmbienceVolume * MusicTuning.DroneBoost; // the Ambience slider scales the drone layer: 1 at its default
        for (int c = 0; c < Contexts.Length; c++)
            for (int l = 0; l < Layers.Length; l++)
            {
                double g = _mixer.Gain(Contexts[c], Layers[l]);
                _gain[c, l] = g;
                double linear = g * level * (Layers[l] == MusicLayer.Drone ? drone : 1);
                float db = linear <= 1e-4 ? -80f : Mathf.LinearToDb((float)linear);
                if (Math.Abs(db - _db[c, l]) > 0.01f)
                {
                    _db[c, l] = db;
                    _sync[c].SetSyncStreamVolume(l, db);
                }
            }
    }
}
