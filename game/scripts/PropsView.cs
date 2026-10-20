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

    private readonly List<(int Index, Node3D Node, Vector3 Home, double Phase)> _keys = [];
    private readonly List<(ChestState Chest, ModelRig Rig)> _chests = [];
    private readonly List<(int Index, ModelRig Rig)> _traps = [];
    private Interactables _items = null!;
    private readonly List<(ModelRig Rig, Vector3 Position, double Phase)> _torches = [];
    private readonly List<OmniLight3D> _lights = [];
    private double _time;

    public void Initialize(DungeonSite site, Interactables items)
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

        for (int i = 0; i < MaxLights; i++)
        {
            var light = new OmniLight3D { LightColor = new Color(1f, 0.62f, 0.28f), OmniRange = 9, OmniAttenuation = 1.4f, ShadowEnabled = false, Visible = false };
            AddChild(light);
            _lights.Add(light);
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
        foreach (var (index, rig) in _traps)
            if (rig.Part("spikes") is { } spikes)
            {
                double ext = _items.SpikeExtension(index);
                spikes.Scale = new Vector3(1, (float)Math.Max(0.12, ext), 1);
            }

        var near = new List<(double Distance, int Index)>();
        for (int i = 0; i < _torches.Count; i++)
        {
            var (rig, position, phase) = _torches[i];
            double flicker = 0.85 + 0.15 * Math.Sin(_time * 11 + phase) * Math.Sin(_time * 7.3 + phase * 2);
            if (rig.Part("flame") is { } flame) flame.Scale = new Vector3(1, (float)(0.9 + 0.2 * flicker), 1);
            double d = position.DistanceTo(viewer);
            if (d < LightRadius) near.Add((d, i));
        }
        near.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        for (int i = 0; i < _lights.Count; i++)
        {
            if (i >= near.Count)
            {
                _lights[i].Visible = false;
                continue;
            }
            var (_, position, phase) = _torches[near[i].Index];
            _lights[i].Visible = true;
            _lights[i].GlobalPosition = position + new Vector3(0, 0.45f, 0);
            _lights[i].LightEnergy = (float)(1.1 + 0.25 * Math.Sin(_time * 9 + phase));
        }
    }
}
