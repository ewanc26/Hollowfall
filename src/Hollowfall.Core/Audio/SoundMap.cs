using Hollowfall.Core.Combat;
using Hollowfall.Core.Portals;

namespace Hollowfall.Core.Audio;

/// <summary>Chooses the sound for each thing that happens in combat, so the rule is in one tested place and the host only plays.</summary>
public static class SoundMap
{
    public static SoundId? For(CombatEvent e) => e switch
    {
        ShotFired { Weapon: "shotgun" } => SoundId.ShotgunShot,
        ShotFired => SoundId.RevolverShot,
        PortalPlaced { Success: false } => SoundId.PortalFail,
        PortalPlaced { Color: PortalColor.Teal } => SoundId.PortalTeal,
        PortalPlaced => SoundId.PortalViolet,
        PortalTraversed => SoundId.PortalCross,
        EnemySevered => SoundId.Sever,
        EnemyTelegraph => SoundId.Telegraph,
        EnemyHit { Killed: true } => null, // the kill has its own sound
        EnemyHit { Zone: HitZone.Head } => SoundId.Headshot,
        EnemyHit => SoundId.Hit,
        EnemyKilled => SoundId.Kill,
        PlayerHurt => SoundId.Hurt,
        PlayerDied => SoundId.Death,
        Parried => SoundId.Parry,
        ProjectileFired => SoundId.EnemyShot,
        ProjectileGone { Exploded: true } => SoundId.Explosion,
        EncounterStarted => SoundId.DoorsLock,
        EncounterCleared => SoundId.DoorsOpen,
        FloorCompleted => SoundId.FloorComplete,
        KeyCollected => SoundId.KeyPickup,
        ChestOpened => SoundId.ChestOpen,
        ChestLocked => SoundId.ChestLocked,
        TrapSprung => SoundId.TrapStrike,
        MeteorImpact => SoundId.Meteor,
        ReputationChanged r => r.New > r.Old ? SoundId.StandingUp : SoundId.StandingDown,
        EntityProvoked { Faction: Relations.Faction.Brownies } => SoundId.BrownieHiss,
        EntityProvoked { Faction: Relations.Faction.Selkies } => SoundId.SelkieCry,
        EntityProvoked { Faction: Relations.Faction.Kelpies } => SoundId.KelpieWhinny,
        EntityProvoked { Faction: Relations.Faction.Wanderers } => SoundId.NigheWail,
        EntityProvoked { Faction: Relations.Faction.Vermin } => SoundId.HareScream,
        OmenSensed or OmenFulfilled => SoundId.OmenToll,
        BoonGiven or OfferingAccepted => SoundId.BoonChime,
        EntityAllied => SoundId.AllyJoin,
        CreamUsed => SoundId.CreamSip,
        _ => null,
    };

    /// <summary>How loud a sound is relative to the others, 0 to 1. Gunfire is loud; menu ticks are not.</summary>
    public static double Gain(SoundId id) => id switch
    {
        SoundId.ShotgunShot or SoundId.Explosion or SoundId.Death or SoundId.Meteor => 1.0,
        SoundId.RevolverShot or SoundId.Kill or SoundId.FloorComplete => 0.9,
        SoundId.UiMove => 0.35,
        SoundId.Ambience => 0.5,
        SoundId.StepWater => 0.55,
        SoundId.FireCrackle => 0.4,
        SoundId.CatPurr => 0.3,
        SoundId.CatMeow => 0.55,
        SoundId.CreamSip => 0.6,
        SoundId.CharmWear => 0.5,
        _ when Footsteps.IsStepSound(id) => 0.4,
        SoundId.PortalCross or SoundId.Hit => 0.6,
        _ => 0.75,
    };
}
