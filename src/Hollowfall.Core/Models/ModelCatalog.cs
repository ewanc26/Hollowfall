using Hollowfall.Core.Combat;

namespace Hollowfall.Core.Models;

/// <summary>
/// The shared palette. A small set of colours, each doing one job, so everything reads as the same world: warm rust
/// and bone for the flesh enemies, cold violet for the ranged ones, dark plate with crimson and gold for the guardian,
/// gunmetal and wood for weapons, and bright glow colours reserved for things that matter (eyes, cores, telegraphs).
/// </summary>
public static class Palette
{
    public static readonly ModelColor Rust = ModelColor.Hex(0x8a3b2a);
    public static readonly ModelColor RustDark = ModelColor.Hex(0x4a1f18);
    public static readonly ModelColor Bone = ModelColor.Hex(0xd9cfb0);
    public static readonly ModelColor Violet = ModelColor.Hex(0x6a3a9a);
    public static readonly ModelColor VioletDark = ModelColor.Hex(0x35205a);
    public static readonly ModelColor Plate = ModelColor.Hex(0x34303c);
    public static readonly ModelColor PlateDark = ModelColor.Hex(0x1f1c26);
    public static readonly ModelColor Crimson = ModelColor.Hex(0x9a1626);
    public static readonly ModelColor Gold = ModelColor.Hex(0xc9a227);
    public static readonly ModelColor Steel = ModelColor.Hex(0x4a4f5a);
    public static readonly ModelColor SteelLight = ModelColor.Hex(0x8189a0);
    public static readonly ModelColor Wood = ModelColor.Hex(0x6b4a2f);
    public static readonly ModelColor Stone = ModelColor.Hex(0x6d6a72);

    // Glow colours: unlit and bright.
    public static readonly ModelColor EyeAmber = ModelColor.Hex(0xffb830);
    public static readonly ModelColor Ember = ModelColor.Hex(0xff6a1a);
    public static readonly ModelColor Hot = ModelColor.Hex(0xff3a2a);
    public static readonly ModelColor Teal = ModelColor.Hex(0x19d3c5);
    public static readonly ModelColor VioletGlow = ModelColor.Hex(0xa24bff);
    public static readonly ModelColor White = ModelColor.Hex(0xfff6e0);
}

/// <summary>
/// Every model in the game, built in code. Style rules: facets not curves, chunky proportions that sit well beside voxels,
/// a silhouette that tells the enemy apart at a glance (hunched and clawed, floating and tentacled, tall and armoured),
/// glow only where the player must look, and a budget of a few hundred triangles each. Models face -Z with their feet at y = 0.
/// </summary>
public static class ModelCatalog
{
    private static Vec3 V(double x, double y, double z) => new(x, y, z);

    private static readonly Lazy<LowPolyModel> HuskModel = new(BuildHusk);
    private static readonly Lazy<LowPolyModel> SpitterModel = new(BuildSpitter);
    private static readonly Lazy<LowPolyModel> WardenModel = new(BuildWarden);
    private static readonly Lazy<LowPolyModel> OrbModel = new(() => BuildOrb("orb", Palette.Ember, Palette.Hot));
    private static readonly Lazy<LowPolyModel> ReflectedOrbModel = new(() => BuildOrb("orb_reflected", Palette.Teal, Palette.White));
    private static readonly Lazy<LowPolyModel> RevolverModel = new(BuildRevolver);
    private static readonly Lazy<LowPolyModel> ShotgunModel = new(BuildShotgun);
    private static readonly Lazy<LowPolyModel> ChestModel = new(BuildChest);
    private static readonly Lazy<LowPolyModel> KeyModel = new(BuildKey);
    private static readonly Lazy<LowPolyModel> SpikeTrapModel = new(BuildSpikeTrap);
    private static readonly Lazy<LowPolyModel> TorchModel = new(BuildTorch);
    private static readonly Lazy<LowPolyModel> LootHealthModel = new(BuildLootHealth);
    private static readonly Lazy<LowPolyModel> LootCleanseModel = new(BuildLootCleanse);
    private static readonly Lazy<LowPolyModel> LootStyleModel = new(BuildLootStyle);
    private static readonly Lazy<LowPolyModel> LootBloodlustModel = new(BuildLootBloodlust);
    private static readonly Lazy<LowPolyModel> LootParryModel = new(BuildLootParry);

