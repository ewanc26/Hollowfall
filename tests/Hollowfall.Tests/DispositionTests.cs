using Hollowfall.Core.Combat;
using Hollowfall.Core.Relations;

namespace Hollowfall.Tests;

public class SpeciesTests
{
    [Fact]
    public void TheTableIsIndexedByKindAndEveryKindHasOne()
    {
        var kinds = Enum.GetValues<EnemyKind>();
        Assert.Equal(kinds.Length, SpeciesInfo.All.Count);
        foreach (var kind in kinds) Assert.Equal(kind, SpeciesInfo.Of(kind).Kind);
    }

    [Fact]
    public void TheHollowIsAlwaysHostileAndEveryoneElseIsNot()
    {
        foreach (var kind in new[] { EnemyKind.Husk, EnemyKind.Spitter, EnemyKind.Warden })
        {
            Assert.True(SpeciesInfo.Of(kind).AlwaysHostile);
            Assert.Equal(Disposition.Aggressive, SpeciesInfo.Of(kind).Natural);
        }
        foreach (var kind in new[] { EnemyKind.Brownie, EnemyKind.Selkie, EnemyKind.Kelpie, EnemyKind.BeanNighe, EnemyKind.Hare })
            Assert.False(SpeciesInfo.Of(kind).AlwaysHostile);
    }

    [Fact]
    public void SpeciesMatchTheirRolesAndFactions()
    {
        Assert.Equal(Disposition.Passive, SpeciesInfo.Of(EnemyKind.Brownie).Natural);
        Assert.Equal(Disposition.Skittish, SpeciesInfo.Of(EnemyKind.Selkie).Natural);
        Assert.Equal(Disposition.Territorial, SpeciesInfo.Of(EnemyKind.Kelpie).Natural);
        Assert.Equal(Disposition.Passive, SpeciesInfo.Of(EnemyKind.BeanNighe).Natural);
        Assert.Equal(Faction.Vermin, SpeciesInfo.Of(EnemyKind.Hare).Faction);
        Assert.True(SpeciesInfo.Of(EnemyKind.Selkie).CanAlly);
        Assert.All(new[] { EnemyKind.Brownie, EnemyKind.BeanNighe, EnemyKind.Hare }, k => Assert.True(SpeciesInfo.Of(k).Pacifist));
        Assert.False(SpeciesInfo.Of(EnemyKind.Kelpie).Pacifist);
    }

    [Fact]
    public void NewKindsHaveHitBoxesAndOnlyTheFightersHaveAnAttack()
    {
        foreach (var kind in new[] { EnemyKind.Brownie, EnemyKind.Selkie, EnemyKind.Kelpie, EnemyKind.BeanNighe, EnemyKind.Hare })
        {
            var spec = EnemySpec.Of(kind);
            Assert.Equal(kind, spec.Kind);
            Assert.True(spec.Health > 0 && spec.Height > 0.4 && spec.HalfWidth > 0.1);
            bool fights = !SpeciesInfo.Of(kind).Pacifist;
            Assert.Equal(fights, spec.MeleeDamage > 0);
        }
    }
}

public class TraitTests
{
    private static readonly SpeciesInfo Selkie = SpeciesInfo.Of(EnemyKind.Selkie);

    [Fact]
    public void TraitsAreAPureFunctionOfSeedKindAndPlace()
    {
        Assert.Equal(Traits.For(7, Selkie, 10, 71, 20), Traits.For(7, Selkie, 10, 71, 20));
        Assert.NotEqual(Traits.For(7, Selkie, 10, 71, 20), Traits.For(8, Selkie, 10, 71, 20));
        Assert.NotEqual(Traits.For(7, Selkie, 10, 71, 20), Traits.For(7, Selkie, 11, 71, 20));
        Assert.NotEqual(Traits.For(7, Selkie, 10, 71, 20), Traits.For(7, SpeciesInfo.Of(EnemyKind.Hare), 10, 71, 20));
    }

    [Fact]
    public void TraitsStayInsideTheirSpeciesRanges()
    {
        foreach (var species in SpeciesInfo.All)
            for (int i = 0; i < 200; i++)
            {
                var t = Traits.For(i, species, i * 3, 70, i * 5);
                Assert.InRange(t.Bravery, species.Bravery.Lo, species.Bravery.Hi);
                Assert.InRange(t.Grudge, species.Grudge.Lo, species.Grudge.Hi);
                Assert.InRange(t.Curiosity, species.Curiosity.Lo, species.Curiosity.Hi);
            }
    }

