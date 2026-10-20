using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>
/// Draws one enemy from its low-poly model and animates it from the simulation's state, with no animation files: limbs
/// swing about their joints in time with the speed it moves, an attack winds up with a clear pose (arms raised, mouth
/// swelling, core flaring) that turns the glow bright yellow, which is the cue to parry, and a stunned enemy sags and wobbles.
/// </summary>
public partial class EnemyView : Node3D
{
    private const double TurnSpeed = 9.0;

    private readonly EnemyKind _kind;
    private readonly EnemySpec _spec;
    private ModelRig _rig = null!;
    private Vector3 _lastPosition;
    private double _speed, _phase, _time, _windupTime, _strike;
    private double _yaw;
    private bool _wasWindup;
    private string? _forcedPose;
    private readonly Dictionary<string, Vector3> _rest = [];

    public int EnemyId { get; }

    public EnemyView(Enemy enemy) : this(enemy.Kind) => EnemyId = enemy.Id;

    /// <summary>An enemy of a kind with no simulation behind it, for the model viewer.</summary>
    public EnemyView(EnemyKind kind)
    {
        _kind = kind;
        _spec = EnemySpec.Of(kind);
    }

    /// <summary>The model, for the corpse effect and the model viewer.</summary>
    public ModelRig Rig => _rig;

    public override void _Ready()
    {
        _rig = ModelFactory.Build(ModelCatalog.ForEnemy(_kind));
        AddChild(_rig.Root);
        // Poses move parts from where the model puts them, so a change to the model never leaves the animation behind.
        foreach (var (name, node) in _rig.Parts) _rest[name] = node.Position;
        _lastPosition = Position;
    }

    /// <summary>For the model viewer: hold a pose ("idle", "walk", "windup", "stunned") instead of following a simulation.</summary>
    public void ForcePose(string? pose) => _forcedPose = pose;

    /// <summary>Follows the simulation: moves to the enemy, turns toward the player and poses the limbs.</summary>
    public void Follow(Enemy enemy, Vector3 playerPosition, double delta)
    {
        var target = new Vector3((float)enemy.Feet.X, (float)enemy.Feet.Y, (float)enemy.Feet.Z);
        Position = target;

        double dx = playerPosition.X - target.X, dz = playerPosition.Z - target.Z;
        if (dx * dx + dz * dz > 1e-4)
        {
            double want = Math.Atan2(-dx, -dz);
            double diff = Mathf.Wrap(want - _yaw, -Math.PI, Math.PI);
            _yaw += Math.Clamp(diff, -TurnSpeed * delta, TurnSpeed * delta);
        }
        Rotation = new Vector3(0, (float)_yaw, 0);

        Animate(enemy.State, delta);
    }

    /// <summary>Poses the model for a state. Shared by the game and the viewer.</summary>
    public void Animate(EnemyState state, double delta)
    {
        _time += delta;
        var pose = _forcedPose ?? state switch
        {
            EnemyState.Windup => "windup",
            EnemyState.Stunned => "stunned",
            _ => "move",
        };

        double instant = delta > 0 ? new Vector2(Position.X - _lastPosition.X, Position.Z - _lastPosition.Z).Length() / delta : 0;
        _lastPosition = Position;
        double moving = pose == "walk" ? _spec.Speed : instant;
        _speed = Mathf.Lerp(_speed, moving, Math.Min(1, delta * 10));
        _phase += _speed * (_kind == EnemyKind.Warden ? 1.6 : 2.6) * delta;

        bool windup = pose == "windup";
        _windupTime = windup ? _windupTime + delta : 0;
        if (_wasWindup && !windup && pose != "stunned") _strike = 1; // the blow lands: snap the arms down
        _wasWindup = windup;
        _strike = Math.Max(0, _strike - delta * 6);

        double gait = Math.Clamp(_speed / Math.Max(_spec.Speed, 0.1), 0, 1);
        double raise = windup ? Ease(_windupTime / Math.Max(_spec.WindupSeconds, 0.05)) : 0;

        switch (_kind)
        {
            case EnemyKind.Husk:
                PoseHusk(gait, raise, pose == "stunned");
                break;
            case EnemyKind.Spitter:
                PoseSpitter(raise, pose == "stunned");
                break;
            default:
                PoseWarden(gait, raise, pose == "stunned");
                break;
        }

        // Telegraph: while an attack winds up the glow flashes bright yellow, the cue that it can be parried. A stunned
        // enemy dims; otherwise the glow breathes a little so things look alive.
        Color glow = windup
            ? new Color(1f, 0.95f, 0.25f).Lerp(Colors.White, (float)(0.5 + 0.5 * Math.Sin(_time * 30)))
            : pose == "stunned" ? new Color(0.35f, 0.35f, 0.4f) : Colors.White * (float)(0.9 + 0.1 * Math.Sin(_time * 3));
        glow.A = 1;
        _rig.SetGlow(windup ? glow * 1.6f : glow);
    }

