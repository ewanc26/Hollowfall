using VoxelGame.Core.Combat;

namespace VoxelGame.Tests;

public class StyleMeterTests
{
    private static void Wait(StyleMeter m, double seconds)
    {
        for (double t = 0; t < seconds; t += 1.0 / 60) m.Tick(1.0 / 60);
    }

    [Fact]
    public void StartsEmptyAndKillsEarnPoints()
    {
        var m = new StyleMeter();
        Assert.Equal(StyleRank.None, m.Rank);
        Assert.Equal(45, m.RegisterKill("revolver"), 3); // 30, x1.5 because the revolver is fresh
        Assert.Equal(45, m.Points, 3);
        Assert.Equal(StyleRank.None, m.Rank);
    }

    [Fact]
    public void PointsFillRanksInOrder()
    {
        var m = new StyleMeter();
        var seen = new List<StyleRank>();
        for (int i = 0; i < 200 && m.Rank < StyleRank.Legendary; i++)
        {
            m.NotifyMoving(true);
            m.Award(StyleBonus.AirSlam);
            if (seen.Count == 0 || seen[^1] != m.Rank) seen.Add(m.Rank);
        }
        Assert.Equal(Enum.GetValues<StyleRank>().Where(r => r != StyleRank.None).Prepend(StyleRank.None).Skip(0).Where(r => seen.Contains(r) || r == StyleRank.None), seen.Prepend(StyleRank.None).Distinct());
        Assert.Equal(StyleRank.Legendary, m.Rank);
        Assert.Equal(1.0, m.RankProgress);
    }

    [Fact]
    public void PointsAreCappedAtTheTop()
    {
        var m = new StyleMeter();
        for (int i = 0; i < 500; i++) m.Award(StyleBonus.Chargeback);
        Assert.Equal(StyleMeter.MaxPoints, m.Points);
        Assert.True(m.TotalScore > StyleMeter.MaxPoints, "total score keeps counting past the cap");
    }

    [Fact]
    public void StandingStillMakesStyleDrainFasterAtHigherRanks()
    {
        double LossOverOneSecond(double start)
        {
            var m = new StyleMeter();
            while (m.Points < start) m.Award(StyleBonus.Chargeback);
            double before = m.Points;
            Wait(m, 1.0);
            return before - m.Points;
        }
        double low = LossOverOneSecond(200), high = LossOverOneSecond(3000);
        Assert.True(high > low * 2, $"{low} vs {high}");
    }

    [Fact]
    public void StyleDecaysToZeroButNeverBelow()
    {
        var m = new StyleMeter();
        m.Award(StyleBonus.Parry);
        Wait(m, 60);
        Assert.Equal(0, m.Points);
        Assert.Equal(StyleRank.None, m.Rank);
    }

    [Fact]
    public void BeingHitDropsOneRankToTheStartOfTheLowerOne()
    {
        var m = new StyleMeter();
        while (m.Rank < StyleRank.Savage) m.Award(StyleBonus.Chargeback);
        m.OnDamaged();
        Assert.Equal(StyleRank.Brutal, m.Rank);
        m.OnDamaged();
        Assert.Equal(StyleRank.Fierce, m.Rank);
        var fresh = new StyleMeter();
        fresh.OnDamaged();
        Assert.Equal(0, fresh.Points);
    }

    [Fact]
    public void MovementMultipliesPointsWhileSlidingOrAirborneThenFadesQuickly()
    {
        var m = new StyleMeter();
        Assert.Equal(1.0, m.MovementMultiplier);
        m.NotifyMoving(true);
        Assert.Equal(3.0, m.MovementMultiplier);
        Assert.Equal(StyleMeter.BaseValue(StyleBonus.Headshot) * 3, m.Award(StyleBonus.Headshot), 3);
        Wait(m, 0.25);
        Assert.Equal(1.0, m.MovementMultiplier);
    }

