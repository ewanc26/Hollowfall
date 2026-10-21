using Hollowfall.Core.Audio;
using Hollowfall.Core.Audio.Music;
using Hollowfall.Core.Combat;

namespace Hollowfall.Tests;

public class MusicTargetTests
{
    private static readonly MusicState Calm = new() { Underground = true, Floor = 0 };

    private static double T(MusicState s, MusicLayer layer, bool combat = false, bool heavy = false) => MusicMixer.Targets(s, combat, heavy)[(int)layer];

    private static IEnumerable<MusicState> Sweep()
    {
        foreach (bool underground in new[] { false, true })
            foreach (int floor in new[] { 0, 1, 2, 5 })
                foreach (int enemies in new[] { 0, 1, 6, 30 })
                    foreach (int rank in new[] { 0, 4, 8 })
                        foreach (double health in new[] { 1.0, 0.3, 0.0 })
                            foreach (bool flag in new[] { false, true })
                                yield return new MusicState
                                {
                                    Underground = underground,
                                    Floor = floor,
                                    EnemiesNear = enemies,
                                    StyleRank = rank,
                                    HealthFraction = health,
                                    EncounterActive = flag,
                                    BossActive = flag && enemies > 5,
                                    NearWulver = flag,
                                    NearHearth = flag && rank == 4,
                                    PassiveEntityNearby = flag && rank == 8,
                                    PortalLinked = flag && floor == 1,
                                    DialogueOpen = flag,
                                    Paused = flag && rank == 0,
                                };
    }

    [Fact]
    public void CalmSurfaceIsTheWhistleOverADroneAndHarp()
    {
        var s = new MusicState();
        Assert.True(T(s, MusicLayer.Drone) > 0 && T(s, MusicLayer.Harp) > 0 && T(s, MusicLayer.Whistle) > 0);
        foreach (var l in new[] { MusicLayer.DrumLight, MusicLayer.DrumHeavy, MusicLayer.Choir, MusicLayer.Heartbeat, MusicLayer.Hearth, MusicLayer.Pulse, MusicLayer.Shimmer })
            Assert.Equal(0, T(s, l));
    }

    [Fact]
    public void TheWulversHillHasTheLouderWhistle()
    {
        Assert.True(T(new MusicState { NearWulver = true }, MusicLayer.Whistle) > T(new MusicState(), MusicLayer.Whistle));
        Assert.Equal(MusicContext.WulverHill, MusicContexts.Of(new MusicState { NearWulver = true }));
        Assert.Equal(MusicContext.Plain, MusicContexts.Of(new MusicState { Underground = true, NearWulver = true }));
    }

    [Fact]
    public void DeeperFloorsAreDarkerAndLoseTheWhistle()
    {
        Assert.Equal(MusicContext.Ashen, MusicContexts.Of(new MusicState { Underground = true, Floor = 3 }));
        Assert.Equal(0, T(Calm with { Floor = 2 }, MusicLayer.Whistle));
        Assert.True(T(Calm with { Floor = 2 }, MusicLayer.Pulse) > T(Calm, MusicLayer.Pulse));
        Assert.True(T(Calm with { Floor = 1 }, MusicLayer.Harp) < T(Calm, MusicLayer.Harp));
    }

    [Fact]
    public void CombatBringsTheDrumInAndTakesTheHarpAndWhistleOut()
    {
        var fight = Calm with { EnemiesNear = 3, StyleRank = 3 };
        Assert.True(T(fight, MusicLayer.DrumLight, combat: true) > 0.3);
        Assert.Equal(0, T(fight, MusicLayer.DrumLight, combat: false));
        Assert.True(T(fight, MusicLayer.Harp, combat: true) < T(Calm, MusicLayer.Harp) / 2);
        Assert.Equal(0, T(fight, MusicLayer.Whistle, combat: true));
    }

