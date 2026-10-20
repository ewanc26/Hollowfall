using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Portals;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// Runs Core's combat simulation and draws what it produces: enemies, projectiles, shot tracers and
/// portals. All rules are in <see cref="CombatSim"/>; this only turns its state and events into nodes.
/// </summary>
public partial class CombatView : Node3D
{
    private sealed class Tracer
    {
        public MeshInstance3D Node = null!;
        public double Life;
    }

    private readonly Dictionary<int, MeshInstance3D> _enemies = [];
    private readonly Dictionary<int, MeshInstance3D> _projectiles = [];
    private readonly Dictionary<PortalColor, PortalView> _portals = [];
    private readonly List<Tracer> _tracers = [];
    private readonly Dictionary<EnemyKind, StandardMaterial3D> _kindMaterials = [];
    private StandardMaterial3D _telegraph = null!;
    private WorldView _world = null!;

    public CombatSim Sim { get; private set; } = null!;
    public DungeonSite Site { get; private set; } = null!;
    public IReadOnlyList<CombatEvent> LastEvents { get; private set; } = [];

    public event Action<CombatEvent>? EventRaised;

    public void Initialize(CombatSim sim, DungeonSite site, WorldView world)
    {
        Sim = sim;
        Site = site;
        _world = world;

        _kindMaterials[EnemyKind.Husk] = Solid(new Color(0.72f, 0.2f, 0.18f));
        _kindMaterials[EnemyKind.Spitter] = Solid(new Color(0.55f, 0.25f, 0.75f));
        _kindMaterials[EnemyKind.Warden] = Solid(new Color(0.25f, 0.05f, 0.08f));
        _telegraph = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.9f, 0.1f), EmissionEnabled = true, Emission = new Color(1f, 0.85f, 0f), EmissionEnergyMultiplier = 2.5f };
    }

    private static StandardMaterial3D Solid(Color c) => new() { AlbedoColor = c, Roughness = 0.8f };

    /// <summary>Advances the simulation one fixed step and refreshes the visuals.</summary>
    public void Step(double delta, PlayerView view, PlayerActions actions)
    {
        Sim.Tick(delta, view, actions);

        var events = Sim.DrainEvents();
        LastEvents = events;
        foreach (var e in events) Handle(e);
        foreach (var (cx, cz) in Sim.TakeDirtyChunks()) _world.MarkDirty(cx, cz);

        SyncEnemies();
        SyncProjectiles();
    }

    public override void _Process(double delta)
    {
        for (int i = _tracers.Count - 1; i >= 0; i--)
        {
            var t = _tracers[i];
            t.Life -= delta;
            if (t.Life <= 0)
            {
                t.Node.QueueFree();
                _tracers.RemoveAt(i);
            }
            else if (t.Node.MaterialOverride is StandardMaterial3D m)
            {
                m.AlbedoColor = new Color(m.AlbedoColor, (float)(t.Life / 0.15));
            }
        }
    }

    /// <summary>Updates what each portal shows. Call once per rendered frame with the viewer's camera.</summary>
    public void UpdatePortalViews(Camera3D viewer)
    {
        foreach (var pv in _portals.Values) pv.UpdateView(Sim.Portals.Other(pv.Portal), viewer);
    }

    private void Handle(CombatEvent e)
    {
        switch (e)
        {
            case ShotFired shot when shot.Path != null:
                foreach (var (from, to) in shot.Path) AddTracer(from, to, shot.Weapon == "shotgun" ? new Color(1f, 0.7f, 0.3f) : new Color(1f, 1f, 0.6f));
                break;
            case PortalPlaced { Success: true } placed:
                RefreshPortals();
                break;
            case EnemyKilled killed when _enemies.TryGetValue(killed.Id, out var node):
                node.QueueFree();
                _enemies.Remove(killed.Id);
                break;
        }
        EventRaised?.Invoke(e);
    }

    private void RefreshPortals()
    {
        foreach (var color in new[] { PortalColor.Teal, PortalColor.Violet })
        {
            var portal = Sim.Portals.Get(color);
            if (_portals.TryGetValue(color, out var existing) && existing.Portal == portal) continue;
            if (existing != null) { existing.QueueFree(); _portals.Remove(color); }
            if (portal == null) continue;

            var view = new PortalView(portal) { Name = $"Portal_{color}" };
            AddChild(view);
            _portals[color] = view;
        }
    }

    private void SyncEnemies()
    {
        var alive = new HashSet<int>();
        foreach (var e in Sim.Enemies)
        {
            alive.Add(e.Id);
            if (!_enemies.TryGetValue(e.Id, out var node))
            {
                node = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.6f, 1.8f, 0.6f) }, Name = $"Enemy_{e.Id}" };
                AddChild(node);
                _enemies[e.Id] = node;
            }
            node.Position = new Vector3((float)e.Feet.X, (float)e.Feet.Y + 0.9f, (float)e.Feet.Z);
            node.MaterialOverride = e.Telegraphing ? _telegraph : _kindMaterials[e.Kind];
            node.Scale = e.Kind == EnemyKind.Warden ? new Vector3(1.6f, 1.25f, 1.6f) : Vector3.One;
        }

        foreach (var id in _enemies.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _enemies[id].QueueFree();
            _enemies.Remove(id);
        }
    }

    private void SyncProjectiles()
    {
        var alive = new HashSet<int>();
        foreach (var p in Sim.Projectiles)
        {
            alive.Add(p.Id);
            if (!_projectiles.TryGetValue(p.Id, out var node))
            {
                node = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f }, Name = $"Projectile_{p.Id}" };
                AddChild(node);
                _projectiles[p.Id] = node;
            }
            node.Position = new Vector3((float)p.Position.X, (float)p.Position.Y, (float)p.Position.Z);
            node.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = p.Reflected ? new Color(0.3f, 0.9f, 1f) : new Color(1f, 0.45f, 0.1f),
                EmissionEnabled = true,
                Emission = p.Reflected ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.35f, 0f),
                EmissionEnergyMultiplier = 3f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
        }

        foreach (var id in _projectiles.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _projectiles[id].QueueFree();
            _projectiles.Remove(id);
        }
    }

    private void AddTracer(Vec3 from, Vec3 to, Color colour)
    {
        var a = new Vector3((float)from.X, (float)from.Y - 0.15f, (float)from.Z);
        var b = new Vector3((float)to.X, (float)to.Y, (float)to.Z);
        float length = a.DistanceTo(b);
        if (length < 0.05f) return;

        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.025f, 0.025f, length) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(node);
        node.GlobalPosition = (a + b) / 2;
        node.LookAt(b, Mathf.Abs((b - a).Normalized().Y) > 0.95f ? Vector3.Right : Vector3.Up);
        _tracers.Add(new Tracer { Node = node, Life = 0.15 });
    }
}
