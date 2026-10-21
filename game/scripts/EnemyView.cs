using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Models;

namespace Hollowfall.Game;

/// <summary>
/// Draws one enemy from its committed model and plays the clips Core baked for it. The simulation's state picks the clip:
/// walks and flees scrub through the stride so the feet keep pace with the creature, a windup scrubs through its wind-up
/// (arms raised, glow flaring yellow, the cue to parry), and a blow snaps the clip down once. The glow telegraph is the one
/// thing left in code, because it follows the live state rather than a fixed clip.
/// </summary>
public partial class EnemyView : Node3D
{
    private const double TurnSpeed = 9.0;
    private const double BlendSeconds = 0.2;

    private readonly EnemyKind _kind;
    private readonly EnemySpec _spec;
    private ModelRig _rig = null!;
    private Vector3 _lastPosition;
    private double _speed, _phase, _time, _windupTime;
    private double _yaw;
    private bool _wasWindup;
    private Motion? _forced;
    private Motion? _playing;

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
        _lastPosition = Position;
    }

    /// <summary>For the model viewer: hold one clip instead of following a simulation.</summary>
    public void ForcePose(Motion? motion) => _forced = motion;

    /// <summary>Follows the simulation: moves to the enemy, turns toward the player and plays its clip.</summary>
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

    /// <summary>Plays the model for a state. Shared by the game and the viewer.</summary>
    public void Animate(EnemyState state, double delta)
    {
        _time += delta;
        double instant = delta > 0 ? new Vector2(Position.X - _lastPosition.X, Position.Z - _lastPosition.Z).Length() / delta : 0;
        _lastPosition = Position;
        double moving = _forced == Motion.Walk ? _spec.Speed : instant;
        _speed = Mathf.Lerp(_speed, moving, Math.Min(1, delta * 10));
        _phase += _speed * (_kind == EnemyKind.Warden ? 1.6 : 2.6) * delta;
        double gait = Math.Clamp(_speed / Math.Max(_spec.Speed, 0.1), 0, 1);

        var motion = _forced ?? Choose(state, gait);
        bool windup = motion == Motion.Windup;
        _windupTime = windup ? _windupTime + delta : 0;
        // The blow lands the moment the windup ends: the strike clip snaps the arms down.
        bool landed = _wasWindup && !windup && motion != Motion.Stunned;
        _wasWindup = windup;
        Show(landed ? Motion.Strike : motion);

        // Telegraph: while an attack winds up the glow flashes bright yellow, the cue that it can be parried. A stunned
        // enemy dims; otherwise the glow breathes a little so things look alive.
        Color glow = windup
            ? new Color(1f, 0.95f, 0.25f).Lerp(Colors.White, (float)(0.5 + 0.5 * Math.Sin(_time * 30)))
            : motion == Motion.Stunned ? new Color(0.35f, 0.35f, 0.4f) : Colors.White * (float)(0.9 + 0.1 * Math.Sin(_time * 3));
        glow.A = 1;
        _rig.SetGlow(windup ? glow * 1.6f : glow);
    }

    private Motion Choose(EnemyState state, double gait)
    {
        if (state == EnemyState.Stunned) return Motion.Stunned;
        if (state == EnemyState.Windup) return Motion.Windup;
        if (IsStriking) return Motion.Strike;
        bool walking = gait > 0.15;
        if (walking && _intent == Hollowfall.Core.Relations.Intent.Flee) return Motion.Flee;
        if (_intent == Hollowfall.Core.Relations.Intent.Warn) return Motion.Warn;
        return walking ? Motion.Walk : Motion.Idle;
    }

    private bool IsStriking =>
        _rig.Animator is { } animator && animator.IsPlaying() && animator.CurrentAnimation == Motions.Name(Motion.Strike);

    private bool Has(Motion motion) => _rig.Animator?.HasAnimation(Motions.Name(motion)) == true;

    /// <summary>Plays a clip, or scrubs it: walks and flees follow the stride, a windup follows its timer. A blow snaps; the rest blend.</summary>
    private void Show(Motion wanted)
    {
        if (_rig.Animator is not { } animator) return;
        var motion = Has(wanted) ? wanted : Motion.Idle;
        if (motion == _playing && !Motions.FollowsStride(motion) && motion != Motion.Windup) return;

        string name = Motions.Name(motion);
        // Nothing to blend from on the first clip, and a blow is instant; everything else eases between clips.
        double blend = _playing == null || motion == Motion.Strike ? 0 : BlendSeconds;
        if (Motions.FollowsStride(motion) || motion == Motion.Windup)
        {
            if (motion != _playing) animator.Play(name, blend, 0);
            animator.Seek(Motions.FollowsStride(motion) ? StridePosition() : Math.Min(_windupTime, _spec.WindupSeconds), update: true);
        }
        else if (motion != _playing || motion == Motion.Strike)
        {
            animator.Play(name, blend);
            animator.Seek(Motions.Loops(motion) ? _time % Motions.LoopSeconds : 0, update: true);
        }
        _playing = motion;
    }

    private double StridePosition() => _phase % Math.Tau / Math.Tau * Motions.StrideSeconds;

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
}