    public static LowPolyModel Husk => HuskModel.Value;
    public static LowPolyModel Spitter => SpitterModel.Value;
    public static LowPolyModel Warden => WardenModel.Value;
    public static LowPolyModel Orb => OrbModel.Value;
    public static LowPolyModel ReflectedOrb => ReflectedOrbModel.Value;
    public static LowPolyModel Revolver => RevolverModel.Value;
    public static LowPolyModel Shotgun => ShotgunModel.Value;
    public static LowPolyModel Chest => ChestModel.Value;
    public static LowPolyModel Key => KeyModel.Value;
    public static LowPolyModel SpikeTrap => SpikeTrapModel.Value;
    public static LowPolyModel Torch => TorchModel.Value;
    public static LowPolyModel LootHealth => LootHealthModel.Value;
    public static LowPolyModel LootCleanse => LootCleanseModel.Value;
    public static LowPolyModel LootStyle => LootStyleModel.Value;
    public static LowPolyModel LootBloodlust => LootBloodlustModel.Value;
    public static LowPolyModel LootParry => LootParryModel.Value;

    public static IReadOnlyList<LowPolyModel> All => [Husk, Spitter, Warden, Orb, ReflectedOrb, Revolver, Shotgun, Chest, Key, SpikeTrap, Torch, LootHealth, LootCleanse, LootStyle, LootBloodlust, LootParry];

    public static LowPolyModel ForEnemy(EnemyKind kind) => kind switch
    {
        EnemyKind.Husk => Husk,
        EnemyKind.Spitter => Spitter,
        _ => Warden,
    };

    /// <summary>The pickup that floats out of a chest for a kind of loot. A spare key is the key model itself.</summary>
    public static LowPolyModel ForLoot(LootKind kind) => kind switch
    {
        LootKind.Health => LootHealth,
        LootKind.Cleanse => LootCleanse,
        LootKind.StyleBoost => LootStyle,
        LootKind.Bloodlust => LootBloodlust,
        LootKind.WideParry => LootParry,
        _ => Key,
    };

    // ---- Husk: a hunched, long-armed thing with claws and a lantern jaw ------------------------

