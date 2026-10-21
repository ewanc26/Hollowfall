using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>
/// The dungeon's furniture, drawn from the low-poly catalogue: chests where loot spawns, keys that turn in the air, spike
/// traps in trap rooms, and wall torches whose flames glow. Real lights are costly, so only the few torches nearest the
/// player cast any; the rest rely on their glowing flames.
/// </summary>
public partial class PropsView : Node3D
{
    private const int MaxLights = 6;
    private const double LightRadius = 24;
    private const double LootSeconds = 2.0;

    private readonly List<(int Index, Node3D Node, Vector3 Home, double Phase)> _keys = [];
    private readonly List<(ChestState Chest, ModelRig Rig)> _chests = [];
    private readonly List<(int Index, ModelRig Rig)> _traps = [];
    private readonly HashSet<int> _released = [];
    private readonly List<(Node3D Node, Vector3 From, double Age, double Phase)> _loot = [];
    private Interactables _items = null!;
    private readonly List<(ModelRig Rig, Vector3 Position, double Phase)> _torches = [];
    private readonly List<OmniLight3D> _lights = [];
    private readonly List<(ModelRig Rig, Vector3 Position, double Phase)> _pits = [];
    private double _time;

    /// <summary>Called every frame with how loud the fire's crackle should be, 0 to 1, by the distance to the nearest fire pit (the host plays the loop).</summary>
    public Action<float>? CrackleLevel { get; set; }

    /// <summary>How many fire pits are drawn, and how many real lights are lit this frame (for tests).</summary>
    public int PitCount => _pits.Count;

    public int LightsOn => _lights.Count(l => l.Visible);

    public IReadOnlyList<Vector3> PitPositions => [.. _pits.Select(p => p.Position)];

    public void Initialize(DungeonSite site, Interactables items, IReadOnlyList<FirePitSpot>? pits = null)
    {
        _items = items;
        for (int index = 0; index < site.Spawns.Count; index++)
        {
            var spawn = site.Spawns[index];
            var at = new Vector3(spawn.X + 0.5f, spawn.Y, spawn.Z + 0.5f);
            switch (spawn.Kind)
            {
                case SpawnKind.Loot:
                    {
                        var chest = ModelFactory.Build(ModelCatalog.Chest);
                        chest.Root.Position = at;
                        // A deterministic turn so chests do not all face the same way.
                        chest.Root.RotationDegrees = new Vector3(0, ((spawn.X * 7 + spawn.Z * 13) & 3) * 90, 0);
                        AddChild(chest.Root);
                        _chests.Add((_items.Chests.First(c => c.Index == index), chest));
                        break;
                    }
                case SpawnKind.Key:
                    {
                        var key = ModelFactory.Build(ModelCatalog.Key, castShadows: false);
                        key.Root.Position = at;
                        AddChild(key.Root);
                        _keys.Add((index, key.Root, at, spawn.X * 0.37 + spawn.Z));
                        break;
                    }
                case SpawnKind.Trap:
                    {
                        var trap = ModelFactory.Build(ModelCatalog.SpikeTrap);
                        trap.Root.Position = at;
                        AddChild(trap.Root);
                        break;
                    }
            }
        }

        foreach (var t in site.Torches)
        {
            var torch = ModelFactory.Build(ModelCatalog.Torch, castShadows: false);
            var position = new Vector3((float)t.X, (float)t.Y, (float)t.Z);
            torch.Root.Position = position;
            // The model's back is +Z and its flame leans toward -Z, so -Z must point into the room.
            torch.Root.Rotation = new Vector3(0, (float)Math.Atan2(-t.FaceX, -t.FaceZ), 0);
            AddChild(torch.Root);
            _torches.Add((torch, position, t.CellX * 0.91 + t.CellZ * 1.3));
        }

        // Fire pits: a ring of stones, logs and a flame of glowing gems. They are warmth and decoration; nothing in them hurts.
        foreach (var pit in pits ?? [])
        {
            var rig = ModelFactory.Build(ModelCatalog.FirePit, castShadows: false);
            var position = new Vector3((float)pit.X, (float)pit.Y, (float)pit.Z);
            rig.Root.Position = position;
            rig.Root.RotationDegrees = new Vector3(0, (pit.CellX * 37 + pit.CellZ * 11) % 360, 0);
            AddChild(rig.Root);
            _pits.Add((rig, position, pit.CellX * 0.77 + pit.CellZ * 1.9));
        }

        for (int i = 0; i < MaxLights; i++)
        {
            var light = new OmniLight3D { LightColor = new Color(1f, 0.62f, 0.28f), OmniRange = 9, OmniAttenuation = 1.4f, ShadowEnabled = false, Visible = false };
            AddChild(light);
            _lights.Add(light);
        }
    }

    /// <summary>Opening a chest lets its loot out: the pickup rises from the lid, turns, and shrinks away as its effect lands.</summary>
    private void ReleaseLoot(double delta)
    {
        foreach (var (chest, _) in _chests)
        {
            if (!chest.Opened || !_released.Add(chest.Index)) continue;
            var model = ModelFactory.Build(ModelCatalog.ForLoot(chest.Loot.Kind), castShadows: false);
            var from = new Vector3(chest.Spawn.X + 0.5f, chest.Spawn.Y + 0.4f, chest.Spawn.Z + 0.5f);
            model.Root.Position = from;
            model.Root.Scale = Vector3.One * 0.01f;
            AddChild(model.Root);
            _loot.Add((model.Root, from, 0, chest.Spawn.X * 0.37 + chest.Spawn.Z));
        }

        for (int i = _loot.Count - 1; i >= 0; i--)
        {
            var (node, from, age, phase) = _loot[i];
            age += delta;
            if (age >= LootSeconds)
            {
                node.QueueFree();
                _loot.RemoveAt(i);
                continue;
            }
            double t = age / LootSeconds;
            double rise = 1 - (1 - t) * (1 - t);
            float scale = (float)Math.Min(Math.Min(1, age / 0.25), (LootSeconds - age) / 0.45);
            node.Position = from + new Vector3(0, (float)(rise + Math.Sin(age * 5 + phase) * 0.04), 0);
            node.RotationDegrees = new Vector3(0, (float)(age * 220 + phase * 40) % 360, 0);
            node.Scale = Vector3.One * (1.5f * Math.Max(0.01f, scale));
            _loot[i] = (node, from, age, phase);
        }
    }