    [Fact]
    public void TwoOfAKindDifferAcrossTheSeed()
    {
        var values = Enumerable.Range(0, 50).Select(i => Traits.For(3, Selkie, i, 70, 0).Bravery).Distinct().Count();
        Assert.True(values > 40, "individuals should not share a personality");
        Assert.True(Enumerable.Range(0, 100).Select(i => Traits.For(1, Selkie, i, 70, 0).Bravery).Max() > 0.6);
    }
}

public class MemoryTests
{
    private static readonly Traits Timid = new(0, 0, 0.5), Bold = new(1, 1, 0.5), Spiteful = new(0.5, 1, 0.5), Forgiving = new(0.5, 0, 0.5);

    [Fact]
    public void BeingHurtMakesAnAnimalAngryAndAfraid()
    {
        var m = new Memory();
        Assert.True(m.Blank);
        m.Hurt(0.5, Timid);
        Assert.True(m.Anger >= Mind.WarnAnger);
        Assert.True(m.Fear > 0);
        Assert.False(m.Blank);
    }

    [Fact]
    public void TheBraveFearLessAndEverythingStaysBetweenZeroAndOne()
    {
        var a = new Memory();
        var b = new Memory();
        a.Hurt(0.3, Timid);
        b.Hurt(0.3, Bold);
        Assert.True(a.Fear > b.Fear);
        for (int i = 0; i < 50; i++) { a.Hurt(1, Timid); a.Witnessed(Stance.Ally, 1, Timid); a.Alarmed(1, true, Timid); a.Gift(); }
        Assert.Equal(1, a.Anger);
        Assert.Equal(1, a.Fear);
        Assert.Equal(1, a.Gratitude);
        a.Set(double.NaN, -4, 9);
        Assert.Equal((0, 0, 1), (a.Anger, a.Fear, a.Gratitude));
    }

    [Fact]
    public void WitnessesReactByHowTheyRegardTheVictim()
    {
        double Anger(Stance s) { var m = new Memory(); m.Witnessed(s, 1, Timid); return m.Anger; }
        double Fear(Stance s) { var m = new Memory(); m.Witnessed(s, 1, Timid); return m.Fear; }
        Assert.True(Anger(Stance.Ally) > Anger(Stance.Friendly));
        Assert.True(Anger(Stance.Friendly) > 0);
        Assert.Equal(0, Anger(Stance.Neutral));
        Assert.True(Fear(Stance.Neutral) > 0);
        Assert.True(Fear(Stance.Ally) > Fear(Stance.Neutral));
        Assert.Equal(0, Fear(Stance.Hostile));
        Assert.Equal(0, Fear(Stance.Enemy));
        // A kill is worse to see than a hurt.
        var k = new Memory(); k.Witnessed(Stance.Ally, 1, Timid);
        var h = new Memory(); h.Witnessed(Stance.Ally, Memory.HarmSeverity, Timid);
        Assert.True(k.Anger > h.Anger);
    }

    [Fact]
    public void GrudgesLastAsLongAsPersonalityDecidesAndFearFadesFasterForTheBrave()
    {
        var spite = new Memory(); spite.Set(1, 1, 0);
        var forgive = new Memory(); forgive.Set(1, 1, 0);
        for (int i = 0; i < 60; i++) { spite.Tick(1, Spiteful); forgive.Tick(1, Forgiving); }
        Assert.True(spite.Anger > 0.7);
        Assert.Equal(0, forgive.Anger);

        var bold = new Memory(); bold.Set(0, 1, 0);
        var timid = new Memory(); timid.Set(0, 1, 0);
        for (int i = 0; i < 12; i++) { bold.Tick(1, Bold); timid.Tick(1, Timid); }
        Assert.True(bold.Fear < timid.Fear);
    }

    [Fact]
    public void EverythingEventuallyFades()
    {
        var m = new Memory();
        m.Set(1, 1, 1);
        for (int i = 0; i < 2000; i++) m.Tick(1, Spiteful);
        Assert.True(m.Blank);
        m.Tick(-5, Spiteful);
        m.Tick(0, Spiteful);
        Assert.True(m.Blank);
    }
}