    private static LowPolyModel BuildHusk()
    {
        var m = new ModelBuilder("husk");

        m.Part("torso", V(0, 0.95, 0))
            .Box(V(0, 0.38, -0.03), V(0.46, 0.52, 0.30), Palette.Rust, V(-12, 0, 0))
            .Box(V(0, 0.05, 0), V(0.38, 0.28, 0.26), Palette.RustDark)
            .Box(V(0, 0.18, -0.2), V(0.34, 0.12, 0.05), Palette.RustDark, V(-12, 0, 0))
            .Prism(V(0, 0.5, 0.2), 0.055, 0.16, 4, Palette.Bone, 0, V(90, 0, 0), spin: 45)
            .Prism(V(0, 0.3, 0.19), 0.05, 0.14, 4, Palette.Bone, 0, V(90, 0, 0), spin: 45)
            .Prism(V(0, 0.1, 0.17), 0.045, 0.12, 4, Palette.Bone, 0, V(90, 0, 0), spin: 45);

        m.Part("head", V(0, 1.58, -0.1), "torso")
            .Box(V(0, 0.1, -0.04), V(0.28, 0.26, 0.28), Palette.Rust.Shade(0.08f))
            .Wedge(V(0, -0.08, -0.12), V(0.22, 0.1, 0.2), Palette.Bone, V(0, 180, 0))
            .Prism(V(-0.06, -0.04, -0.2), 0.025, 0.07, 4, Palette.Bone, 0, V(180, 0, 0), spin: 45)
            .Prism(V(0.06, -0.04, -0.2), 0.025, 0.07, 4, Palette.Bone, 0, V(180, 0, 0), spin: 45)
            .Prism(V(-0.11, 0.3, -0.02), 0.045, 0.2, 4, Palette.Bone, 0, V(0, 0, 18), spin: 45)
            .Prism(V(0.11, 0.3, -0.02), 0.045, 0.2, 4, Palette.Bone, 0, V(0, 0, -18), spin: 45)
            .Box(V(-0.07, 0.13, -0.185), V(0.07, 0.04, 0.02), Palette.EyeAmber, glow: true)
            .Box(V(0.07, 0.13, -0.185), V(0.07, 0.04, 0.02), Palette.EyeAmber, glow: true);

        foreach (double side in new[] { -1.0, 1.0 })
        {
            string tag = side < 0 ? "l" : "r";
            m.Part($"arm_{tag}", V(0.3 * side, 1.45, -0.05), "torso")
                .Box(V(0, -0.2, 0), V(0.11, 0.4, 0.11), Palette.Rust)
                .Box(V(0, -0.52, -0.02), V(0.17, 0.34, 0.17), Palette.RustDark)
                .Prism(V(-0.05, -0.76, -0.04), 0.025, 0.2, 4, Palette.Bone, 0, V(180, 0, 0), spin: 45)
                .Prism(V(0, -0.77, -0.05), 0.025, 0.22, 4, Palette.Bone, 0, V(180, 0, 0), spin: 45)
                .Prism(V(0.05, -0.76, -0.04), 0.025, 0.2, 4, Palette.Bone, 0, V(180, 0, 0), spin: 45);

            m.Part($"leg_{tag}", V(0.13 * side, 0.9, 0))
                .Box(V(0, -0.22, 0.02), V(0.16, 0.44, 0.18), Palette.RustDark)
                .Box(V(0, -0.58, -0.02), V(0.13, 0.36, 0.14), Palette.Rust)
                .Wedge(V(0, -0.84, -0.07), V(0.15, 0.1, 0.26), Palette.Bone, V(0, 180, 0));
        }

        return m.Anchor("eye", V(0, 1.7, -0.25)).Build();
    }

    // ---- Spitter: a floating bulb with a cannon mouth and hanging tentacles -------------------

    private static LowPolyModel BuildSpitter()
    {
        var m = new ModelBuilder("spitter");

        m.Part("body", V(0, 0.95, 0))
            .Blob(V(0, 0, 0), V(0.36, 0.42, 0.36), Palette.Violet)
            .Prism(V(0, 0.46, 0.02), 0.07, 0.24, 4, Palette.VioletDark, 0, spin: 45)
            .Prism(V(-0.2, 0.38, 0.04), 0.06, 0.2, 4, Palette.VioletDark, 0, V(0, 0, 30), spin: 45)
            .Prism(V(0.2, 0.38, 0.04), 0.06, 0.2, 4, Palette.VioletDark, 0, V(0, 0, -30), spin: 45)
            .Blob(V(0, 0.1, -0.3), V(0.11, 0.11, 0.07), Palette.EyeAmber, glow: true)
            .Box(V(0, 0.1, -0.37), V(0.04, 0.1, 0.02), Palette.PlateDark);

        m.Part("mouth", V(0, 0.9, -0.3))
            .Prism(V(0, 0, -0.1), 0.17, 0.3, 6, Palette.VioletDark, 0.45, V(-90, 0, 0))
            .Gem(V(0, 0, -0.26), V(0.08, 0.08, 0.06), Palette.Ember, glow: true);

        for (int i = 0; i < 4; i++)
        {
            double x = (i % 2 == 0 ? -1 : 1) * 0.15, z = (i < 2 ? -1 : 1) * 0.1;
            m.Part($"tent_{i}", V(x, 0.64, z))
                .Prism(V(0, -0.22, 0), 0.07, 0.5, 5, Palette.Violet.Shade(i % 2 == 0 ? 0.05f : -0.08f), 0.3, V(180, 0, 0))
                .Gem(V(0, -0.5, 0), V(0.035, 0.045, 0.035), Palette.Teal, glow: true);
        }

        return m.Anchor("mouth", V(0, 0.9, -0.55)).Anchor("eye", V(0, 1.05, -0.35)).Build();
    }

