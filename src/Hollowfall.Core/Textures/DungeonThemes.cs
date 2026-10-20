using Hollowfall.Core.Dungeon;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Textures;

/// <summary>How a dungeon floor looks: plain brick at the top, then damp and mossy, then cracked, ashen and ember-lit.</summary>
public enum DungeonTheme
{
    Plain,
    Mossy,
    Ashen,
}

/// <summary>The light and fog of a place. Colours are 0 to 1; <see cref="FogDensity"/> is Godot's exponential fog density.</summary>
public readonly record struct Atmosphere(float FogR, float FogG, float FogB, float FogDensity, float AmbientR, float AmbientG, float AmbientB, float AmbientEnergy);

/// <summary>
/// Which theme a dungeon block wears, and the fog that goes with it. The theme is a pure function of height: the dungeon's
/// floors sit <see cref="DungeonSite.FloorSpacing"/> blocks apart from <see cref="DungeonSite.FirstFloorY"/> down, so the mesher
/// needs no knowledge of any particular site. Only dungeon blocks are themed; terrain never is, and anything above the first
/// floor (the entrance plaza and the top of the spiral stair) is plain.
/// </summary>
public static class DungeonThemes
{
    /// <summary>The highest block of the topmost floor's band.</summary>
    public static int TopOfFloors => DungeonSite.FirstFloorY + DungeonSite.FloorSpacing - 1;

    /// <summary>The blocks that only the dungeon builds, and that so change with depth.</summary>
    public static bool IsThemed(BlockType b) =>
        b is BlockType.StoneBrick or BlockType.SecretDoor or BlockType.BrickFloor or BlockType.Pillar or BlockType.Stairs;

    /// <summary>The floor whose band holds this height, or -1 above the first floor.</summary>
    public static int FloorAtY(int y) => y > TopOfFloors ? -1 : (TopOfFloors - y) / DungeonSite.FloorSpacing;

    /// <summary>The block's height within its floor: 0 is the paving, 6 the ceiling.</summary>
    public static int Layer(int y) => ((y - DungeonSite.FirstFloorY) % DungeonSite.FloorSpacing + DungeonSite.FloorSpacing) % DungeonSite.FloorSpacing;

    /// <summary>Floor 0 is plain, floor 1 mossy, and floor 2 and everything deeper ashen.</summary>
    public static DungeonTheme ForFloor(int floor) => floor switch
    {
        <= 0 => DungeonTheme.Plain,
        1 => DungeonTheme.Mossy,
        _ => DungeonTheme.Ashen,
    };

    public static DungeonTheme ForY(int y) => ForFloor(FloorAtY(y));

    /// <summary>The surface's own light, which is what the sky is tuned for.</summary>
    public static readonly Atmosphere Surface = new(0.72f, 0.82f, 0.92f, 0.003f, 0.55f, 0.60f, 0.70f, 0.35f);

    /// <summary>The fog and ambient light of a dungeon theme: cold grey, green damp, then a dull ember glow.</summary>
    public static Atmosphere Of(DungeonTheme theme) => theme switch
    {
        DungeonTheme.Mossy => new(0.07f, 0.16f, 0.12f, 0.018f, 0.38f, 0.58f, 0.48f, 0.38f),
        DungeonTheme.Ashen => new(0.22f, 0.10f, 0.06f, 0.020f, 0.60f, 0.50f, 0.46f, 0.40f),
        _ => new(0.09f, 0.10f, 0.14f, 0.014f, 0.46f, 0.52f, 0.64f, 0.36f),
    };

    /// <summary>The atmosphere for a floor index; a negative index means the surface.</summary>
    public static Atmosphere ForAtmosphere(int floor) => floor < 0 ? Surface : Of(ForFloor(floor));

    public static Atmosphere Lerp(Atmosphere a, Atmosphere b, float t) => new(
        L(a.FogR, b.FogR, t), L(a.FogG, b.FogG, t), L(a.FogB, b.FogB, t), L(a.FogDensity, b.FogDensity, t),
        L(a.AmbientR, b.AmbientR, t), L(a.AmbientG, b.AmbientG, t), L(a.AmbientB, b.AmbientB, t), L(a.AmbientEnergy, b.AmbientEnergy, t));

    private static float L(float a, float b, float t) => t <= 0f ? a : t >= 1f ? b : a + (b - a) * t;
}