public class DispositionMatrixTests
{
    private static readonly Traits[] Samples = [new(0, 0, 0), new(0.2, 0.5, 0.9), new(0.5, 0.5, 0.5), new(0.8, 0.9, 0.2), new(1, 1, 1)];

    private static int Threat(Disposition d) => d switch { Disposition.Aggressive => 3, Disposition.Territorial => 2, Disposition.Neutral => 1, _ => 0 };

    private static IEnumerable<DispositionInput> Matrix()
    {
        foreach (var species in SpeciesInfo.All)
            foreach (var stance in Enum.GetValues<Stance>())
                foreach (var traits in Samples)
                    foreach (bool provoked in new[] { false, true })
                        foreach (bool scared in new[] { false, true })
                            yield return new DispositionInput(species, stance, traits, provoked, scared);
    }

    [Fact]
    public void EveryCombinationGivesADefinedDispositionDeterministically()
    {
        int n = 0;
        foreach (var input in Matrix())
        {
            var d = Dispositions.Decide(input);
            Assert.True(Enum.IsDefined(d));
            Assert.Equal(d, Dispositions.Decide(input));
            n++;
        }
        Assert.Equal(SpeciesInfo.All.Count * 6 * 5 * 4, n);
    }

    [Fact]
    public void TheHollowIsAggressiveWhateverElseIsTrue()
    {
        foreach (var input in Matrix().Where(i => i.Species.AlwaysHostile))
            Assert.Equal(Disposition.Aggressive, Dispositions.Decide(input));
    }

    [Fact]
    public void PacifistsNeverFightWhateverHappens()
    {
        foreach (var input in Matrix().Where(i => i.Species.Pacifist))
            Assert.True(Threat(Dispositions.Decide(input)) <= 1, $"{input.Species.Name} {input.Stance} {input}");
    }

    [Fact]
    public void AlliesOnlyAppearForTrustingCreaturesThatAreCalmAndAbleToFight()
    {
        foreach (var input in Matrix())
            if (Dispositions.Decide(input) == Disposition.Allied)
            {
                Assert.Equal(Stance.Ally, input.Stance);
                Assert.True(input.Species.CanAlly);
                Assert.False(input.Provoked);
                Assert.False(input.Scared);
            }
    }

    [Fact]
    public void WarmerStancesNeverMakeACalmCreatureMoreDangerous()
    {
        foreach (var species in SpeciesInfo.All.Where(s => !s.AlwaysHostile))
            foreach (var traits in Samples)
            {
                int last = int.MaxValue;
                foreach (var stance in Enum.GetValues<Stance>())
                {
                    int threat = Threat(Dispositions.Decide(new DispositionInput(species, stance, traits, false, false)));
                    Assert.True(threat <= last, $"{species.Name} {traits} {stance}");
                    last = threat;
                }
            }
    }

    [Fact]
    public void AProvokedFighterFightsUnlessItIsAFrightenedCoward()
    {
        foreach (var input in Matrix().Where(i => i.Provoked && !i.Species.Pacifist && !i.Species.AlwaysHostile))
        {
            var d = Dispositions.Decide(input);
            bool coward = input.Scared && input.Traits.Bravery < Dispositions.CowardBelow;
            Assert.Equal(coward ? Disposition.Skittish : Disposition.Aggressive, d);
        }
    }

    [Fact]
    public void FearMakesTheGentleRunButTheBoldGuardHoldTheirGround()
    {
        var selkie = SpeciesInfo.Of(EnemyKind.Selkie);
        Assert.Equal(Disposition.Skittish, Dispositions.Decide(new(selkie, Stance.Ally, Traits.Middling, false, true)));
        var kelpie = SpeciesInfo.Of(EnemyKind.Kelpie);
        Assert.Equal(Disposition.Territorial, Dispositions.Decide(new(kelpie, Stance.Hostile, new Traits(0.9, 0.5, 0), false, true)));
        Assert.Equal(Disposition.Skittish, Dispositions.Decide(new(kelpie, Stance.Hostile, new Traits(0.2, 0.5, 0), false, true)));
    }