    // ---- Warden: a tall guardian in plate, with a burning core ---------------------------------

    private static LowPolyModel BuildWarden()
    {
        var m = new ModelBuilder("warden");

        m.Part("torso", V(0, 1.25, 0))
            .Box(V(0, 0.35, 0), V(0.78, 0.6, 0.46), Palette.Plate)
            .Box(V(0, 0.35, -0.26), V(0.6, 0.5, 0.08), Palette.Crimson)
            .Box(V(0, -0.1, 0), V(0.5, 0.3, 0.34), Palette.PlateDark)
            .Box(V(0, -0.02, -0.02), V(0.54, 0.06, 0.37), Palette.Gold)
            .Box(V(0, 0.4, 0.3), V(0.7, 0.7, 0.1), Palette.PlateDark)
            .Prism(V(-0.22, 0.6, 0.38), 0.06, 0.2, 4, Palette.Gold, 0, V(90, 0, 0), spin: 45)
            .Prism(V(0, 0.6, 0.38), 0.07, 0.24, 4, Palette.Gold, 0, V(90, 0, 0), spin: 45)
            .Prism(V(0.22, 0.6, 0.38), 0.06, 0.2, 4, Palette.Gold, 0, V(90, 0, 0), spin: 45)
            .Gem(V(0, 0.4, -0.32), V(0.1, 0.11, 0.06), Palette.Hot, glow: true);

        m.Part("head", V(0, 1.95, -0.02), "torso")
            .Box(V(0, 0.12, 0), V(0.34, 0.34, 0.36), Palette.Plate)
            .Wedge(V(0, 0.32, 0.02), V(0.08, 0.12, 0.3), Palette.Crimson)
            .Box(V(0, 0.12, -0.185), V(0.26, 0.05, 0.02), Palette.Hot, glow: true)
            .Prism(V(-0.2, 0.34, 0), 0.06, 0.3, 4, Palette.Bone, 0, V(0, 0, 38), spin: 45)
            .Prism(V(0.2, 0.34, 0), 0.06, 0.3, 4, Palette.Bone, 0, V(0, 0, -38), spin: 45);

        foreach (double side in new[] { -1.0, 1.0 })
        {
            string tag = side < 0 ? "l" : "r";
            m.Part($"arm_{tag}", V(0.5 * side, 1.8, 0), "torso")
                .Box(V(0, 0.02, 0), V(0.32, 0.2, 0.34), Palette.Crimson)
                .Prism(V(0.08 * side, 0.18, 0), 0.07, 0.22, 4, Palette.Gold, 0, V(0, 0, -12 * side), spin: 45)
                .Prism(V(-0.06 * side, 0.15, 0), 0.05, 0.16, 4, Palette.Gold, 0, V(0, 0, 12 * side), spin: 45)
                .Box(V(0, -0.3, 0), V(0.2, 0.5, 0.2), Palette.Plate)
                .Box(V(0, -0.78, -0.03), V(0.3, 0.34, 0.3), Palette.Steel)
                .Prism(V(-0.08, -0.98, -0.08), 0.04, 0.12, 4, Palette.Gold, 0, V(180, 0, 0), spin: 45)
                .Prism(V(0.08, -0.98, -0.08), 0.04, 0.12, 4, Palette.Gold, 0, V(180, 0, 0), spin: 45);

            m.Part($"leg_{tag}", V(0.2 * side, 1.07, 0))
                .Box(V(0, -0.3, 0), V(0.26, 0.55, 0.28), Palette.Plate)
                .Box(V(0, -0.78, 0), V(0.24, 0.5, 0.26), Palette.PlateDark)
                .Wedge(V(0, -1.0, -0.08), V(0.28, 0.14, 0.42), Palette.Crimson, V(0, 180, 0));
        }

        return m.Anchor("core", V(0, 1.65, -0.35)).Anchor("eye", V(0, 2.07, -0.2)).Build();
    }