    [Fact]
    public void TheHeavyDrumNeedsTheHeavyFlag()
    {
        var fight = Calm with { EnemiesNear = 6, StyleRank = 6 };
        Assert.Equal(0, T(fight, MusicLayer.DrumHeavy, combat: true, heavy: false));
        Assert.True(T(fight, MusicLayer.DrumHeavy, combat: true, heavy: true) > 0.4);
    }

    [Fact]
    public void MoreStyleAndMoreEnemiesMakeItLouder()
    {
        var quiet = Calm with { EnemiesNear = 1, StyleRank = 0 };
        var wild = Calm with { EnemiesNear = 6, StyleRank = 8 };
        Assert.True(MusicMixer.IntensityOf(wild) > MusicMixer.IntensityOf(quiet));
        Assert.True(T(wild, MusicLayer.DrumLight, true) > T(quiet, MusicLayer.DrumLight, true));
        Assert.True(T(wild, MusicLayer.Pulse, true) > T(quiet, MusicLayer.Pulse, true));
        Assert.True(T(wild, MusicLayer.Shimmer, true) > 0 && T(quiet, MusicLayer.Shimmer, true) == 0);
    }

    [Fact]
    public void TheBossIsTheChoirAndBothDrumsAndNoHarp()
    {
        var boss = Calm with { Floor = 2, BossActive = true, EnemiesNear = 1 };
        Assert.True(T(boss, MusicLayer.Choir, true) > 0.5);
        Assert.True(T(boss, MusicLayer.DrumHeavy, true) > 0.5 && T(boss, MusicLayer.DrumLight, true) > 0.5);
        Assert.Equal(0, T(boss, MusicLayer.Harp, true));
        Assert.Equal(1, MusicMixer.IntensityOf(boss));
        Assert.Equal(0, T(Calm, MusicLayer.Choir));
    }

    [Fact]
    public void ADeadPlayerGetsSilence()
    {
        var targets = MusicMixer.Targets(Calm with { Dead = true, BossActive = true, EnemiesNear = 4 }, true, true);
        Assert.All(targets, v => Assert.Equal(0, v));
    }

    [Fact]
    public void LowHealthBringsTheHeartbeatAndMoreOfItAsHealthFalls()
    {
        Assert.Equal(0, T(Calm with { HealthFraction = 0.5 }, MusicLayer.Heartbeat));
        double a = T(Calm with { HealthFraction = 0.3 }, MusicLayer.Heartbeat);
        double b = T(Calm with { HealthFraction = 0.1 }, MusicLayer.Heartbeat);
        Assert.True(a > 0 && b > a);
        Assert.Equal(0, T(Calm with { HealthFraction = double.NaN }, MusicLayer.Heartbeat));
    }

    [Fact]
    public void ByAHearthTheMusicTurnsToTheHumAndASoftHarp()
    {
        var fire = new MusicState { NearHearth = true };
        Assert.True(T(fire, MusicLayer.Hearth) > 0.5);
        Assert.True(T(fire, MusicLayer.Harp) < T(new MusicState(), MusicLayer.Harp));
        Assert.True(T(fire, MusicLayer.Whistle) < T(new MusicState(), MusicLayer.Whistle));
        Assert.Equal(0, T(new MusicState(), MusicLayer.Hearth));
        Assert.Equal(0, T(fire with { EnemiesNear = 2 }, MusicLayer.Hearth, combat: true)); // a fight drowns the fireside
    }

    [Fact]
    public void PassiveCreaturesAndLinkedPortalsBringTheShimmer()
    {
        Assert.True(T(new MusicState { PassiveEntityNearby = true }, MusicLayer.Shimmer) > T(new MusicState { PortalLinked = true }, MusicLayer.Shimmer));
        Assert.True(T(new MusicState { PortalLinked = true }, MusicLayer.Shimmer) > 0);
        Assert.Equal(0, T(new MusicState(), MusicLayer.Shimmer));
    }