    [Fact]
    public void KillsInQuickSuccessionChain()
    {
        var m = new StyleMeter();
        m.RegisterKill("a");
        Wait(m, 0.15);
        double second = m.RegisterKill("a");
        Assert.Equal(45 + 25, second, 3); // kill + double kill
        Wait(m, 0.15);
        double third = m.RegisterKill("a");
        Assert.Equal(45 + 50, third, 3);
        Wait(m, 0.15);
        Assert.Equal(45 + 100, m.RegisterKill("a"), 3);

        Wait(m, 2.0);
        Assert.Equal(45, m.RegisterKill("a"), 3); // the chain expired
    }

    [Fact]
    public void KillingWithADifferentWeaponEarnsVarietyAndStaleWeaponsEarnLess()
    {
        var m = new StyleMeter();
        m.RegisterKill("revolver");
        Wait(m, 1.0);
        Assert.Equal(45 + 50, m.RegisterKill("shotgun"), 3); // kill (fresh x1.5) + Arsenal

        var spam = new StyleMeter();
        Assert.Equal(Freshness.Fresh, spam.FreshnessOf("revolver"));
        for (int i = 0; i < 8; i++) spam.NotifyWeaponUsed("revolver");
        Assert.Equal(Freshness.Used, spam.FreshnessOf("revolver"));
        for (int i = 0; i < 6; i++) spam.NotifyWeaponUsed("revolver");
        Assert.Equal(Freshness.Stale, spam.FreshnessOf("revolver"));
        for (int i = 0; i < 40; i++) spam.NotifyWeaponUsed("revolver");
        Assert.Equal(Freshness.Dull, spam.FreshnessOf("revolver"));
        Assert.Equal(0, spam.Award(StyleBonus.Headshot, "revolver"));

        spam.NotifyWeaponUsed("shotgun");
        for (int i = 0; i < 20; i++) spam.NotifyWeaponUsed("shotgun");
        Assert.NotEqual(Freshness.Dull, spam.FreshnessOf("revolver")); // switching weapons restores freshness
    }

    [Fact]
    public void FreshWeaponsEarnABonus()
    {
        var m = new StyleMeter();
        Assert.Equal(StyleMeter.BaseValue(StyleBonus.Headshot) * 1.5, m.Award(StyleBonus.Headshot, "revolver"), 3);
    }

    [Fact]
    public void HigherRanksSpeedUpHardDamageRecovery()
    {
        var m = new StyleMeter();
        Assert.Equal(1.0, m.HardDamageSpeed);
        while (m.Rank < StyleRank.Legendary) m.Award(StyleBonus.Chargeback);
        Assert.True(m.HardDamageSpeed > 2.5);
    }

    [Fact]
    public void RecentAwardsAreKeptForTheHud()
    {
        var m = new StyleMeter();
        for (int i = 0; i < 20; i++) m.Award(StyleBonus.Parry);
        Assert.Equal(8, m.RecentAwards.Count);
        Assert.All(m.RecentAwards, a => Assert.Equal(StyleBonus.Parry, a.Bonus));
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var m = new StyleMeter();
        m.RegisterKill("a");
        m.NotifyMoving(true);
        m.Reset();
        Assert.Equal((0.0, 0.0, 1.0), (m.Points, m.TotalScore, m.MovementMultiplier));
        Assert.Empty(m.RecentAwards);
    }

    [Fact]
    public void ItIsDeterministic()
    {
        StyleMeter Run()
        {
            var m = new StyleMeter();
            for (int i = 0; i < 600; i++)
            {
                if (i % 40 == 0) m.RegisterKill(i % 80 == 0 ? "a" : "b");
                if (i % 97 == 0) m.OnDamaged();
                m.NotifyMoving(i % 5 == 0);
                m.Tick(1.0 / 60);
            }
            return m;
        }
        var a = Run();
        var b = Run();
        Assert.Equal((a.Points, a.TotalScore, a.Rank), (b.Points, b.TotalScore, b.Rank));
    }
}

public class PlayerVitalsTests
{
    [Fact]
    public void StartsFullAndDamageLowersHealth()
    {
        var v = new PlayerVitals();
        Assert.Equal(100, v.Health);
        Assert.Equal(30, v.Damage(30));
        Assert.Equal(70, v.Health);
        Assert.False(v.Dead);
        v.Damage(500);
        Assert.True(v.Dead);
        Assert.Equal(0, v.Health);
        Assert.Equal(0, v.Damage(10));
    }