    // ---- projectiles -------------------------------------------------------------------------------

    private static LowPolyModel BuildOrb(string id, ModelColor outer, ModelColor inner)
    {
        var m = new ModelBuilder(id);
        m.Part("orb", V(0, 0, 0))
            .Gem(V(0, 0, 0), V(0.24, 0.24, 0.24), outer, glow: true)
            .Gem(V(0, 0, 0), V(0.14, 0.14, 0.14), inner, V(45, 45, 0), glow: true);
        return m.Build();
    }

    // ---- weapons (first person: barrel along -Z, grip below the origin) --------------------------

    private static LowPolyModel BuildRevolver()
    {
        var m = new ModelBuilder("revolver");

        m.Part("body", V(0, 0, 0))
            .Box(V(0, 0.02, -0.05), V(0.05, 0.08, 0.18), Palette.Steel)
            .Box(V(0, 0.045, -0.2), V(0.035, 0.035, 0.2), Palette.SteelLight)
            .Box(V(0, 0.015, -0.19), V(0.03, 0.03, 0.16), Palette.Steel)
            .Box(V(0, 0.045, -0.1), V(0.046, 0.046, 0.02), Palette.Gold)
            .Box(V(0, 0.075, -0.29), V(0.012, 0.016, 0.012), Palette.SteelLight)
            .Box(V(0, -0.08, 0.06), V(0.045, 0.16, 0.07), Palette.Wood, V(-18, 0, 0))
            .Box(V(0, -0.03, 0.0), V(0.012, 0.05, 0.1), Palette.Steel, V(0, 0, 0));

        m.Part("cylinder", V(0, 0.02, -0.04))
            .Prism(V(0, 0, 0), 0.04, 0.09, 6, Palette.Steel.Shade(0.1f), 1, V(90, 0, 0))
            .Prism(V(0, 0, 0), 0.043, 0.012, 6, Palette.Gold, 1, V(90, 0, 0));

        m.Part("hammer", V(0, 0.06, 0.04))
            .Box(V(0, 0.025, 0.012), V(0.016, 0.06, 0.03), Palette.SteelLight, V(-25, 0, 0));

        m.Part("gem", V(0, 0.075, -0.015))
            .Gem(V(0, 0, 0), V(0.014, 0.014, 0.014), Palette.Teal, glow: true);

        return m.Anchor("muzzle", V(0, 0.045, -0.31)).Build();
    }

    private static LowPolyModel BuildShotgun()
    {
        var m = new ModelBuilder("shotgun");

        m.Part("body", V(0, 0, 0))
            .Prism(V(-0.024, 0.04, -0.17), 0.024, 0.32, 8, Palette.Steel, 1, V(90, 0, 0))
            .Prism(V(0.024, 0.04, -0.17), 0.024, 0.32, 8, Palette.Steel, 1, V(90, 0, 0))
            .Box(V(0, 0.064, -0.17), V(0.02, 0.012, 0.3), Palette.SteelLight)
            .Box(V(0, 0.03, -0.0), V(0.1, 0.08, 0.1), Palette.Gold)
            .Box(V(0, -0.005, -0.15), V(0.07, 0.04, 0.16), Palette.Wood)
            .Box(V(0, -0.06, 0.05), V(0.05, 0.14, 0.07), Palette.Wood, V(-16, 0, 0))
            .Box(V(0, 0.015, 0.11), V(0.045, 0.06, 0.1), Palette.Wood, V(8, 0, 0));

        m.Part("gem", V(0, 0.085, -0.02))
            .Gem(V(0, 0, 0), V(0.016, 0.016, 0.016), Palette.VioletGlow, glow: true);

        return m.Anchor("muzzle", V(0, 0.04, -0.34)).Build();
    }