    [Fact]
    public void EveryTargetIsBetweenZeroAndTheHeadroomAndTheSumCannotClip()
    {
        int n = 0;
        foreach (var s in Sweep())
            foreach (bool combat in new[] { false, true })
                foreach (bool heavy in new[] { false, true })
                {
                    var t = MusicMixer.Targets(s, combat, heavy);
                    double worst = 0;
                    for (int i = 0; i < t.Length; i++)
                    {
                        Assert.InRange(t[i], 0, MusicTuning.MasterHeadroom + 1e-9);
                        worst += t[i] * MusicTuning.Peak((MusicLayer)i) * ((MusicLayer)i == MusicLayer.Drone ? MusicTuning.DroneBoost : 1);
                    }
                    Assert.True(worst <= 1 + 1e-9, $"the worst-case sum is {worst}");
                    n++;
                }
        Assert.True(n > 2000);
    }

    [Fact]
    public void EvenEveryLayerAtOnceIsKeptUnderOne()
    {
        // The rule scales an over-full mix down. Prove it with the loudest thing the table can ask for.
        var s = new MusicState { Underground = true, Floor = 2, BossActive = true, EnemiesNear = 6, StyleRank = 8, HealthFraction = 0.05, PassiveEntityNearby = true, PortalLinked = true, NearHearth = true };
        var t = MusicMixer.Targets(s, true, true);
        double sum = 0;
        for (int i = 0; i < t.Length; i++) sum += t[i] * MusicTuning.Peak((MusicLayer)i) * ((MusicLayer)i == MusicLayer.Drone ? MusicTuning.DroneBoost : 1);
        Assert.True(sum <= 1 + 1e-9);
    }
}

public class MusicMixerTests
{
    private const double Dt = 1.0 / 60;
    private static readonly MusicState Calm = new() { Underground = true, Floor = 0 };
    private static readonly MusicState Fight = new() { Underground = true, Floor = 0, EnemiesNear = 4, StyleRank = 5, EncounterActive = true };

    private static void Run(MusicMixer m, MusicState s, double seconds)
    {
        for (double t = 0; t < seconds; t += Dt) m.Update(s, Dt);
    }

    /// <summary>A scripted set of states that change often and abruptly.</summary>
    private static MusicState StateAt(double t) => ((int)(t / 1.7) % 6) switch
    {
        0 => Calm,
        1 => Fight,
        2 => Fight with { BossActive = true },
        3 => Calm with { HealthFraction = 0.1, DialogueOpen = true },
        4 => new MusicState { NearWulver = true, Paused = true },
        _ => Calm with { Dead = true },
    };

    [Fact]
    public void NoLayerEverMovesFasterThanItsAttackOrReleaseAllows()
    {
        var m = new MusicMixer();
        var previous = Enum.GetValues<MusicLayer>().ToDictionary(l => l, _ => 0.0);
        for (double t = 0; t < 120; t += Dt)
        {
            m.Update(StateAt(t), Dt);
            foreach (var l in Enum.GetValues<MusicLayer>())
            {
                double now = m.Smoothed(l), delta = Math.Abs(now - previous[l]);
                double fastest = 1 / Math.Min(MusicTuning.AttackSeconds(l), MusicTuning.DeathFadeSeconds);
                Assert.True(delta <= fastest * Dt + 1e-9, $"{l} moved {delta / Dt:F3} per second, more than {fastest:F3}");
                previous[l] = now;
            }
        }
    }

    [Fact]
    public void AGiantFrameCannotMakeTheMusicJump()
    {
        var m = new MusicMixer();
        m.Update(Fight, 30); // a hitch of half a minute counts as no more than a tenth of a second
        Assert.True(m.Smoothed(MusicLayer.DrumLight) <= MusicTuning.MaxStepSeconds / MusicTuning.AttackSeconds(MusicLayer.DrumLight) + 1e-9);
        m.Update(Fight, double.NaN);
        m.Update(Fight, -5);
    }

