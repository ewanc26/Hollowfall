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

public sealed record FloorCompleted(int Floor, FloorResult Result) : CombatEvent;

public sealed record PlayerDied : CombatEvent;

public sealed record KeyCollected(int SpawnIndex) : CombatEvent;

public sealed record ChestOpened(int SpawnIndex, double Healed, bool WasLocked) : CombatEvent;

public sealed record ChestLocked(int SpawnIndex) : CombatEvent;

public sealed record TrapSprung(int SpawnIndex, double Damage) : CombatEvent;
