using Hollowfall.Core.Terrain;

namespace Hollowfall.Core.Combat;

/// <summary>A rule that changes the hostile enemies of one room. <see cref="None"/> is most rooms.</summary>
public enum RoomMutator
{
    None,

    /// <summary>The enemies move faster and hit harder.</summary>
    Frenzy,

    /// <summary>The enemies have more health, so the fight takes longer.</summary>
    Hardened,

    /// <summary>The enemies have less health.</summary>
    Weakened,
}

/// <summary>
/// Which rooms get a rule, and what it does to their enemies. Chosen from the world seed and the room's place on its floor, so the
/// same dungeon always has the same rooms, and it is a pure function (no shared state) as dungeon generation must be. Bosses and
/// the Folk, wanderers and vermin are never changed: a mutator makes a fight harder or longer, never a creature harmless.
/// </summary>
public static class RoomMutators
{
    /// <summary>About one room in this many gets a rule.</summary>
    public const int OneIn = 3;

    public const double FrenzySpeed = 1.3;
    public const double FrenzyDamage = 1.15;
    public const double HardenedHealth = 1.4;
    public const double WeakenedHealth = 0.7;

    /// <summary>The rule for the room with <paramref name="roomId"/> on <paramref name="floor"/>, for a hostile enemy of <paramref name="kind"/>.</summary>
    public static RoomMutator For(long seed, int floor, int roomId, EnemyKind kind)
    {
        if (kind is not (EnemyKind.Husk or EnemyKind.Spitter)) return RoomMutator.None;
        ulong h = SeedHash.Hash(seed, floor, roomId, salt: 0x3A7);
        if (h % OneIn != 0) return RoomMutator.None;
        return (RoomMutator)(1 + (int)((h / OneIn) % 3));
    }

    /// <summary>The spec of an enemy in a room with <paramref name="mutator"/>. The base spec is returned unchanged for <see cref="RoomMutator.None"/>.</summary>
    public static EnemySpec Apply(EnemySpec spec, RoomMutator mutator) => mutator switch
    {
        RoomMutator.Frenzy => spec with { Speed = spec.Speed * FrenzySpeed, MeleeDamage = spec.MeleeDamage * FrenzyDamage, ProjectileDamage = spec.ProjectileDamage * FrenzyDamage },
        RoomMutator.Hardened => spec with { Health = spec.Health * HardenedHealth },
        RoomMutator.Weakened => spec with { Health = spec.Health * WeakenedHealth },
        _ => spec,
    };
}