    [Fact]
    public void CombatComesInFastAndGoesOutSlowly()
    {
        var m = new MusicMixer();
        Run(m, Calm, 20);
        double start = 0;
        double toRise = 0;
        while (m.Smoothed(MusicLayer.DrumLight) < 0.9 * MusicMixer.Targets(Fight, true, false)[(int)MusicLayer.DrumLight] && toRise < 30) { m.Update(Fight, Dt); toRise += Dt; }
        Run(m, Fight, 10);
        double peak = m.Smoothed(MusicLayer.DrumLight);
        double toFall = 0;
        while (m.Smoothed(MusicLayer.DrumLight) > 0.1 * peak && toFall < 60) { m.Update(Calm, Dt); toFall += Dt; }
        Assert.True(toRise < 1.0, $"the drum took {toRise:F2} s to come in");
        Assert.True(toFall > toRise * 4, $"the drum took {toFall:F2} s to fade against {toRise:F2} s to come in");
        Assert.True(MusicTuning.AttackSeconds(MusicLayer.DrumLight) < MusicTuning.ReleaseSeconds(MusicLayer.DrumLight));
        _ = start;
    }

    [Fact]
    public void EveryLayerAttacksFasterThanItReleasesAndDronesNeverDropAbruptly()
    {
        foreach (var l in Enum.GetValues<MusicLayer>())
            Assert.True(MusicTuning.AttackSeconds(l) <= MusicTuning.ReleaseSeconds(l), $"{l}");
        Assert.True(MusicTuning.ReleaseSeconds(MusicLayer.Drone) >= 5);
        var m = new MusicMixer();
        Run(m, Calm, 30);
        double before = m.Smoothed(MusicLayer.Drone);
        Run(m, new MusicState { Underground = true, Floor = 0, Paused = false }, 0.5);
        m.Update(new MusicState(), Dt); // leaving the dungeon
        Assert.True(before - m.Smoothed(MusicLayer.Drone) < 0.01);
    }

    [Fact]
    public void CombatDoesNotFlickerWhenTheEnemyCountDoes()
    {
        var m = new MusicMixer();
        Run(m, Calm, 5);
        int flips = 0;
        bool last = m.Combat;
        for (double t = 0; t < 20; t += Dt)
        {
            var s = ((int)(t / 0.4) % 2 == 0) ? Fight : Calm; // enemies come and go every 0.4 s
            m.Update(s, Dt);
            if (m.Combat != last) { flips++; last = m.Combat; }
        }
        Assert.Equal(1, flips);
        Assert.True(m.Combat);
    }

    [Fact]
    public void CombatEndsOnlyAfterTheHold()
    {
        var m = new MusicMixer();
        Run(m, Fight, 2);
        Assert.True(m.Combat);
        Run(m, Calm, MusicTuning.CombatHoldSeconds - 0.3);
        Assert.True(m.Combat);
        Run(m, Calm, 0.6);
        Assert.False(m.Combat);
        // and it starts at once
        m.Update(Fight, Dt);
        Assert.True(m.Combat);
    }

    [Fact]
    public void TheHeavyDrumComesInHighAndLeavesLow()
    {
        var m = new MusicMixer();
        var mid = Calm with { EnemiesNear = 3, StyleRank = 3 };            // between the two thresholds
        double i = MusicMixer.IntensityOf(mid);
        Assert.InRange(i, MusicTuning.HeavyOff + 0.01, MusicTuning.HeavyOn - 0.01);
        Run(m, mid, 5);
        Assert.False(m.Heavy); // in the middle from below: stays out
        Run(m, Fight with { EnemiesNear = 6, StyleRank = 6 }, 1);
        Assert.True(m.Heavy);
        Run(m, mid, 5);
        Assert.True(m.Heavy); // in the middle from above: stays in
        Run(m, Calm with { EnemiesNear = 1 }, 1);
        Assert.False(m.Heavy);
    }