    [Fact]
    public void AHatedPlayerMakesNeutralThingsHostileAndARevereredOneMakesWaryThingsFriendly()
    {
        var kelpie = SpeciesInfo.Of(EnemyKind.Kelpie);
        var selkie = SpeciesInfo.Of(EnemyKind.Selkie);
        var bold = new Traits(0.9, 0.5, 0.5);

        // A normally calm territorial thing is aggressive toward someone it hates.
        Assert.Equal(Disposition.Territorial, Dispositions.Decide(new(kelpie, Stance.Neutral, bold, false, false)));
        Assert.Equal(Disposition.Aggressive, Dispositions.Decide(new(kelpie, Dispositions.EffectiveStance(kelpie, ReputationTier.Hated, 0), bold, false, false)));
        Assert.Equal(Disposition.Aggressive, Dispositions.Decide(new(selkie, Dispositions.EffectiveStance(selkie, ReputationTier.Hated, 0), bold, false, false)));
        Assert.Equal(Disposition.Skittish, Dispositions.Decide(new(selkie, Dispositions.EffectiveStance(selkie, ReputationTier.Hated, 0), new Traits(0.1, 0.5, 0.5), false, false)));

        // Wary creatures are friendly to someone they revere.
        Assert.Equal(Disposition.Skittish, Dispositions.Decide(new(selkie, Dispositions.EffectiveStance(selkie, ReputationTier.Wary, 0), bold, false, false)));
        Assert.Equal(Disposition.Allied, Dispositions.Decide(new(selkie, Dispositions.EffectiveStance(selkie, ReputationTier.Revered, 0), bold, false, false)));
        Assert.Equal(Disposition.Curious, Dispositions.Decide(new(SpeciesInfo.Of(EnemyKind.Brownie), Dispositions.EffectiveStance(SpeciesInfo.Of(EnemyKind.Brownie), ReputationTier.Revered, 0), bold, false, false)));
    }

    [Fact]
    public void TheHollowIgnoresStandingAndGratitudeWarmsOthersByAStep()
    {
        var husk = SpeciesInfo.Of(EnemyKind.Husk);
        foreach (var tier in Enum.GetValues<ReputationTier>())
            Assert.Equal(Stance.Enemy, Dispositions.EffectiveStance(husk, tier, 1));

        var brownie = SpeciesInfo.Of(EnemyKind.Brownie);
        Assert.Equal(Stance.Neutral, Dispositions.EffectiveStance(brownie, ReputationTier.Neutral, 0));
        Assert.Equal(Stance.Friendly, Dispositions.EffectiveStance(brownie, ReputationTier.Neutral, 0.6));
        Assert.Equal(Stance.Ally, Dispositions.EffectiveStance(brownie, ReputationTier.Revered, 1));
    }

    [Fact]
    public void FriendsYouCanWalkUpToFollowAndShyOnesStillRun()
    {
        var brownie = SpeciesInfo.Of(EnemyKind.Brownie);
        foreach (double curiosity in new[] { 0.0, 0.9 })
            Assert.Equal(Disposition.Curious, Dispositions.Decide(new(brownie, Stance.Friendly, new Traits(0.1, 0.5, curiosity), false, false)));
        Assert.Equal(Disposition.Passive, Dispositions.Decide(new(SpeciesInfo.Of(EnemyKind.BeanNighe), Stance.Friendly, new Traits(0.1, 0.5, 0.9), false, false)));
        Assert.Equal(Disposition.Passive, Dispositions.Decide(new(SpeciesInfo.Of(EnemyKind.Hare), Stance.Friendly, new Traits(0.1, 0.5, 0.9), false, false)));
    }
}

public class BrainTests
{
    private const double Dt = 0.1;
    private static readonly Traits Mid = Traits.Middling;

    private static Perception At(double distance, double approach = 0, bool sees = true, double? fromHome = null, double hostile = double.PositiveInfinity) =>
        new(distance, approach, sees, fromHome ?? distance, hostile);

    private static Intent Step(Brain b, Disposition d, SpeciesInfo sp, Perception p, bool provoked = false, bool scared = false, bool furious = false, Traits? t = null) =>
        b.Step(Dt, d, sp, t ?? Mid, provoked, scared, furious, p);