    [Fact]
    public void DamageCreatesHardDamageThatCapsHealing()
    {
        var v = new PlayerVitals();
        v.Damage(40);
        Assert.Equal(40 * PlayerVitals.HardDamageShare, v.HardDamage, 3);
        Assert.Equal(100 - 14, v.Ceiling, 3);
        v.Heal(1000);
        Assert.Equal(86, v.Health, 3);
    }

    [Fact]
    public void HardDamageWaitsThenDrains()
    {
        var v = new PlayerVitals();
        v.Damage(40); // hard damage 14, delay 1s + 2s
        for (int i = 0; i < 120; i++) v.Tick(1.0 / 60);
        Assert.Equal(14, v.HardDamage, 3); // still inside the delay
        for (int i = 0; i < 180; i++) v.Tick(1.0 / 60);
        Assert.Equal(0, v.HardDamage, 3);
    }

    [Fact]
    public void StyleSpeedsUpHardDamageDrain()
    {
        PlayerVitals Hurt() { var v = new PlayerVitals(); v.Damage(60); return v; }
        var slow = Hurt();
        var fast = Hurt();
        for (int i = 0; i < 300; i++) { slow.Tick(1.0 / 60, 1.0); fast.Tick(1.0 / 60, 3.0); }
        Assert.True(fast.HardDamage < slow.HardDamage);
    }

    [Fact]
    public void BloodOnlyHealsAtCloseRange()
    {
        var v = new PlayerVitals();
        v.Damage(60);
        v.Tick(10); // clear hard damage so only the health matters
        Assert.Equal(0, v.HealFromHit(HitZone.Head, PlayerVitals.BloodRange + 0.1));
        Assert.Equal(10, v.HealFromHit(HitZone.Torso, 2));
        Assert.Equal(20, v.HealFromHit(HitZone.Limb, 2));
        Assert.Equal(30, v.HealFromHit(HitZone.Head, 2));
        Assert.Equal(100, v.Health, 3);
        Assert.Equal(0, v.HealFromHit(HitZone.Head, 2)); // already full
    }

    [Fact]
    public void ShotgunPelletsHealALittleEach()
    {
        var v = new PlayerVitals();
        v.Damage(50);
        v.Tick(10);
        double total = Enumerable.Range(0, 8).Sum(_ => v.HealFromHit(HitZone.Torso, 1.5, perPellet: 3));
        Assert.Equal(24, total, 3);
    }

    [Fact]
    public void GibbingUpCloseRestoresALot()
    {
        var v = new PlayerVitals();
        v.Damage(80);
        v.Tick(10);
        Assert.Equal(80, v.HealFromGib(1.0), 3); // capped by missing health
        Assert.Equal(0, new PlayerVitals().HealFromGib(10));
    }

    [Fact]
    public void ParryRefillsToTheCeilingAndGrantsInvulnerability()
    {
        var v = new PlayerVitals();
        v.Damage(70);
        double healed = v.Parry();
        Assert.Equal(v.Ceiling, v.Health, 3);
        Assert.True(healed > 0);
        Assert.True(v.Invulnerable);
        Assert.Equal(0, v.Damage(50));
        v.Tick(PlayerVitals.ParryInvulnerability + 0.01);
        Assert.False(v.Invulnerable);
        Assert.True(v.Damage(10) > 0);
    }

    [Fact]
    public void DeadPlayersDoNotHeal()
    {
        var v = new PlayerVitals();
        v.Damage(1000);
        Assert.Equal(0, v.Heal(50));
        Assert.Equal(0, v.Parry());
        Assert.True(v.Dead);
    }

    [Fact]
    public void HardDamageNeverReachesTheMaximum()
    {
        var v = new PlayerVitals();
        for (int i = 0; i < 30; i++) { v.Damage(5); v.Heal(100); }
        Assert.True(v.Ceiling >= 1);
    }

    [Fact]
    public void ResetRestoresEverything()
    {
        var v = new PlayerVitals();
        v.Damage(60);
        v.Reset();
        Assert.Equal((100.0, 0.0, false), (v.Health, v.HardDamage, v.Invulnerable));
    }
}