    [Fact]
    public void TheBossBringsTheChoirInAndTheHarpOut()
    {
        var m = new MusicMixer();
        Run(m, Calm, 20);
        var boss = Calm with { Floor = 2, BossActive = true, EnemiesNear = 1 };
        Run(m, boss, 12);
        Assert.True(m.Heavy);
        Assert.True(m.Smoothed(MusicLayer.Choir) > 0.6);
        Assert.True(m.Smoothed(MusicLayer.DrumHeavy) > 0.6);
        Assert.True(m.Smoothed(MusicLayer.Harp) < 0.05);
    }

    [Fact]
    public void DeathFadesEverythingOutInAboutThreeSeconds()
    {
        var m = new MusicMixer();
        Run(m, Calm with { Floor = 2, BossActive = true, EnemiesNear = 3 }, 40);
        Assert.True(Enum.GetValues<MusicLayer>().Sum(l => m.LayerGain(l)) > 1, $"{Enum.GetValues<MusicLayer>().Sum(l => m.LayerGain(l))}");
        var dead = Calm with { Dead = true, BossActive = true };
        Run(m, dead, 1.5);
        Assert.True(Enum.GetValues<MusicLayer>().Sum(l => m.LayerGain(l)) > 0.1, $"the music should still be fading at 1.5 s: {Enum.GetValues<MusicLayer>().Sum(l => m.LayerGain(l))}");
        Run(m, dead, MusicTuning.DeathFadeSeconds - 1.5 + 0.1);
        Assert.All(Enum.GetValues<MusicLayer>(), l => Assert.True(m.LayerGain(l) < 1e-6, $"{l} still at {m.LayerGain(l)}"));
        Assert.False(m.Combat);
        // Respawning brings the music back in (from the slow attack)
        Run(m, Calm, 10);
        Assert.True(m.Smoothed(MusicLayer.Drone) > 0.4);
    }

    [Fact]
    public void DialogueDucksToFortyPercentAndReleases()
    {
        var m = new MusicMixer();
        Run(m, Calm, 20);
        double normal = m.LayerGain(MusicLayer.Drone);
        Run(m, Calm with { DialogueOpen = true }, 2);
        Assert.Equal(MusicTuning.DialogueLevel, m.LayerGain(MusicLayer.Drone) / normal, 3);
        Run(m, Calm, 3);
        Assert.Equal(1, m.LayerGain(MusicLayer.Drone) / normal, 3);
    }

    [Fact]
    public void PauseDucksTheMixAndTakesTheHighsFirst()
    {
        var m = new MusicMixer();
        var calm = new MusicState { Underground = true, Floor = 0, PassiveEntityNearby = true };
        Run(m, calm, 25);
        double drone = m.LayerGain(MusicLayer.Drone), shimmer = m.LayerGain(MusicLayer.Shimmer), harp = m.LayerGain(MusicLayer.Harp);
        Run(m, calm with { Paused = true }, 2);
        double droneKept = m.LayerGain(MusicLayer.Drone) / drone, shimmerKept = m.LayerGain(MusicLayer.Shimmer) / shimmer, harpKept = m.LayerGain(MusicLayer.Harp) / harp;
        Assert.True(droneKept < 1 && shimmerKept < harpKept && harpKept < droneKept, $"kept: drone {droneKept:F2}, harp {harpKept:F2}, shimmer {shimmerKept:F2}");
        Assert.True(m.PauseAmount > 0.99);
        Run(m, calm, 3);
        Assert.Equal(1, m.LayerGain(MusicLayer.Drone) / drone, 3);
    }

    [Fact]
    public void TheMusicStartsFromSilenceAndRampsUp()
    {
        var m = new MusicMixer();
        m.Update(Calm, Dt);
        Assert.True(m.LayerGain(MusicLayer.Drone) < 0.01);
        Run(m, Calm, MusicTuning.StartFadeSeconds + 4);
        Assert.Equal(1, m.StartLevel, 6);
        Assert.True(m.LayerGain(MusicLayer.Drone) > 0.4);
    }