    [Fact]
    public void PassiveCreaturesFleeWhenCloseIgnoreWhenFarAndNeverAttack()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Brownie);
        var b = new Brain();
        Assert.Equal(Intent.Idle, Step(b, Disposition.Passive, sp, At(12)));
        Assert.Equal(Intent.Flee, Step(b, Disposition.Passive, sp, At(2)));
        Assert.Equal(Intent.Flee, Step(b, Disposition.Passive, sp, At(4))); // keeps running until well clear
        Assert.Equal(Intent.Idle, Step(b, Disposition.Passive, sp, At(12)));
        Assert.Equal(Intent.Idle, Step(b, Disposition.Passive, sp, At(2, sees: false)));
        Assert.Equal(Intent.Flee, Step(b, Disposition.Passive, sp, At(30), provoked: true));
        Assert.Equal(Intent.Flee, Step(b, Disposition.Passive, sp, At(30), scared: true));
    }

    [Fact]
    public void NoNonHostileDispositionOfAFleeingKindEverAttacksWhenItIsNotAllowedTo()
    {
        var rng = new Hollowfall.Core.Dungeon.SeededRandom(1);
        foreach (var d in new[] { Disposition.Passive, Disposition.Skittish, Disposition.Curious, Disposition.Allied })
        {
            var b = new Brain();
            for (int i = 0; i < 2000; i++)
            {
                var p = At(rng.NextDouble() * 30, rng.NextDouble() * 8 - 2, rng.Chance(0.7), rng.NextDouble() * 30, rng.Chance(0.3) ? rng.NextDouble() * 15 : double.PositiveInfinity);
                var intent = Step(b, d, SpeciesInfo.Of(EnemyKind.Selkie), p, rng.Chance(0.3), rng.Chance(0.3), rng.Chance(0.1));
                Assert.NotEqual(Intent.Attack, intent);
                Assert.NotEqual(Intent.Warn, intent);
            }
        }
    }

    [Fact]
    public void SkittishCreaturesRunFromAFastApproachButNotAStrollOrADistantPlayer()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Selkie);
        var b = new Brain();
        Assert.Equal(Intent.Idle, Step(b, Disposition.Skittish, sp, At(6, approach: 1)));
        Assert.Equal(Intent.Flee, Step(b, Disposition.Skittish, sp, At(6, approach: 5)));
        Assert.Equal(Intent.Flee, Step(b, Disposition.Skittish, sp, At(8, approach: 0))); // still running
        Assert.Equal(Intent.Idle, Step(b, Disposition.Skittish, sp, At(25)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Skittish, sp, At(20, approach: 9)));
        Assert.Equal(Intent.Flee, Step(new Brain(), Disposition.Skittish, sp, At(2, approach: 0))); // cornered distance
        Assert.Equal(Intent.Flee, Step(new Brain(), Disposition.Skittish, sp, At(30), scared: true));
    }

    [Fact]
    public void BoldCreaturesFleeLessEagerly()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Selkie);
        Assert.Equal(Intent.Flee, Step(new Brain(), Disposition.Skittish, sp, At(7, approach: 4), t: new Traits(0, 0.5, 0.5)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Skittish, sp, At(7, approach: 4), t: new Traits(1, 0.5, 0.5)));
    }

    [Fact]
    public void TerritorialCreaturesWarnThenAttackOnlyInsideTheirGround()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Kelpie);
        var b = new Brain();
        Assert.Equal(Intent.Idle, Step(b, Disposition.Territorial, sp, At(12, fromHome: 12)));
        Assert.Equal(Intent.Warn, Step(b, Disposition.Territorial, sp, At(5, fromHome: 5)));
        Intent last = Intent.Warn;
        int steps = 0;
        while (last == Intent.Warn && steps++ < 100) last = Step(b, Disposition.Territorial, sp, At(5, fromHome: 5));
        Assert.Equal(Intent.Attack, last);
        Assert.InRange(steps * Dt, Brain.WarnFastest - 0.2, Brain.WarnSlowest + 0.2);

        // It chases a little beyond its ground, then gives up and goes home.
        Assert.Equal(Intent.Attack, Step(b, Disposition.Territorial, sp, At(9, fromHome: 9)));
        Assert.Equal(Intent.Idle, Step(b, Disposition.Territorial, sp, At(20, fromHome: 20)));
        Assert.Equal(Intent.Warn, Step(b, Disposition.Territorial, sp, At(5, fromHome: 5)));
    }

    [Fact]
    public void ABraveCreatureWarnsForLessTimeThanATimidOne()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Kelpie);
        int Warning(Traits t)
        {
            var b = new Brain();
            int n = 0;
            while (Step(b, Disposition.Territorial, sp, At(3, fromHome: 3), t: t) != Intent.Attack) n++;
            return n;
        }
        Assert.True(Warning(new Traits(1, 0.5, 0)) < Warning(new Traits(0, 0.5, 0)));
    }

    [Fact]
    public void FuryAttacksAtOnceWithNoWarning()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Kelpie);
        Assert.Equal(Intent.Attack, Step(new Brain(), Disposition.Aggressive, sp, At(5), provoked: true, furious: true));
        Assert.Equal(Intent.Warn, Step(new Brain(), Disposition.Aggressive, sp, At(5), provoked: true));
    }

    [Fact]
    public void AnAggressiveCreatureOfTheHollowAttacksFromAnywhereAtOnce()
    {
        var husk = SpeciesInfo.Of(EnemyKind.Husk);
        foreach (double d in new[] { 1.0, 20, 500 })
            Assert.Equal(Intent.Attack, Step(new Brain(), Disposition.Aggressive, husk, At(d, sees: false)));
    }

    [Fact]
    public void ANaturalAggressorAttacksOnSightWithinRangeOnly()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Kelpie);
        Assert.Equal(Intent.Attack, Step(new Brain(), Disposition.Aggressive, sp, At(10)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Aggressive, sp, At(10, sees: false)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Aggressive, sp, At(sp.AggroRange + 5)));
    }

    [Fact]
    public void CuriousCreaturesFollowAtADistanceAndBackOffIfCrowded()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Brownie);
        Assert.Equal(Intent.Follow, Step(new Brain(), Disposition.Curious, sp, At(9)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Curious, sp, At(3)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Curious, sp, At(1)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Curious, sp, At(9, approach: 6)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Curious, sp, At(40)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Curious, sp, At(9, sees: false)));
    }

    [Fact]
    public void AlliesFollowAndAssistAndNeverTurnOnThePlayer()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Selkie);
        Assert.Equal(Intent.Follow, Step(new Brain(), Disposition.Allied, sp, At(10)));
        Assert.Equal(Intent.Idle, Step(new Brain(), Disposition.Allied, sp, At(2)));
        Assert.Equal(Intent.Assist, Step(new Brain(), Disposition.Allied, sp, At(10, hostile: 6)));
        Assert.Equal(Intent.Follow, Step(new Brain(), Disposition.Allied, sp, At(10, hostile: 30)));
        // Even angry and frightened, an Allied disposition (which Decide would no longer give) never attacks.
        Assert.NotEqual(Intent.Attack, Step(new Brain(), Disposition.Allied, sp, At(1), provoked: true, furious: true));
    }

    [Fact]
    public void WarnLeftCountsDownAndIsZeroOtherwise()
    {
        var sp = SpeciesInfo.Of(EnemyKind.Kelpie);
        var b = new Brain();
        Assert.Equal(0, b.WarnLeft);
        Step(b, Disposition.Territorial, sp, At(3, fromHome: 3));
        Assert.InRange(b.WarnLeft, 0.1, Brain.WarnSlowest);
        b.Reset();
        Assert.Equal(Intent.Idle, b.Intent);
        Assert.Equal(0, b.WarnLeft);
    }
}