    private Vector3 Rest(string part) => _rest.GetValueOrDefault(part);

    private static double Ease(double t) => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3);

    private void Rot(string part, double xDeg, double yDeg = 0, double zDeg = 0)
    {
        if (_rig.Part(part) is { } node) node.RotationDegrees = new Vector3((float)xDeg, (float)yDeg, (float)zDeg);
    }

    private void PoseHusk(double gait, double raise, bool stunned)
    {
        double swing = Math.Sin(_phase) * 38 * gait, bob = Math.Abs(Math.Sin(_phase)) * 0.05 * gait;
        double idle = Math.Sin(_time * 2.2) * 2;

        Rot("leg_l", swing);
        Rot("leg_r", -swing);

        // Arms hang and swing; on a windup they go up and back over the head, then slam down.
        double armSwing = -swing * 0.7;
        double up = -150 * raise + 60 * _strike;
        Rot("arm_l", stunned ? 15 : armSwing * (1 - raise) + up, 0, 4 + idle);
        Rot("arm_r", stunned ? 15 : -armSwing * (1 - raise) + up, 0, -4 - idle);

        double lean = (stunned ? 14 : -6 * gait) + 10 * raise - 22 * _strike;
        Rot("torso", lean, Math.Sin(_phase) * 5 * gait, stunned ? Math.Sin(_time * 14) * 6 : 0);
        Rot("head", stunned ? 40 : 6 * gait - 10 * raise, 0, stunned ? Math.Sin(_time * 11) * 8 : 0);
        if (_rig.Part("torso") is { } t) t.Position = Rest("torso") + new Vector3(0, (float)bob, 0);
    }

    private void PoseSpitter(double raise, bool stunned)
    {
        // It floats: bob, and drift side to side a little.
        double bob = Math.Sin(_time * 2.0) * 0.09;
        double swell = 1 + 0.2 * raise;
        if (_rig.Part("body") is { } body)
        {
            body.Position = Rest("body") + new Vector3(0, (float)bob, 0);
            body.Scale = Vector3.One * (float)(swell * (1 - 0.08 * _strike));
            body.RotationDegrees = new Vector3((float)(stunned ? Math.Sin(_time * 9) * 12 : 0), 0, (float)(stunned ? Math.Sin(_time * 7) * 18 : Math.Sin(_time * 1.3) * 3));
        }
        if (_rig.Part("mouth") is { } mouth)
        {
            mouth.Position = Rest("mouth") + new Vector3(0, (float)bob, -0.3f * (float)(swell - 1));
            mouth.Scale = Vector3.One * (float)(1 + 0.45 * raise);
        }
        for (int i = 0; i < 4; i++)
            if (_rig.Part($"tent_{i}") is { } tent)
            {
                double wave = Math.Sin(_time * 3.0 + i * 1.7);
                tent.Position = Rest($"tent_{i}") + new Vector3(0, (float)bob, 0);
                tent.RotationDegrees = new Vector3((float)(wave * 14 + 25 * raise), 0, (float)(Math.Cos(_time * 2.4 + i) * 14));
            }
    }

    private void PoseWarden(double gait, double raise, bool stunned)
    {
        double swing = Math.Sin(_phase) * 24 * gait, bob = Math.Abs(Math.Sin(_phase)) * 0.04 * gait;
        Rot("leg_l", swing);
        Rot("leg_r", -swing);

        // Both fists go over the head on a windup and come down hard; the core flares.
        double up = -165 * raise + 80 * _strike;
        Rot("arm_l", stunned ? 20 : -swing * 0.5 * (1 - raise) + up, 0, 6);
        Rot("arm_r", stunned ? 20 : swing * 0.5 * (1 - raise) + up, 0, -6);
        Rot("torso", (stunned ? 12 : 0) + 8 * raise - 18 * _strike, Math.Sin(_phase) * 3 * gait);
        Rot("head", stunned ? 35 : -6 * raise + 8 * _strike);

        if (_rig.Part("torso") is { } torso)
        {
            torso.Position = Rest("torso") + new Vector3(0, (float)bob, 0);
            // The chest core breathes, and flares on a windup.
            torso.Scale = new Vector3(1, 1, 1) * (float)(1 + 0.03 * Math.Sin(_time * (3 + 12 * raise)));
        }
    }
}