    // ---- dungeon props -----------------------------------------------------------------------------

    private static LowPolyModel BuildChest()
    {
        var m = new ModelBuilder("chest");
        m.Part("base", V(0, 0, 0))
            .Box(V(0, 0.17, 0), V(0.62, 0.34, 0.4), Palette.Wood)
            .Box(V(-0.2, 0.17, 0), V(0.06, 0.36, 0.42), Palette.Gold)
            .Box(V(0.2, 0.17, 0), V(0.06, 0.36, 0.42), Palette.Gold);
        m.Part("lid", V(0, 0.34, 0.2))
            .Box(V(0, 0.08, -0.2), V(0.62, 0.16, 0.4), Palette.Wood.Shade(0.1f))
            .Wedge(V(0, 0.2, -0.2), V(0.62, 0.1, 0.4), Palette.Wood.Shade(0.2f), V(0, 180, 0))
            .Box(V(-0.2, 0.1, -0.2), V(0.06, 0.2, 0.42), Palette.Gold)
            .Box(V(0.2, 0.1, -0.2), V(0.06, 0.2, 0.42), Palette.Gold)
            .Box(V(0, 0.0, -0.41), V(0.1, 0.12, 0.04), Palette.Gold)
            .Gem(V(0, 0.0, -0.44), V(0.03, 0.04, 0.02), Palette.EyeAmber, glow: true);
        return m.Anchor("open", V(0, 0.4, -0.1)).Build();
    }

    private static LowPolyModel BuildKey()
    {
        var m = new ModelBuilder("key");
        m.Part("key", V(0, 0.35, 0))
            .Prism(V(0, 0.16, 0), 0.1, 0.03, 8, Palette.Gold, 1, V(90, 0, 0))
            .Prism(V(0, 0.16, 0), 0.04, 0.034, 8, Palette.PlateDark, 1, V(90, 0, 0))
            .Box(V(0, -0.05, 0), V(0.035, 0.32, 0.03), Palette.Gold)
            .Box(V(0.04, -0.14, 0), V(0.06, 0.04, 0.03), Palette.Gold)
            .Box(V(0.04, -0.07, 0), V(0.06, 0.04, 0.03), Palette.Gold)
            .Gem(V(0, 0.16, 0), V(0.012, 0.012, 0.012), Palette.EyeAmber, glow: true);
        return m.Build();
    }

    private static LowPolyModel BuildSpikeTrap()
    {
        var m = new ModelBuilder("spike_trap");
        m.Part("plate", V(0, 0, 0))
            .Box(V(0, 0.025, 0), V(0.9, 0.05, 0.9), Palette.Steel)
            .Box(V(0, 0.055, 0), V(0.84, 0.02, 0.84), Palette.PlateDark);
        var spikes = m.Part("spikes", V(0, 0.05, 0));
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
                spikes.Prism(V(x * 0.27, 0.15, z * 0.27), 0.07, 0.3, 4, (x + z) % 2 == 0 ? Palette.SteelLight : Palette.Bone, 0, spin: 45);
        return m.Build();
    }

    private static LowPolyModel BuildTorch()
    {
        var m = new ModelBuilder("torch");
        m.Part("bracket", V(0, 0, 0))
            .Box(V(0, 0, 0.04), V(0.14, 0.22, 0.04), Palette.Steel)
            .Box(V(0, -0.04, -0.03), V(0.05, 0.05, 0.14), Palette.Steel)
            .Prism(V(0, 0.1, -0.1), 0.035, 0.34, 6, Palette.Wood, 0.8, V(-12, 0, 0))
            .Prism(V(0, 0.28, -0.115), 0.05, 0.06, 6, Palette.Steel, 1.3, V(-12, 0, 0));
        m.Part("flame", V(0, 0.34, -0.12))
            .Gem(V(0, 0.1, 0), V(0.07, 0.15, 0.07), Palette.Ember, glow: true)
            .Gem(V(0, 0.07, 0), V(0.04, 0.09, 0.04), Palette.EyeAmber, V(0, 45, 0), glow: true);
        return m.Anchor("flame", V(0, 0.46, -0.12)).Build();
    }

