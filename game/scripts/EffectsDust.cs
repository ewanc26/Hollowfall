using Godot;

namespace Hollowfall.Game;

/// <summary>
/// Dust motes drifting in the dungeon's air, caught by the torchlight. A small CpuParticles3D box that follows the viewer while
/// its particles stay where they were released (<c>LocalCoords</c> off), so walking through the cloud looks like walking through
/// still air. Few, tiny and faint on purpose: it should be noticed only when it is missing. Hidden above ground.
/// </summary>
public partial class EffectsDust : Node3D
{
    public const int Motes = 70;
    public const float Lifetime = 10f;

    private CpuParticles3D _particles = null!;
    private bool _off = System.Environment.GetEnvironmentVariable("HOLLOWFALL_DUST") == "0";

    /// <summary>Where the cloud is centred: the viewer's eye.</summary>
    public Func<Vector3> Target { get; set; } = () => Vector3.Zero;

    /// <summary>Whether the dust shows (underground).</summary>
    public bool Active
    {
        get => Visible;
        set => Visible = value && !_off;
    }

    public override void _Ready()
    {
        // HOLLOWFALL_DUST=0 turns the motes off, for comparing screenshots.
        if (_off) { Visible = false; SetProcess(false); return; }

        var fade = new Gradient();
        fade.Offsets = [0f, 0.2f, 0.8f, 1f];
        fade.Colors = [new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.85f), new Color(1, 1, 1, 0.85f), new Color(1, 1, 1, 0)];

        _particles = new CpuParticles3D
        {
            Name = "Motes",
            Amount = Motes,
            Lifetime = Lifetime,
            Preprocess = Lifetime, // the air is already full of dust when the player arrives
            LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(6f, 2.2f, 6f),
            Direction = new Vector3(0.3f, 0.2f, 0.1f),
            Spread = 180f,
            InitialVelocityMin = 0.03f,
            InitialVelocityMax = 0.14f,
            Gravity = new Vector3(0f, -0.012f, 0f),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            ColorRamp = fade,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new QuadMesh
            {
                Size = new Vector2(0.06f, 0.06f),
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                    VertexColorUseAsAlbedo = true,
                    AlbedoColor = new Color(1f, 0.93f, 0.75f, 1f),
                    NoDepthTest = false,
                },
            },
        };
        AddChild(_particles);
    }

    public override void _Process(double delta) => GlobalPosition = Target();
}
