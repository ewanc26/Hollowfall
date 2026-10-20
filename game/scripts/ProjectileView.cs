using Godot;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>An enemy shot: a spinning, flickering gem of light with a small glow around it. Reflected shots turn teal.</summary>
public partial class ProjectileView : Node3D
{
    private ModelRig? _rig;
    private OmniLight3D _light = null!;
    private bool _reflected;
    private double _time;

    public override void _Ready()
    {
        _light = new OmniLight3D { OmniRange = 3.5f, LightEnergy = 1.4f, LightColor = new Color(1f, 0.5f, 0.15f), ShadowEnabled = false };
        AddChild(_light);
        Build(false);
    }

    private void Build(bool reflected)
    {
        _rig?.Root.QueueFree();
        _rig = ModelFactory.Build(reflected ? ModelCatalog.ReflectedOrb : ModelCatalog.Orb, castShadows: false);
        AddChild(_rig.Root);
        _reflected = reflected;
        _light.LightColor = reflected ? new Color(0.2f, 0.9f, 0.9f) : new Color(1f, 0.5f, 0.15f);
    }

    public void Follow(Vector3 position, bool reflected, double delta)
    {
        Position = position;
        if (reflected != _reflected) Build(reflected);

        _time += delta;
        if (_rig?.Part("orb") is { } orb)
        {
            orb.RotationDegrees = new Vector3((float)(_time * 140), (float)(_time * 210), 0);
            orb.Scale = Vector3.One * (float)(1 + 0.12 * Math.Sin(_time * 22));
        }
        _light.LightEnergy = (float)(1.2 + 0.4 * Math.Sin(_time * 18));
    }
}