public class FloorRankingTests
{
    [Fact]
    public void ParTimeGrowsWithEnemiesAndDistance()
    {
        var small = FloorTargets.For(5, 40);
        Assert.True(FloorTargets.For(20, 40).ParTimeSeconds > small.ParTimeSeconds);
        Assert.True(FloorTargets.For(5, 200).ParTimeSeconds > small.ParTimeSeconds);
        Assert.Equal(40 + 1.2 * 5 + 0.25 * 40, small.ParTimeSeconds, 6);
    }

    [Fact]
    public void TimeGradesStepDownAsYouSlowDown()
    {
        var t = FloorTargets.For(10, 100);
        double par = t.ParTimeSeconds;
        Assert.Equal(Grade.S, t.GradeTime(par));
        Assert.Equal(Grade.A, t.GradeTime(par * 1.2));
        Assert.Equal(Grade.B, t.GradeTime(par * 1.9));
        Assert.Equal(Grade.C, t.GradeTime(par * 2.9));
        Assert.Equal(Grade.D, t.GradeTime(par * 4));
    }

    [Fact]
    public void KillGradesFollowTheShareOfEnemiesKilled()
    {
        var t = FloorTargets.For(20, 50);
        Assert.Equal(Grade.S, t.GradeKills(20));
        Assert.Equal(Grade.A, t.GradeKills(18));
        Assert.Equal(Grade.B, t.GradeKills(15));
        Assert.Equal(Grade.C, t.GradeKills(10));
        Assert.Equal(Grade.D, t.GradeKills(3));
        Assert.Equal(Grade.S, FloorTargets.For(0, 50).GradeKills(0));
    }

    [Fact]
    public void StyleGradesScaleWithEnemyCount()
    {
        var t = FloorTargets.For(10, 50);
        Assert.Equal(Grade.S, t.GradeStyle(1500));
        Assert.Equal(Grade.A, t.GradeStyle(1200));
        Assert.Equal(Grade.B, t.GradeStyle(800));
        Assert.Equal(Grade.C, t.GradeStyle(400));
        Assert.Equal(Grade.D, t.GradeStyle(100));
        Assert.Equal(Grade.S, FloorTargets.For(20, 50).GradeStyle(3000));
    }

    [Theory]
    [InlineData(Grade.S, Grade.S, Grade.S, 0, OverallGrade.P)]
    [InlineData(Grade.S, Grade.S, Grade.S, 1, OverallGrade.S)]
    [InlineData(Grade.S, Grade.S, Grade.A, 0, OverallGrade.S)]
    [InlineData(Grade.A, Grade.A, Grade.A, 0, OverallGrade.A)]
    [InlineData(Grade.B, Grade.S, Grade.A, 2, OverallGrade.B)]
    [InlineData(Grade.C, Grade.C, Grade.C, 0, OverallGrade.C)]
    [InlineData(Grade.D, Grade.D, Grade.D, 0, OverallGrade.D)]
    [InlineData(Grade.S, Grade.S, Grade.S, 6, OverallGrade.B)]
    public void OverallGradeSumsPointsAndSubtractsRestarts(Grade time, Grade kills, Grade style, int restarts, OverallGrade expected)
    {
        var t = FloorTargets.For(10, 100);
        double seconds = time switch { Grade.S => t.ParTimeSeconds, Grade.A => t.ParTimeSeconds * 1.2, Grade.B => t.ParTimeSeconds * 1.9, Grade.C => t.ParTimeSeconds * 2.9, _ => t.ParTimeSeconds * 5 };
        int killCount = kills switch { Grade.S => 10, Grade.A => 9, Grade.B => 8, Grade.C => 5, _ => 1 };
        double styleScore = style switch { Grade.S => 1500, Grade.A => 1200, Grade.B => 800, Grade.C => 400, _ => 50 };

        var r = FloorRanking.Score(t, seconds, killCount, styleScore, restarts);
        Assert.Equal((time, kills, style), (r.Time, r.Kills, r.Style));
        Assert.Equal(expected, r.Overall);
        Assert.Equal((int)time + (int)kills + (int)style - restarts, r.Points);
    }
}
