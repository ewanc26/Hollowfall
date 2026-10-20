using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>
/// A lineup of every model on a plinth, for checking the art: HOLLOWFALL_SHOWCASE picks the page (enemies, poses, weapons,
/// props, effects). Run it with <c>godot --path game res://scenes/Showcase.tscn</c>, or record frames with the movie writer.
/// </summary>
public partial class ModelShowcase : Node3D
{
    private readonly List<(Node3D Node, double Speed)> _spin = [];
    private readonly List<EnemyView> _posed = [];
    private Camera3D _camera = null!;
    private double _time;
    private string _page = "enemies";

    public override void _Ready()
    {
        _page = System.Environment.GetEnvironmentVariable("HOLLOWFALL_SHOWCASE") ?? "enemies";

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.1f, 0.11f, 0.15f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.55f, 0.58f, 0.7f),
                AmbientLightEnergy = 0.55f,
            },
        });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, -30, 0), LightEnergy = 1.2f, ShadowEnabled = true });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 150, 0), LightEnergy = 0.45f });

        var floor = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(30, 0.2f, 14) },
            Position = new Vector3(0, -0.1f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.24f), Roughness = 1f },
            Name = "Floor",
        };
        AddChild(floor);

        _camera = new Camera3D { Fov = 50, Current = true };
        AddChild(_camera);

        switch (_page)
        {
            case "poses": BuildPoses(); break;
            case "weapons": BuildWeapons(); break;
            case "props": BuildProps(); break;
            case "effects": AddChild(new EffectsShowcase(_camera)); break;
            default: BuildEnemies(); break;
        }
    }

    private void Place(Node3D node, Vector3 at, string label, double spin = 0, float labelHeight = 0.2f)
    {
        node.Position = at;
        AddChild(node);
        if (spin != 0) _spin.Add((node, spin));
        var text = new Label3D { Text = label, PixelSize = 0.006f, Position = at + new Vector3(0, -0.05f, 1.2f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color(1f, 0.85f, 0.4f), NoDepthTest = true };
        AddChild(text);
        text.Position = new Vector3(at.X, labelHeight, at.Z + 1.3f);
    }

    private void BuildEnemies()
    {
        _camera.Position = new Vector3(0, 1.7f, 7.2f);
        _camera.LookAt(new Vector3(0, 1.1f, 0), Vector3.Up);
        int i = 0;
        foreach (var kind in new[] { EnemyKind.Husk, EnemyKind.Spitter, EnemyKind.Warden })
        {
            var view = new EnemyView(kind);
            view.ForcePose("idle");
            _posed.Add(view);
            Place(view, new Vector3(-3.4f + i * 3.4f, 0, 0), kind.ToString(), spin: 0.5);
            i++;
        }
    }

    private void BuildPoses()
    {
        _camera.Position = new Vector3(0, 6.5f, 15f);
        _camera.LookAt(new Vector3(0, 0.8f, 0), Vector3.Up);
        string[] poses = ["walk", "windup", "stunned"];
        int row = 0;
        foreach (var kind in new[] { EnemyKind.Husk, EnemyKind.Spitter, EnemyKind.Warden })
        {
            for (int col = 0; col < 3; col++)
            {
                var view = new EnemyView(kind);
                view.ForcePose(poses[col]);
                _posed.Add(view);
                Place(view, new Vector3(-4.2f + col * 4.2f, 0, -4.5f + row * 4.5f), $"{kind} {poses[col]}", spin: 0.35, labelHeight: 0.05f);
            }
            row++;
        }
    }

    private void BuildWeapons()
    {
        _camera.Position = new Vector3(0, 1.1f, 3.2f);
        _camera.LookAt(new Vector3(0, 0.75f, 0), Vector3.Up);
        var revolver = ModelFactory.Build(ModelCatalog.Revolver);
        revolver.Root.Scale = Vector3.One * 4.5f;
        revolver.SetGlow("gem", Colors.White * 1.5f);
        Place(revolver.Root, new Vector3(-1.1f, 0.75f, 0), "Revolver", spin: 0.6, labelHeight: 0.05f);
        var shotgun = ModelFactory.Build(ModelCatalog.Shotgun);
        shotgun.Root.Scale = Vector3.One * 4.5f;
        shotgun.SetGlow("gem", Colors.White * 1.5f);
        Place(shotgun.Root, new Vector3(1.1f, 0.75f, 0), "Shotgun", spin: 0.6, labelHeight: 0.05f);
        var orb = ModelFactory.Build(ModelCatalog.Orb, castShadows: false);
        orb.Root.Scale = Vector3.One * 1.0f;
        Place(orb.Root, new Vector3(-0.35f, 1.7f, 0), "", spin: 1.5);
        var reflected = ModelFactory.Build(ModelCatalog.ReflectedOrb, castShadows: false);
        reflected.Root.Scale = Vector3.One * 1.0f;
        Place(reflected.Root, new Vector3(0.35f, 1.7f, 0), "", spin: 1.5);
    }

    private void BuildProps()
    {
        _camera.Position = new Vector3(0, 1.6f, 5.4f);
        _camera.LookAt(new Vector3(0, 0.55f, 0), Vector3.Up);
        Place(ModelFactory.Build(ModelCatalog.Chest).Root, new Vector3(-3.2f, 0, 0), "Chest", spin: 0.5, labelHeight: 0.05f);
        var open = ModelFactory.Build(ModelCatalog.Chest);
        if (open.Part("lid") is { } lid) lid.RotationDegrees = new Vector3(105, 0, 0);
        Place(open.Root, new Vector3(-1.6f, 0, 0), "Chest (open)", spin: 0.5, labelHeight: 0.05f);
        Place(ModelFactory.Build(ModelCatalog.Key).Root, new Vector3(0, 0.2f, 0), "Key", spin: 1.0, labelHeight: 0.05f);
        Place(ModelFactory.Build(ModelCatalog.SpikeTrap).Root, new Vector3(1.8f, 0, 0), "Spike trap", spin: 0.4, labelHeight: 0.05f);
        var torch = ModelFactory.Build(ModelCatalog.Torch);
        Place(torch.Root, new Vector3(3.4f, 0.8f, 0), "Torch", spin: 0.6, labelHeight: 0.05f);
        // The loot a chest can release, one pickup per kind (a spare key is the key model above).
        var loot = new[]
        {
            (ModelCatalog.LootHealth, "Health"), (ModelCatalog.LootCleanse, "Cleanse"), (ModelCatalog.LootStyle, "Style"),
            (ModelCatalog.LootBloodlust, "Bloodlust"), (ModelCatalog.LootParry, "Open guard"),
        };
        for (int i = 0; i < loot.Length; i++)
        {
            var pickup = ModelFactory.Build(loot[i].Item1, castShadows: false);
            pickup.Root.Scale = Vector3.One * 2.0f;
            Place(pickup.Root, new Vector3(-2.6f + i * 1.3f, 1.75f, 0), loot[i].Item2, spin: 0.8, labelHeight: 1.45f);
        }
        AddChild(new OmniLight3D { Position = new Vector3(3.4f, 1.5f, 0.2f), LightColor = new Color(1f, 0.6f, 0.25f), OmniRange = 4 });
        Place(ModelFactory.Build(ModelCatalog.Scroll).Root, new Vector3(0.85f, 0, 0.3f), "Scroll", spin: 0.5, labelHeight: 0.05f);
    }

    public override void _Process(double delta)
    {
        _time += delta;
        foreach (var (node, speed) in _spin) node.RotationDegrees = new Vector3(0, (float)(_time * speed * 60 % 360), 0);
        foreach (var view in _posed) view.Animate(EnemyState.Chasing, delta);
    }
}