public class MindTests
{
    private static Mind Make(EnemyKind kind, Traits? traits = null) => new(SpeciesInfo.Of(kind), traits ?? Traits.Middling, new Vec3(0, 0, 0));
    private static Perception Near(double d = 5, double fromHome = 5) => new(d, 0, true, fromHome);

    [Fact]
    public void ACalmNeutralKelpieIgnoresYouFarAwayAndWarnsThenAttacksNearHome()
    {
        var m = Make(EnemyKind.Kelpie);
        m.Update(0.1, ReputationTier.Neutral, Near(20, 20));
        Assert.Equal(Intent.Idle, m.Intent);
        Assert.Equal(Disposition.Territorial, m.Disposition);
        m.Update(0.1, ReputationTier.Neutral, Near(4, 4));
        Assert.Equal(Intent.Warn, m.Intent);
        Assert.Equal(Cue.Hostile, m.Cue);
        for (int i = 0; i < 30; i++) m.Update(0.1, ReputationTier.Neutral, Near(4, 4));
        Assert.Equal(Intent.Attack, m.Intent);
    }

    [Fact]
    public void BeingHurtProvokesThenTimeCalmsIt()
    {
        var m = Make(EnemyKind.Selkie, new Traits(0.6, 0.0, 0.5));
        m.Update(0.1, ReputationTier.Neutral, Near(20, 20));
        Assert.Equal(Disposition.Skittish, m.Disposition);
        m.Memory.Hurt(0.5, m.Traits);
        var ev = m.Update(0.1, ReputationTier.Neutral, Near(20, 20));
        Assert.True(ev.HasFlag(MindEvents.Provoked));
        Assert.True(m.Provoked);
        Assert.Equal(Disposition.Aggressive, m.Disposition);

        var calmed = MindEvents.None;
        for (int i = 0; i < 3000 && !calmed.HasFlag(MindEvents.Calmed); i++) calmed |= m.Update(0.1, ReputationTier.Neutral, Near(20, 20));
        Assert.True(calmed.HasFlag(MindEvents.Calmed));
        Assert.Equal(Intent.Idle, m.Intent);
        Assert.Equal(Disposition.Skittish, m.Disposition);
    }

