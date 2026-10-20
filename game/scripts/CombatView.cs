using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Models;
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

    private readonly Dictionary<int, EnemyView> _enemies = [];
    private readonly Dictionary<int, ProjectileView> _projectiles = [];
    private Vector3 _playerPosition;
    private readonly Dictionary<PortalColor, PortalView> _portals = [];
    private readonly List<Tracer> _tracers = [];
    private WorldView _world = null!;
    private EffectsView _effects = null!;

    public CombatSim Sim { get; private set; } = null!;
    public DungeonSite Site { get; private set; } = null!;
    public IReadOnlyList<CombatEvent> LastEvents { get; private set; } = [];

    public event Action<CombatEvent>? EventRaised;

    public void Initialize(CombatSim sim, DungeonSite site, WorldView world)
    {
        Sim = sim;
        Site = site;
        _world = world;
        _effects = new EffectsView { Name = "Effects" };
        _effects.Initialize(world.World.GetBlock);
        AddChild(_effects);
    }

    private static StandardMaterial3D Solid(Color c) => new() { AlbedoColor = c, Roughness = 0.8f };

    /// <summary>Advances the simulation one fixed step and refreshes the visuals.</summary>
    public void Step(double delta, PlayerView view, PlayerActions actions)
    {
        _playerPosition = new Vector3((float)view.Feet.X, (float)view.Feet.Y, (float)view.Feet.Z);
        Sim.Tick(delta, view, actions);

        var events = Sim.DrainEvents();
        LastEvents = events;
        foreach (var e in events) Handle(e);
        foreach (var (cx, cz) in Sim.TakeDirtyChunks()) _world.MarkDirty(cx, cz);

        SyncEnemies(delta);
        SyncProjectiles(delta);
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

    /// <summary>Where portal openings are on screen for this camera, so the HUD can stay off them.</summary>
    public List<Rect2> PortalScreenRects(Camera3D viewer)
    {
        var rects = new List<Rect2>();
        foreach (var pv in _portals.Values)
            if (Sim.Portals.Linked && pv.ScreenRect(viewer) is { } rect) rects.Add(rect);
        return rects;
    }

    /// <summary>Updates what each portal shows. Call once per rendered frame with the viewer's camera.</summary>
    public void UpdatePortalViews(Camera3D viewer)
    {
        foreach (var pv in _portals.Values) pv.UpdateView(Sim.Portals.Other(pv.Portal), viewer);
    }

    private void Handle(CombatEvent e)
    {
        _effects.OnEvent(e);
        switch (e)
        {
            case ShotFired shot when shot.Path != null:
                foreach (var (from, to) in shot.Path) AddTracer(from, to, shot.Weapon == "shotgun" ? new Color(1f, 0.7f, 0.3f) : new Color(1f, 1f, 0.6f));
                break;
            case PortalPlaced { Success: true } placed:
                RefreshPortals();
                break;
            case PortalClosed closed:
                RefreshPortals();
                AddChild(SeverEffect.ForPortal(closed));
                break;
            case EnemySevered severed:
                AddChild(SeverEffect.ForEnemy(severed));
                break;
            case EnemyKilled killed when _enemies.TryGetValue(killed.Id, out var node):
                _effects.Gib(killed.Kind, node.Position, killed.Id);
                Collapse(node);
                _enemies.Remove(killed.Id);
                break;
        }
        EventRaised?.Invoke(e);
    }

    /// <summary>Makes the portal views match the sim's portals: new ones appear, replaced or cleared ones go.</summary>
    public void RefreshPortals()
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

    /// <summary>A killed enemy bursts into shards (<see cref="EffectsView"/>) as the body slumps, shrinks into the floor and disappears.</summary>
    private void Collapse(EnemyView view)
    {
        view.ForcePose("stunned");
        var tween = CreateTween();
        tween.SetParallel();
        tween.TweenProperty(view, "scale", new Vector3(1.15f, 0.05f, 1.15f), 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(view, "position:y", view.Position.Y - 0.25f, 0.3);
        tween.Chain().TweenCallback(Callable.From(view.QueueFree));
    }

    private void SyncEnemies(double delta)
    {
        var alive = new HashSet<int>();
        foreach (var e in Sim.Enemies)
        {
            alive.Add(e.Id);
            if (!_enemies.TryGetValue(e.Id, out var view))
            {
                view = new EnemyView(e) { Name = $"Enemy_{e.Id}" };
                AddChild(view);
                _enemies[e.Id] = view;
            }
            view.Follow(e, _playerPosition, delta);
        }

        foreach (var id in _enemies.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _enemies[id].QueueFree();
            _enemies.Remove(id);
        }
    }

    private void SyncProjectiles(double delta)
    {
        var alive = new HashSet<int>();
        foreach (var p in Sim.Projectiles)
        {
            alive.Add(p.Id);
            if (!_projectiles.TryGetValue(p.Id, out var view))
            {
                view = new ProjectileView { Name = $"Projectile_{p.Id}" };
                AddChild(view);
                _projectiles[p.Id] = view;
            }
            view.Follow(new Vector3((float)p.Position.X, (float)p.Position.Y, (float)p.Position.Z), p.Reflected, delta);
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