    [Fact]
    public void ContextsCrossfadeAndTheirWeightsNeverExceedOne()
    {
        var m = new MusicMixer();
        Run(m, new MusicState(), 1);
        Assert.Equal(1, m.ContextWeight(MusicContext.Surface), 6);
        for (double t = 0; t < 30; t += Dt)
        {
            m.Update(t < 2 ? Calm : t < 4 ? Calm with { Floor = 1 } : Calm with { Floor = 2 }, Dt); // changes its mind twice
            Assert.True(Enum.GetValues<MusicContext>().Sum(m.ContextWeight) <= 1 + 1e-9);
        }
        Assert.Equal(1, m.ContextWeight(MusicContext.Ashen), 6);
        Assert.Equal(0, m.ContextWeight(MusicContext.Surface), 6);

        var halfway = new MusicMixer();
        Run(halfway, new MusicState(), 1);
        Run(halfway, Calm, MusicTuning.ContextCrossfadeSeconds / 2);
        Assert.InRange(halfway.ContextWeight(MusicContext.Surface), 0.4, 0.6);
        Assert.InRange(halfway.ContextWeight(MusicContext.Plain), 0.4, 0.6);
    }

    [Fact]
    public void EveryFinalGainIsBetweenZeroAndOneAndTheSumOfAContextsLayersCannotClip()
    {
        var m = new MusicMixer();
        for (double t = 0; t < 120; t += Dt)
        {
            m.Update(StateAt(t) with { NearHearth = (int)t % 7 == 0, PassiveEntityNearby = (int)t % 5 == 0 }, Dt);
            double peakSum = 0;
            foreach (var c in Enum.GetValues<MusicContext>())
                foreach (var l in Enum.GetValues<MusicLayer>())
                {
                    double g = m.Gain(c, l);
                    Assert.InRange(g, 0, 1);
                    peakSum += g * MusicTuning.Peak(l) * (l == MusicLayer.Drone ? MusicTuning.DroneBoost : 1);
                }
            Assert.True(peakSum <= 1 + 1e-9, $"at {t:F2} s the worst-case sum is {peakSum:F3}");
        }
    }

    [Fact]
    public void TheMixerIsDeterministic()
    {
        var a = new MusicMixer();
        var b = new MusicMixer();
        for (double t = 0; t < 60; t += Dt)
        {
            a.Update(StateAt(t), Dt);
            b.Update(StateAt(t), Dt);
            foreach (var c in Enum.GetValues<MusicContext>())
                foreach (var l in Enum.GetValues<MusicLayer>())
                    Assert.Equal(a.Gain(c, l), b.Gain(c, l));
        }
    }
}

public class MusicStingerTests
{
    public static TheoryData<Stinger> All
    {
        get
        {
            var d = new TheoryData<Stinger>();
            foreach (var s in Enum.GetValues<Stinger>()) d.Add(s);
            return d;
        }
    }

    [Theory]
    [MemberData(nameof(All))]
    public void EachStingerIsAudibleBoundedFiniteAndEndsInSilence(Stinger s)
    {
        var data = MusicStingers.Render(s, MusicFixture.Seed);
        Assert.InRange(data.Length / (double)MusicTuning.SampleRate, 2.5, 12);
        Assert.All(data, v => Assert.True(float.IsFinite(v)));
        Assert.Equal(MusicStingers.Peak, data.Max(Math.Abs), 3);
        double rms = Math.Sqrt(data.Sum(v => (double)v * v) / data.Length);
        Assert.True(rms > 0.02, $"{s} rms {rms}");
        Assert.True(Math.Abs(data[^1]) < 1e-4, $"{s} ends at {data[^1]}");
        Assert.True(Math.Abs(data[0]) < 0.15, $"{s} starts at {data[0]}");
    }

