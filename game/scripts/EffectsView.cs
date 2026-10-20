using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Effects;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// Draws the combat effects: chunky coloured shards that fly from a killed enemy and bounce on the floor, and bright flecks
/// where a shot lands. What flies and how is decided in Core (<see cref="Gibs"/>, <see cref="ShardField"/>); this node only draws
/// the live shards, as two MultiMeshes (one draw call each) over fixed-size pools, so effects never allocate nodes or arrays
/// while the game runs. There is no blood: shards take the enemy's own palette. It uses the default process mode, so it
/// freezes under the pause menu.
/// </summary>
public partial class EffectsView : Node3D
{
    public const int GibCapacity = 192;
    public const int SparkCapacity = 96;

    private readonly ShardField _gibs = new(GibCapacity);
    private readonly ShardField _sparks = new(SparkCapacity);
    private MultiMesh _gibMesh = null!, _sparkMesh = null!;
    private Func<int, int, int, BlockType> _blockAt = static (_, _, _) => BlockType.Air;
    private bool _gibsDrawn, _sparksDrawn;
    private int _bursts;

    /// <summary>How many shards are flying or lying about, for tests and the showcase.</summary>
    public int Live => _gibs.AliveCount + _sparks.AliveCount;

    /// <summary>Gives the shards a world to bounce on.</summary>
    public void Initialize(Func<int, int, int, BlockType> blockAt) => _blockAt = blockAt;

    public override void _Ready()
    {
        // Shards are chunky triangular prisms, lit and flat shaded like the models; sparks are small unlit cubes.
        _gibMesh = MakeMultiMesh(new PrismMesh { Size = new Vector3(1.3f, 0.8f, 0.9f) }, GibCapacity, unshaded: false);
        _sparkMesh = MakeMultiMesh(new BoxMesh { Size = Vector3.One }, SparkCapacity, unshaded: true);
        AddChild(new MultiMeshInstance3D { Name = "Gibs", Multimesh = _gibMesh, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        AddChild(new MultiMeshInstance3D { Name = "Sparks", Multimesh = _sparkMesh, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private static MultiMesh MakeMultiMesh(Mesh mesh, int capacity, bool unshaded)
    {
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            Roughness = 0.85f,
        });
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = capacity,
            VisibleInstanceCount = capacity,
            CustomAabb = new Aabb(new Vector3(-4096, -256, -4096), new Vector3(8192, 1024, 8192)), // effects are never culled by a stale box
        };
        var hidden = new Transform3D(Basis.FromScale(Vector3.Zero), Vector3.Zero);
        for (int i = 0; i < capacity; i++) mm.SetInstanceTransform(i, hidden);
        return mm;
    }

    /// <summary>Reacts to a combat event: a hit throws sparks from where it landed. Kills are handled by <see cref="Gib"/>, which needs the enemy's place.</summary>
    public void OnEvent(CombatEvent e)
    {
        if (e is EnemyHit h) Sparks(h.Point.X, h.Point.Y, h.Point.Z, h.Zone, h.Killed, h.Id);
    }

    /// <summary>Breaks a killed enemy standing at <paramref name="feet"/> into shards.</summary>
    public void Gib(EnemyKind kind, Vector3 feet, int id) =>
        Gibs.Burst(_gibs, kind, feet.X, feet.Y, feet.Z, Seed(id));

    public void Sparks(double x, double y, double z, HitZone zone, bool killed, int id) =>
        Gibs.Sparks(_sparks, x, y, z, zone, killed, Seed(id));

    private long Seed(int id) => id * 1_000_003L + _bursts++;

    public override void _Process(double delta)
    {
        _gibs.Step((float)delta, _blockAt);
        _sparks.Step((float)delta, _blockAt);
        _gibsDrawn = Draw(_gibs, _gibMesh, _gibsDrawn, new Vector3(1f, 0.7f, 1.2f));
        _sparksDrawn = Draw(_sparks, _sparkMesh, _sparksDrawn, new Vector3(1f, 1f, 2.4f));
    }

    /// <summary>Copies the live shards into a MultiMesh. Returns whether anything was drawn, so an idle pool costs nothing.</summary>
    private static bool Draw(ShardField field, MultiMesh mesh, bool drewLast, Vector3 stretch)
    {
        var items = field.Items;
        bool any = false;
        var hidden = new Transform3D(Basis.FromScale(Vector3.Zero), Vector3.Zero);
        for (int i = 0; i < items.Length; i++)
        {
            ref readonly var s = ref items[i];
            if (!s.Alive)
            {
                if (drewLast) mesh.SetInstanceTransform(i, hidden);
                continue;
            }
            any = true;
            float size = s.Size * s.Scale;
            var basis = Basis.FromEuler(new Vector3(s.Rx, s.Ry, s.Rz)).Scaled(stretch * size);
            mesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3(s.X, s.Y, s.Z)));
            mesh.SetInstanceColor(i, new Color(s.R, s.G, s.B));
        }
        // The pass that finds nothing alive still hid the slots that just emptied (drewLast), so the next idle frame can skip the work.
        return any;
    }
}