    // ---- loot pickups: each kind has its own silhouette and glow colour, centred about 0.15 above the model origin ----------

    private static LowPolyModel BuildLootHealth()
    {
        // A blood vial: a glowing red body, a bone neck and a cork, banded in gold.
        var m = new ModelBuilder("loot_health");
        m.Part("vial", V(0, 0, 0))
            .Prism(V(0, 0.13, 0), 0.085, 0.2, 6, Palette.Hot, 1, glow: true)
            .Prism(V(0, 0.13, 0), 0.092, 0.035, 6, Palette.Gold)
            .Prism(V(0, 0.27, 0), 0.04, 0.09, 6, Palette.Bone)
            .Prism(V(0, 0.33, 0), 0.05, 0.035, 6, Palette.Wood);
        return m.Build();
    }

    private static LowPolyModel BuildLootCleanse()
    {
        // A teal drop over a bone dish: clean water against blood.
        var m = new ModelBuilder("loot_cleanse");
        m.Part("drop", V(0, 0, 0))
            .Gem(V(0, 0.2, 0), V(0.08, 0.15, 0.08), Palette.Teal, glow: true)
            .Gem(V(0, 0.2, 0), V(0.04, 0.08, 0.04), Palette.White, V(0, 45, 0), glow: true)
            .Prism(V(0, 0.02, 0), 0.12, 0.04, 6, Palette.Bone, 0.7);
        return m.Build();
    }

    private static LowPolyModel BuildLootStyle()
    {
        // A four-pointed gold star standing in a cradle: the flashiest of the lot.
        var m = new ModelBuilder("loot_style");
        m.Part("star", V(0, 0.2, 0))
            .Gem(V(0, 0, 0), V(0.07, 0.17, 0.07), Palette.EyeAmber, glow: true)
            .Gem(V(0, 0, 0), V(0.17, 0.07, 0.07), Palette.EyeAmber, glow: true)
            .Gem(V(0, 0, 0), V(0.05, 0.05, 0.05), Palette.White, V(0, 45, 0), glow: true);
        m.Part("cradle", V(0, 0, 0))
            .Prism(V(0, 0.025, 0), 0.1, 0.05, 4, Palette.Gold, 0.6, spin: 45);
        return m.Build();
    }

    private static LowPolyModel BuildLootBloodlust()
    {
        // A fang of red crystal on a crimson base, leaning into the fight.
        var m = new ModelBuilder("loot_bloodlust");
        m.Part("fang", V(0, 0, 0))
            .Gem(V(0, 0.22, 0), V(0.07, 0.19, 0.07), Palette.Hot, V(0, 0, 12), glow: true)
            .Gem(V(0.09, 0.13, 0), V(0.04, 0.1, 0.04), Palette.Ember, V(0, 0, -25), glow: true)
            .Gem(V(-0.08, 0.12, 0.02), V(0.035, 0.09, 0.035), Palette.Ember, V(0, 0, 28), glow: true)
            .Prism(V(0, 0.025, 0), 0.11, 0.05, 6, Palette.Crimson, 0.75);
        return m.Build();
    }

    private static LowPolyModel BuildLootParry()
    {
        // A violet shield disc with a bright boss: the same colour as the parry flash, set in a steel rim.
        var m = new ModelBuilder("loot_parry");
        m.Part("shield", V(0, 0.2, 0))
            .Prism(V(0, 0, 0), 0.17, 0.035, 8, Palette.Steel, 1, V(90, 0, 0))
            .Prism(V(0, 0, -0.012), 0.12, 0.03, 8, Palette.VioletGlow, 1, V(90, 0, 0), glow: true)
            .Gem(V(0, 0, -0.03), V(0.05, 0.05, 0.04), Palette.White, glow: true);
        return m.Build();
    }
}