    [Fact]
    public void StingersAreDeterministicDifferentFromEachOtherAndFollowTheSeed()
    {
        foreach (var s in Enum.GetValues<Stinger>())
            Assert.True(MusicStingers.Render(s, 5).AsSpan().SequenceEqual(MusicStingers.Render(s, 5)), $"{s} differs between runs");
        var all = Enum.GetValues<Stinger>().Select(s => MusicStingers.Render(s, 5)).ToList();
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                Assert.False(all[i].Length == all[j].Length && all[i].AsSpan().SequenceEqual(all[j]));
        long other = Enumerable.Range(1, 40).First(k => MusicScores.TonicPitchClass(k) != MusicScores.TonicPitchClass(5));
        Assert.False(MusicStingers.Render(Stinger.FloorComplete, 5).AsSpan().SequenceEqual(MusicStingers.Render(Stinger.FloorComplete, other)));
    }
}

public class MusicEventTests
{
    [Fact]
    public void CombatEventsMapToStingers()
    {
        Assert.Equal(Stinger.Death, MusicEvents.For(new PlayerDied()));
        Assert.Equal(Stinger.FloorComplete, MusicEvents.For(new FloorCompleted(0, default!)));
        Assert.Equal(Stinger.FloorComplete, MusicEvents.For(new FloorCompleted(1, default!), bossFloor: 2));
        Assert.Null(MusicEvents.For(new FloorCompleted(2, default!), bossFloor: 2)); // the ending has its own
        Assert.Equal(Stinger.BossIntro, MusicEvents.For(new EncounterStarted(2, 7, 3), bossFloor: 2, bossRoom: 7));
        Assert.Null(MusicEvents.For(new EncounterStarted(2, 6, 3), bossFloor: 2, bossRoom: 7));
        Assert.Null(MusicEvents.For(new EncounterStarted(1, 7, 3), bossFloor: 2, bossRoom: 7));
        Assert.Null(MusicEvents.For(new EncounterStarted(0, 0, 3))); // boss unknown
        Assert.Null(MusicEvents.For(new KeyCollected(1)));
        Assert.Null(MusicEvents.For(new PlayerHurt(5, 90)));
    }

    [Fact]
    public void TheWulversGreetingSoundBringsHisMotif()
    {
        Assert.Equal(Stinger.WulverGreeting, MusicEvents.For(SoundId.WulverGreeting));
        Assert.Null(MusicEvents.For(SoundId.Hit));
    }

    [Fact]
    public void EveryStingerHasASaneGain()
    {
        foreach (var s in Enum.GetValues<Stinger>()) Assert.InRange(MusicEvents.Gain(s), 0.3, 1);
    }
}

public class MusicStateBuilderTests
{
    [Fact]
    public void TheSurfaceIsNotUnderground()
    {
        var s = MusicStateBuilder.Build(-1, [], 0, 1, false, 100, false, false);
        Assert.False(s.Underground);
        Assert.False(s.NearWulver);
        Assert.Equal(MusicContext.Surface, MusicContexts.Of(s));
    }

    [Fact]
    public void NearTheWulverOnlyOnTheSurface()
    {
        Assert.True(MusicStateBuilder.Build(-1, [], 0, 1, false, MusicTuning.WulverHearRange - 1, false, false).NearWulver);
        Assert.False(MusicStateBuilder.Build(-1, [], 0, 1, false, MusicTuning.WulverHearRange + 1, false, false).NearWulver);
        Assert.False(MusicStateBuilder.Build(0, [], 0, 1, false, 5, false, false).NearWulver);
    }

    [Fact]
    public void EnemiesCountOnlyWhenAliveAndClose()
    {
        EnemyProbe[] enemies = [new(true, false, false, 5), new(true, false, false, 100), new(false, false, true, 3), new(true, false, true, 20)];
        var s = MusicStateBuilder.Build(1, enemies, 3, 0.5, false, 0, false, false);
        Assert.Equal(2, s.EnemiesNear);
        Assert.True(s.EncounterActive);
        Assert.False(s.BossActive);
        Assert.Equal(1, s.Floor);
        Assert.Equal(3, s.StyleRank);
    }