    [Fact]
    public void ProvokedStateDoesNotFlickerAroundTheThreshold()
    {
        var m = Make(EnemyKind.Kelpie, new Traits(0.8, 1, 0));
        m.Memory.Set(0.31, 0, 0);
        int provoked = 0, calmed = 0;
        for (int i = 0; i < 300; i++)
        {
            m.Memory.Set(i % 2 == 0 ? 0.31 : 0.2, 0, 0);
            var ev = m.Update(0, ReputationTier.Neutral, Near());
            if (ev.HasFlag(MindEvents.Provoked)) provoked++;
            if (ev.HasFlag(MindEvents.Calmed)) calmed++;
        }
        Assert.Equal(1, provoked);
        Assert.Equal(0, calmed);
    }

    [Fact]
    public void AlliedMindsAnnounceOnceAndNeverAttackTheirFriend()
    {
        var m = Make(EnemyKind.Selkie, new Traits(0.8, 0.5, 0.5));
        var first = m.Update(0.1, ReputationTier.Revered, Near(8));
        Assert.True(first.HasFlag(MindEvents.Allied));
        Assert.False(m.Update(0.1, ReputationTier.Revered, Near(8)).HasFlag(MindEvents.Allied));
        Assert.Equal(Cue.Allied, m.Cue);
    }

    [Fact]
    public void DispositionIsOnlyKnownOnceNoticed()
    {
        var m = Make(EnemyKind.Brownie);
        m.Update(0.1, ReputationTier.Friendly, new Perception(30, 0, true, 30));
        Assert.False(m.Known);
        Assert.Equal(Cue.None, m.Cue);
        m.Update(0.1, ReputationTier.Friendly, new Perception(10, 0, false, 10));
        Assert.False(m.Known);
        m.Update(0.1, ReputationTier.Friendly, new Perception(10, 0, true, 10));
        Assert.True(m.Known);
        Assert.Equal(Cue.Friendly, m.Cue);
    }

    [Fact]
    public void CuesFollowTheDisposition()
    {
        var selkie = Make(EnemyKind.Selkie, new Traits(0.8, 0.5, 0.5));
        selkie.Update(0.1, ReputationTier.Neutral, Near(10));
        Assert.Equal(Cue.Wary, selkie.Cue);
        selkie.Update(0.1, ReputationTier.Revered, Near(10));
        Assert.Equal(Cue.Allied, selkie.Cue);
        var husk = Make(EnemyKind.Husk);
        husk.Update(0.1, ReputationTier.Hated, Near(5));
        Assert.Equal(Cue.None, husk.Cue);
        Assert.Equal(Intent.Attack, husk.Intent);
    }

    [Fact]
    public void HollowMindsIgnoreMemoryEntirely()
    {
        var husk = Make(EnemyKind.Husk);
        husk.Memory.Set(1, 1, 1);
        for (int i = 0; i < 10; i++) husk.Update(0.1, ReputationTier.Revered, new Perception(500, 0, false, 500));
        Assert.Equal(Disposition.Aggressive, husk.Disposition);
        Assert.Equal(Intent.Attack, husk.Intent);
        Assert.False(husk.Provoked);
    }

    [Fact]
    public void GratitudeWarmsAStanceAfterAGift()
    {
        var m = Make(EnemyKind.Brownie, new Traits(0.1, 0.5, 0.9));
        m.Update(0.1, ReputationTier.Neutral, Near(10));
        Assert.Equal(Disposition.Passive, m.Disposition);
        m.Memory.Gift();
        m.Update(0.1, ReputationTier.Neutral, Near(10));
        Assert.Equal(Disposition.Curious, m.Disposition);
    }
}
