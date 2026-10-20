namespace Hollowfall.Core.Combat;

public enum ParryKind
{
    Melee,
    Projectile,
    Shotgun,
}

/// <summary>What happened this tick, for the host to turn into sound, particles and UI.</summary>
public abstract record CombatEvent;

/// <summary>A shot. <see cref="Path"/> has one piece per straight run: more than one when the shot went through portals.</summary>
public sealed record ShotFired(string Weapon, Vec3 Origin, Vec3 End, IReadOnlyList<(Vec3 From, Vec3 To)>? Path = null) : CombatEvent;

public sealed record PortalPlaced(Portals.PortalColor Color, bool Success, Vec3 Position, Vec3 Normal, Vec3 Up, Portals.PlaceFailure Failure) : CombatEvent;

public sealed record PortalTraversed(string What, Portals.PortalColor Entered) : CombatEvent;

public sealed record EnemySpawned(int Id, EnemyKind Kind, Vec3 Position) : CombatEvent;

public sealed record EnemyTelegraph(int Id) : CombatEvent;

public sealed record EnemyHit(int Id, HitZone Zone, double Damage, bool Killed, Vec3 Point) : CombatEvent;

public sealed record EnemyKilled(int Id, EnemyKind Kind) : CombatEvent;

public sealed record PlayerHurt(double Damage, double Health) : CombatEvent;

public sealed record Parried(ParryKind Kind, double Healed) : CombatEvent;

public sealed record ProjectileFired(int Id, Vec3 Position) : CombatEvent;

public sealed record ProjectileReflected(int Id) : CombatEvent;

public sealed record ProjectileGone(int Id, bool Exploded, Vec3 Position) : CombatEvent;

public sealed record EncounterStarted(int Floor, int RoomId, int Enemies) : CombatEvent;

public sealed record EncounterCleared(int Floor, int RoomId) : CombatEvent;

public sealed record DoorsChanged(int Floor, int RoomId, bool Locked) : CombatEvent;

public sealed record FloorCompleted(int Floor, FloorResult Result, double Seconds = 0) : CombatEvent;

public sealed record PlayerDied : CombatEvent;

public sealed record KeyCollected(int SpawnIndex) : CombatEvent;

/// <summary><see cref="Loot"/> is what was inside; <see cref="Applied"/> is what it did (health healed, hard damage removed, style points, keys, or perk seconds).</summary>
public sealed record ChestOpened(int SpawnIndex, double Healed, bool WasLocked, Loot? Loot = null, double Applied = 0) : CombatEvent;

public sealed record ChestLocked(int SpawnIndex) : CombatEvent;

public sealed record TrapSprung(int SpawnIndex, double Damage) : CombatEvent;

/// <summary>The player hit an enemy at speed. The host scales the player's velocity by <see cref="SpeedKept"/>.</summary>
public sealed record MeteorImpact(int Id, double Speed, double Damage, double Points, bool LongFall, double SpeedKept) : CombatEvent;
/// <summary>A portal closed on an enemy. Severed enemies drop no blood. <see cref="Killed"/> false means it was staggered instead.</summary>
public sealed record EnemySevered(int Id, EnemyKind Kind, Portals.PortalColor Portal, double Damage, bool Killed, Vec3 Point, double Points) : CombatEvent;

/// <summary>The player fell into a pit. <see cref="First"/> is true the first time in the run, for a short hint. Recovery follows (see <see cref="PlayerRecovered"/>).</summary>
public sealed record PlayerFell(Dungeon.GapHazard Hazard, double Damage, bool First) : CombatEvent;

/// <summary>The player was put back on safe ground after a fall: the host moves the body to <see cref="Position"/> (feet) and stops its fall.</summary>
public sealed record PlayerRecovered(Vec3 Position) : CombatEvent;

/// <summary>The player stands at the lip of a gap for the first time in this run: the host shows how to cross it.</summary>
public sealed record PitEdgeReached(Dungeon.LinkKind Kind) : CombatEvent;

/// <summary>A portal was replaced or removed. Fired for every closure, whether or not anything was caught in it.</summary>
public sealed record PortalClosed(Portals.PortalColor Color, Vec3 Position, Vec3 Normal, Vec3 Up) : CombatEvent;

// ---- relationships (phase 1 of the RPG systems; append new events below) ----

/// <summary>The player's standing with a faction crossed into another tier. The host shows a toast.</summary>
public sealed record ReputationChanged(Relations.Faction Faction, Relations.ReputationTier Old, Relations.ReputationTier New) : CombatEvent;

/// <summary>A creature was hurt or frightened enough to start warning and fighting back.</summary>
public sealed record EntityProvoked(int Id, Relations.Faction Faction) : CombatEvent;

/// <summary>A creature forgot its anger and settled down.</summary>
public sealed record EntityCalmed(int Id, Relations.Faction Faction) : CombatEvent;

/// <summary>A creature saw (or heard about) <paramref name="SourceId"/> being hurt and took it to heart.</summary>
public sealed record WitnessAlerted(int Id, int SourceId, Relations.Faction Faction) : CombatEvent;

/// <summary>A creature now fights on the player's side.</summary>
public sealed record EntityAllied(int Id, Relations.Faction Faction) : CombatEvent;

/// <summary>A creature gave the player something for their standing.</summary>
public sealed record BoonGiven(int Id, string Boon, double Amount) : CombatEvent;

/// <summary>A Bean Nighe warned of an ambush: the next encounter room will hold an extra wave.</summary>
public sealed record OmenSensed(int Id) : CombatEvent;

/// <summary>An armed omen came true: <paramref name="Extra"/> more enemies joined the encounter.</summary>
public sealed record OmenFulfilled(int Floor, int RoomId, int Extra) : CombatEvent;