    [Fact]
    public void TheBossCountsWhenAliveAndNotFarAway()
    {
        Assert.True(MusicStateBuilder.Build(2, [new(true, true, true, 40)], 0, 1, false, 0, false, false).BossActive);
        Assert.False(MusicStateBuilder.Build(2, [new(true, true, true, 90)], 0, 1, false, 0, false, false).BossActive);
        Assert.False(MusicStateBuilder.Build(2, [new(false, true, true, 5)], 0, 1, false, 0, false, false).BossActive);
    }

    [Fact]
    public void ValuesAreClampedAndFlagsPassedThrough()
    {
        var s = MusicStateBuilder.Build(0, [], 99, 4.5, true, 0, true, true, nearHearth: true, passiveNearby: true, portalLinked: true);
        Assert.Equal(8, s.StyleRank);
        Assert.Equal(1, s.HealthFraction);
        Assert.True(s.Dead && s.DialogueOpen && s.Paused && s.NearHearth && s.PassiveEntityNearby && s.PortalLinked);
        Assert.Equal(1, MusicStateBuilder.Build(0, [], 0, double.NaN, false, 0, false, false).HealthFraction);
        Assert.Equal(0, MusicStateBuilder.Build(0, [], -3, -1, false, 0, false, false).StyleRank);
    }
}

public class MusicOfflineTests
{
    private static float[] Rendered => Cache.Value;

    private static readonly Lazy<float[]> Cache = new(() =>
    {
        var (steps, seconds) = MusicOffline.ListeningScript();
        return MusicOffline.Render(MusicFixture.Bank, steps, seconds);
    });

    private static double Rms(float[] x, double fromSeconds, double toSeconds)
    {
        int a = (int)(fromSeconds * MusicTuning.SampleRate), b = (int)(toSeconds * MusicTuning.SampleRate);
        return Math.Sqrt(Enumerable.Range(a, b - a).Sum(i => (double)x[i] * x[i]) / (b - a));
    }

    [Fact]
    public void TheListeningScriptNeverClipsAndIsNeverSilentUntilTheEnd()
    {
        var x = Rendered;
        Assert.All(x, v => Assert.True(float.IsFinite(v)));
        Assert.True(x.Max(Math.Abs) < 0.95, $"peak {x.Max(Math.Abs)}");
        Assert.True(Rms(x, 6, 12) > 0.005, "the calm part is nearly silent");
        Assert.True(Rms(x, 45, 55) > Rms(x, 15, 25), "the fight is not louder than the calm");
        Assert.True(Rms(x, 75, 88) > Rms(x, 15, 25), "the boss is not heavier than the calm");
    }

    [Fact]
    public void TheMusicStartsQuietlyAndTheDeathFadesAway()
    {
        var x = Rendered;
        Assert.True(Rms(x, 0, 0.3) < Rms(x, 8, 11));
        Assert.True(Rms(x, 97, 100) < Rms(x, 75, 88) * 0.1, "the music did not fade after the death");
    }

    [Fact]
    public void WavFilesAreWellFormed()
    {
        var wav = MusicOffline.ToWav([0f, 0.5f, -0.5f, 1.5f]);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(44 + 8, wav.Length);
        Assert.Equal(36 + 8, BitConverter.ToInt32(wav, 4));
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(wav, 44 + 6)); // clamped
        Assert.Equal(MusicTuning.SampleRate, BitConverter.ToInt32(wav, 24));
    }

    [Fact]
    public void WritesTheListeningWavWhenAskedTo()
    {
        // Set HOLLOWFALL_MUSIC_WAV to a path to keep the rendered sequence for a person to listen to. Otherwise this only checks the size.
        var bytes = MusicOffline.ToWav(Rendered);
        Assert.Equal(44 + Rendered.Length * 2, bytes.Length);
        if (Environment.GetEnvironmentVariable("HOLLOWFALL_MUSIC_WAV") is { Length: > 0 } path) File.WriteAllBytes(path, bytes);
    }
}
