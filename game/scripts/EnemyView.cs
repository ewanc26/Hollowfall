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

    // Creatures (the relationship system): what it wants right now, and the little pip that says how to read it.
    private Hollowfall.Core.Relations.Intent _intent = Hollowfall.Core.Relations.Intent.Idle;
    private MeshInstance3D? _pip;
    private StandardMaterial3D? _pipMaterial;

    /// <summary>The pip's colour and visibility, for tests and the HUD (null when no pip is shown).</summary>
    public Color? PipColor => _pip is { Visible: true } && _pipMaterial != null ? _pipMaterial.AlbedoColor : null;

    public override void _Ready()
    {
        _rig = ModelFactory.Build(ModelCatalog.ForEnemy(_kind));
        AddChild(_rig.Root);
        if (Hollowfall.Core.Relations.SpeciesInfo.Of(_kind) is { AlwaysHostile: false })
        {
            _pipMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.White, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
            _pip = new MeshInstance3D
            {
                Name = "Pip",
                Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 6, Rings = 3 },
                MaterialOverride = _pipMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, (float)_spec.Height + 0.3f, 0),
                Visible = false,
            };
            AddChild(_pip);
        }
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
        var moved = target - Position;
        Position = target;

        double dx = playerPosition.X - target.X, dz = playerPosition.Z - target.Z;
        if (enemy.Mind is { } mind && !mind.Species.AlwaysHostile)
        {
            _intent = mind.Intent;
            UpdatePip(mind, enemy, Math.Sqrt(dx * dx + dz * dz), delta);
            // A creature minding its own business faces where it is going; only the ones dealing with you face you.
            if (_intent is not (Hollowfall.Core.Relations.Intent.Warn or Hollowfall.Core.Relations.Intent.Attack or Hollowfall.Core.Relations.Intent.Follow))
            {
                if (moved.X * moved.X + moved.Z * moved.Z > 1e-6) { dx = moved.X; dz = moved.Z; }
                else dx = dz = 0;
                if (_intent == Hollowfall.Core.Relations.Intent.Flee) { dx = -(playerPosition.X - target.X); dz = -(playerPosition.Z - target.Z); }
            }
        }
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
            case EnemyKind.Brownie:
                PoseBiped(gait, raise, pose == "stunned", 30, 1.0, scurry: true);
                break;
            case EnemyKind.Selkie:
                PoseBiped(gait, raise, pose == "stunned", 28, 0.7, scurry: false);
                break;
            case EnemyKind.Kelpie:
                PoseKelpie(gait, raise, pose == "stunned");
                break;
            case EnemyKind.BeanNighe:
                PoseNighe(gait, pose == "stunned");
                break;
            case EnemyKind.Hare:
                PoseHare(gait, pose == "stunned");
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

    /// <summary>Shows the pip once the player has noticed the creature: colour from its cue, a quicker pulse while it warns.</summary>
    private void UpdatePip(Hollowfall.Core.Relations.Mind mind, Enemy enemy, double distance, double delta)
    {
        if (_pip == null || _pipMaterial == null) return;
        var colour = Hollowfall.Core.Relations.StandingBoard.ShowsPip(enemy, distance) ? Hollowfall.Core.Relations.StandingBoard.PipColour(mind.Cue) : null;
        _pip.Visible = colour != null;
        if (colour is not { } hex) return;
        double pulse = mind.Intent == Hollowfall.Core.Relations.Intent.Warn ? 0.5 + 0.5 * Math.Sin(_time * 14) : 0.78;
        float fade = (float)Math.Clamp(1.2 - distance / Hollowfall.Core.Relations.StandingBoard.PipRange, 0.25, 1);
        _pipMaterial.AlbedoColor = new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, (float)(0.8 * pulse) * fade);
        _pip.Position = new Vector3(0, (float)_spec.Height + 0.3f + (float)Math.Sin(_time * 2.5) * 0.03f, 0);
    }

    /// <summary>Brownies and Selkies share a walk: legs and arms swing; frightened, the arms go up; angry, they rear back and swing.</summary>
    private void PoseBiped(double gait, double raise, bool stunned, double swingDeg, double armFactor, bool scurry)
    {
        double swing = Math.Sin(_phase * (scurry ? 1.3 : 1)) * swingDeg * gait, bob = Math.Abs(Math.Sin(_phase)) * (scurry ? 0.04 : 0.03) * gait;
        bool flee = _intent == Hollowfall.Core.Relations.Intent.Flee;
        bool warn = _intent == Hollowfall.Core.Relations.Intent.Warn;
        Rot("leg_l", swing);
        Rot("leg_r", -swing);

        double up = -150 * raise + 60 * _strike;
        double fright = flee ? -120 + Math.Sin(_time * 18) * 12 : 0;
        double square = warn ? -35 : 0;
        Rot("arm_l", stunned ? 15 : -swing * armFactor * (1 - raise) + up + fright + square, 0, 6);
        Rot("arm_r", stunned ? 15 : swing * armFactor * (1 - raise) + up + fright + square, 0, -6);
        double breathe = Math.Sin(_time * 2.0) * 1.5;
        Rot("torso", (stunned ? 20 : (flee ? 14 : 0) + (warn ? -6 : 0)) + 8 * raise - 18 * _strike + breathe, Math.Sin(_phase) * 4 * gait, stunned ? Math.Sin(_time * 14) * 6 : 0);
        Rot("head", stunned ? 40 : (flee ? -12 : 0) + Math.Sin(_time * 1.3) * 3);
        if (_rig.Part("torso") is { } t) t.Position = Rest("torso") + new Vector3(0, (float)bob, 0);
    }

    private void PoseKelpie(double gait, double raise, bool stunned)
    {
        double swing = Math.Sin(_phase * 0.8) * 34 * gait;
        // Gallop: diagonal pairs move together.
        Rot("leg_fl", swing);
        Rot("leg_br", swing);
        Rot("leg_fr", -swing);
        Rot("leg_bl", -swing);
        if (raise > 0 || _strike > 0)
        {
            // It rears onto its hind legs for the blow, forelegs pawing, then crashes down.
            Rot("leg_fl", -70 * raise + 40 * _strike);
            Rot("leg_fr", -70 * raise + 40 * _strike);
        }
        double rear = (stunned ? 12 : -28 * raise + 18 * _strike);
        if (_rig.Part("body") is { } body)
        {
            body.RotationDegrees = new Vector3((float)rear, 0, 0);
            body.Position = Rest("body") + new Vector3(0, (float)(Math.Abs(Math.Sin(_phase * 0.8)) * 0.05 * gait), 0);
        }
        Rot("neck", stunned ? 25 : Math.Sin(_time * 1.6) * 3 + 6 * gait);
        Rot("head", stunned ? 30 : 4 * raise + Math.Sin(_time * 1.1) * 3);
        bool warn = _intent == Hollowfall.Core.Relations.Intent.Warn;
        if (warn) Rot("head", -12 + Math.Sin(_time * 9) * 5);
        Rot("tail", Math.Sin(_time * 2.2) * 10 + 12 * gait, 0, Math.Cos(_time * 1.7) * 8);
    }

    private void PoseNighe(double gait, bool stunned)
    {
        // She wrings out a cloth as she stands: forearms working, the cloth twisting, a slow sway.
        double wring = Math.Sin(_time * (_intent == Hollowfall.Core.Relations.Intent.Flee ? 9 : 3.2));
        double bob = Math.Abs(Math.Sin(_phase)) * 0.03 * gait;
        Rot("arm_l", stunned ? 20 : -30 + wring * 22, 0, 4);
        Rot("arm_r", stunned ? 20 : -30 - wring * 22, 0, -4);
        Rot("torso", (stunned ? 18 : 6) + Math.Sin(_time * 1.2) * 2, Math.Sin(_time * 0.9) * 4, stunned ? Math.Sin(_time * 12) * 5 : Math.Sin(_time * 1.1) * 2);
        Rot("head", stunned ? 35 : 8 + Math.Sin(_time * 0.7) * 4);
        if (_rig.Part("cloth") is { } cloth)
        {
            cloth.RotationDegrees = new Vector3(0, (float)(wring * 30), (float)(wring * 12));
            cloth.Position = Rest("cloth") + new Vector3(0, (float)(wring * 0.02), 0);
        }
        if (_rig.Part("torso") is { } t) t.Position = Rest("torso") + new Vector3(0, (float)bob, 0);
    }

    private void PoseHare(double gait, bool stunned)
    {
        // Hops: the whole body leaps and the hind legs kick out behind; at rest the nose twitches and the ears turn.
        double hop = Math.Abs(Math.Sin(_phase * 1.4)), twitch = Math.Sin(_time * 11);
        if (_rig.Part("body") is { } body)
        {
            body.Position = Rest("body") + new Vector3(0, (float)(hop * 0.22 * gait), 0);
            body.RotationDegrees = new Vector3((float)(stunned ? 70 : -22 * gait * hop + Math.Sin(_time * 2.4) * 1.5), 0, stunned ? 80 : 0);
        }
        Rot("leg_l", stunned ? 0 : 40 * hop * gait);
        Rot("leg_r", stunned ? 0 : 40 * hop * gait);
        Rot("head", stunned ? 20 : 10 * gait * hop + twitch * 2);
        bool alert = _intent == Hollowfall.Core.Relations.Intent.Flee || gait > 0.3;
        Rot("ear_l", alert ? -30 : 8 + Math.Sin(_time * 1.7) * 8, 0, 10);
        Rot("ear_r", alert ? -30 : 8 + Math.Sin(_time * 1.3 + 1) * 8, 0, -10);
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