    /// <summary>Flickers the flames and hands the real lights to the torches nearest <paramref name="viewer"/>.</summary>
    public void Update(double delta, Vector3 viewer)
    {
        _time += delta;
        foreach (var (index, node, home, phase) in _keys)
        {
            node.Visible = !_items.KeyTaken(index);
            node.RotationDegrees = new Vector3(0, (float)(_time * 90 + phase * 40) % 360, 0);
            node.Position = home + new Vector3(0, (float)(0.15 + Math.Sin(_time * 2 + phase) * 0.06), 0);
        }

        foreach (var (chest, rig) in _chests)
            if (rig.Part("lid") is { } lid)
            {
                float target = chest.Opened ? 105 : 0;
                float next = Mathf.MoveToward(lid.RotationDegrees.X, target, (float)(delta * 330));
                lid.RotationDegrees = new Vector3(next, 0, 0);
            }
        ReleaseLoot(delta);
        foreach (var (index, rig) in _traps)
            if (rig.Part("spikes") is { } spikes)
            {
                double ext = _items.SpikeExtension(index);
                spikes.Scale = new Vector3(1, (float)Math.Max(0.12, ext), 1);
            }

        // Torches and fire pits share the few real lights: the nearest ones win. A pit is warmer, wider and a little brighter.
        var near = new List<(double Distance, Vector3 Position, double Phase, bool Pit)>();
        for (int i = 0; i < _torches.Count; i++)
        {
            var (rig, position, phase) = _torches[i];
            double flicker = 0.85 + 0.15 * Math.Sin(_time * 11 + phase) * Math.Sin(_time * 7.3 + phase * 2);
            if (rig.Part("flame") is { } flame) flame.Scale = new Vector3(1, (float)(0.9 + 0.2 * flicker), 1);
            double d = position.DistanceTo(viewer);
            if (d < LightRadius) near.Add((d, position + new Vector3(0, 0.45f, 0), phase, false));
        }

        double nearestPit = double.MaxValue;
        foreach (var (rig, position, phase) in _pits)
        {
            double flicker = 0.8 + 0.2 * Math.Sin(_time * 9.1 + phase) * Math.Sin(_time * 5.3 + phase * 1.7) + 0.06 * Math.Sin(_time * 23 + phase);
            if (rig.Part("flame") is { } flame)
            {
                flame.Scale = new Vector3(1 - 0.06f * (float)flicker, (float)(0.8 + 0.4 * flicker), 1 - 0.06f * (float)flicker);
                flame.RotationDegrees = new Vector3((float)(4 * Math.Sin(_time * 3.1 + phase)), 0, (float)(5 * Math.Sin(_time * 2.3 + phase * 2)));
            }
            if (rig.Part("tongue_a") is { } a)
            {
                a.Scale = new Vector3(1, (float)(0.7 + 0.5 * Math.Max(0, Math.Sin(_time * 7.7 + phase))), 1);
                a.RotationDegrees = new Vector3(0, 0, (float)(10 * Math.Sin(_time * 4.1 + phase)));
            }
            if (rig.Part("tongue_b") is { } t)
            {
                t.Scale = new Vector3(1, (float)(0.7 + 0.5 * Math.Max(0, Math.Sin(_time * 6.3 + phase * 1.3 + 1))), 1);
                t.RotationDegrees = new Vector3(0, 0, (float)(-10 * Math.Sin(_time * 3.3 + phase)));
            }
            rig.SetGlow("embers", new Color(1, (float)(0.6 + 0.4 * flicker), (float)(0.5 + 0.3 * flicker)));
            double d = position.DistanceTo(viewer);
            nearestPit = Math.Min(nearestPit, d);
            if (d < LightRadius) near.Add((d - 3, position + new Vector3(0, 0.5f, 0), phase, true)); // -3: a pit wins a tie against a torch
        }
        CrackleLevel?.Invoke(nearestPit >= 18 ? 0 : (float)Math.Pow(Math.Clamp(1 - (nearestPit - 2) / 16, 0, 1), 1.6));

        near.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        for (int i = 0; i < _lights.Count; i++)
        {
            if (i >= near.Count)
            {
                _lights[i].Visible = false;
                continue;
            }
            var (_, position, phase, pit) = near[i];
            var light = _lights[i];
            light.Visible = true;
            light.GlobalPosition = position;
            light.OmniRange = pit ? 12 : 9;
            light.LightColor = pit ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.62f, 0.28f);
            light.LightEnergy = pit
                ? (float)(1.9 + 0.35 * Math.Sin(_time * 8 + phase) * Math.Sin(_time * 5.1 + phase) + 0.12 * Math.Sin(_time * 21 + phase))
                : (float)(1.1 + 0.25 * Math.Sin(_time * 9 + phase));
        }
    }
}
