using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Portals;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Combat;

/// <summary>Where the player is and what they are doing this tick. The host owns movement; the sim owns combat.</summary>
/// <remarks><paramref name="Velocity"/> and <paramref name="Crossed"/> (true on the step the player came out of a portal) drive the Meteor.</remarks>
public readonly record struct PlayerView(Vec3 Feet, Vec3 Eye, Vec3 Forward, bool Airborne, bool Sliding, Vec3 Velocity = default, bool Crossed = false);

/// <summary>Buttons pressed this tick (edges, not holds: the host applies fire-rate by calling once per press).</summary>
public readonly record struct PlayerActions(bool FireRevolver, bool FireShotgun, bool Parry, bool PortalTeal = false, bool PortalViolet = false, bool Interact = false);

/// <summary>
/// The fast-combat loop for one dungeon: entering a room with enemies locks its doors and spawns them,
/// enemies telegraph attacks that can be parried, hits at close range heal, style builds and decays, and
/// every floor is graded when you leave it. Fixed-step, deterministic, and free of engine types.
/// </summary>
public sealed class CombatSim
{
    public const double ParryWindowSeconds = 0.22;
    public const double ParryCooldownSeconds = 0.5;
    public const double ParryReach = 3.2;
    public const double ShotgunParryReach = 2.0;
    public const double ParryStunSeconds = 1.6;
    public const double ParryDamage = 25;
    public const double ProjectileSpeed = 13;
    public const double ReflectedSpeedBoost = 1.6;
    public const double ReflectedDamageBoost = 2.5;
    public const double ExplosionRadius = 3;
    public const double ExplosionDamage = 40;

    private sealed class Encounter
    {
        public int Floor;
        public int RoomId;
        public List<Spawn> Spawns = [];
        public List<(int X, int Z)> DoorCells = [];
        public bool Triggered, Cleared;
        public List<Enemy> Enemies = [];
    }

    private sealed class FloorProgress
    {
        public bool Started, Completed;
        public double StartTime;
        public double FinishTime;
        public double StartStyle;
        public int Kills;
        public int Restarts;
        public int Enemies;
        public FloorTargets Targets;
    }

    private readonly VoxelWorld _world;
    private readonly DungeonSite _site;
    private readonly SeededRandom _rng;
    private readonly List<Encounter> _encounters = [];
    private readonly FloorProgress[] _floors;
    private readonly List<Enemy> _enemies = [];
    private readonly List<Projectile> _projectiles = [];
    private readonly List<CombatEvent> _events = [];
    private readonly HashSet<(int, int)> _dirtyChunks = [];
    private readonly Dictionary<string, double> _cooldowns = [];
    private readonly Interactables _items;
    private double _parryWindow, _parryCooldown;
    private readonly MeteorTracker _meteor = new();
    private int _nextId = 1;

    /// <summary>The player's portal pair. The host moves the player through it with <see cref="PortalWalker"/>.</summary>
    public PortalSystem Portals { get; } = new();

    public PlayerVitals Vitals { get; } = new();

    /// <summary>Keys, chests and spike traps.</summary>
    public Interactables Items => _items;

    /// <summary>What the player has seen of each floor.</summary>
    public FloorMap Map { get; }
    public StyleMeter Style { get; } = new();
    public double Time { get; private set; }

    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    public int CurrentFloor { get; private set; } = -1;
    public bool Dead => Vitals.Dead;

    public CombatSim(VoxelWorld world, DungeonSite site)
    {
        _world = world;
        _site = site;
        _rng = new SeededRandom(world.Seed, site.RegionX, site.RegionZ, 700);
        _items = new Interactables(site.Spawns, world.Seed, d => { double before = Vitals.Health; HurtPlayer(d); return before - Vitals.Health; }, Vitals.Heal, _events.Add);
        Map = new FloorMap(site);
        _floors = new FloorProgress[site.FloorCount];
        for (int k = 0; k < site.FloorCount; k++)
        {
            _floors[k] = new FloorProgress();
            BuildEncounters(k);
        }
    }

    private void BuildEncounters(int floor)
    {
        var layout = _site.Floors[floor];
        var progress = _floors[floor];
        foreach (var group in _site.Spawns.Where(s => s.Floor == floor && s.Kind is SpawnKind.Monster or SpawnKind.Boss).GroupBy(s => s.RoomId))
        {
            var enc = new Encounter { Floor = floor, RoomId = group.Key, Spawns = [.. group] };
            for (int z = 1; z < FloorLayout.Size - 1; z++)
                for (int x = 1; x < FloorLayout.Size - 1; x++)
                    if (layout.CellAt(x, z) == CellKind.Door &&
                        (layout.RoomIdAt(x + 1, z) == group.Key || layout.RoomIdAt(x - 1, z) == group.Key ||
                         layout.RoomIdAt(x, z + 1) == group.Key || layout.RoomIdAt(x, z - 1) == group.Key))
                        enc.DoorCells.Add((x, z));
            _encounters.Add(enc);
            progress.Enemies += enc.Spawns.Count;
        }

        int path = 0;
        if (layout.ExitStairs is { } exit)
            path = (int)((Math.Abs(exit.X - layout.EntranceX) + Math.Abs(exit.Z - layout.EntranceZ)) * 1.3);
        else if (layout.BossRoom is { } boss)
            path = (int)((Math.Abs(boss.CenterX - layout.EntranceX) + Math.Abs(boss.CenterZ - layout.EntranceZ)) * 1.3);
        progress.Targets = FloorTargets.For(progress.Enemies, path);
    }

    public FloorTargets TargetsFor(int floor) => _floors[floor].Targets;

    /// <summary>Seconds spent on a floor so far (frozen once the floor is completed); zero if not yet entered.</summary>
    public double ElapsedOn(int floor)
    {
        var p = _floors[floor];
        if (!p.Started) return 0;
        return (p.Completed ? p.FinishTime : Time) - p.StartTime;
    }

    public int KillsOn(int floor) => _floors[floor].Kills;

    public int EnemiesOn(int floor) => _floors[floor].Enemies;

    public bool FloorCompleted(int floor) => _floors[floor].Completed;

    /// <summary>Takes everything that happened since the last call.</summary>
    public List<CombatEvent> DrainEvents()
    {
        var list = new List<CombatEvent>(_events);
        _events.Clear();
        return list;
    }

    /// <summary>Chunks whose blocks changed (doors locking and opening) since the last call, so they can be re-meshed.</summary>
    public List<(int X, int Z)> TakeDirtyChunks()
    {
        var list = _dirtyChunks.ToList();
        _dirtyChunks.Clear();
        return list;
    }

    private Func<int, int, int, BlockType> Blocks => _world.GetBlock;

    // ---- main step --------------------------------------------------------------------------

    public void Tick(double dt, PlayerView view, PlayerActions actions)
    {
        Time += dt;
        Style.Tick(dt);
        Style.NotifyMoving(view.Airborne || view.Sliding);
        Vitals.Tick(dt, Style.HardDamageSpeed);
        foreach (var key in _cooldowns.Keys.ToList()) _cooldowns[key] = Math.Max(0, _cooldowns[key] - dt);
        _parryCooldown = Math.Max(0, _parryCooldown - dt);

        if (Vitals.Dead) return;

        TrackFloor(view);
        if (CurrentFloor >= 0) Map.Reveal(CurrentFloor, view.Feet.X, view.Feet.Z);
        _items.Tick(dt, view, actions.Interact);
        TriggerEncounters(view);
        UpdateMeteor(dt, view);

        if (actions.Parry && _parryCooldown <= 0 && _parryWindow <= 0) _parryWindow = ParryWindowSeconds;
        if (_parryWindow > 0)
        {
            if (TryParry(view)) _parryWindow = 0;
            else
            {
                _parryWindow -= dt;
                if (_parryWindow <= 0) _parryCooldown = ParryCooldownSeconds;
            }
        }

        if (actions.PortalTeal) FirePortal(PortalColor.Teal, view);
        if (actions.PortalViolet) FirePortal(PortalColor.Violet, view);

        if (actions.FireRevolver) Fire(WeaponSpec.Revolver, view);
        if (actions.FireShotgun) Fire(WeaponSpec.Shotgun, view);

        foreach (var e in _enemies.ToList()) UpdateEnemy(e, dt, view);
        UpdateProjectiles(dt, view);
        CheckEncounterClears();
        _enemies.RemoveAll(e => e.Dead);
    }

    /// <summary>Adds an enemy outside any encounter, for tests, debugging and scripted fights.</summary>
    public Enemy SpawnEnemy(EnemyKind kind, Vec3 feet, int floor = 0, int roomId = -1)
    {
        var enemy = new Enemy(_nextId++, EnemySpec.Of(kind), feet, floor, roomId);
        _enemies.Add(enemy);
        _events.Add(new EnemySpawned(enemy.Id, kind, feet));
        return enemy;
    }

    // ---- floors and encounters --------------------------------------------------------------

    private void TrackFloor(PlayerView view)
    {
        int floor = _site.FloorIndexAt(view.Feet.X, view.Feet.Y + 0.1, view.Feet.Z);
        if (floor < 0 || floor == CurrentFloor) return;

        // Finishing a floor means getting down to the next one.
        if (CurrentFloor >= 0 && floor > CurrentFloor) CompleteFloor(CurrentFloor);
        CurrentFloor = floor;

        var p = _floors[floor];
        if (!p.Started)
        {
            p.Started = true;
            p.StartTime = Time;
            p.StartStyle = Style.TotalScore;
        }
    }

    private void CompleteFloor(int floor)
    {
        var p = _floors[floor];
        if (p.Completed || !p.Started) return;
        p.Completed = true;
        p.FinishTime = Time;
        var result = FloorRanking.Score(p.Targets, Time - p.StartTime, p.Kills, Style.TotalScore - p.StartStyle, p.Restarts);
        _events.Add(new FloorCompleted(floor, result, Time - p.StartTime));
    }

    private void TriggerEncounters(PlayerView view)
    {
        if (CurrentFloor < 0) return;
        var layout = _site.Floors[CurrentFloor];
        int lx = (int)Math.Floor(view.Feet.X) - _site.OriginX, lz = (int)Math.Floor(view.Feet.Z) - _site.OriginZ;
        int room = layout.RoomIdAt(lx, lz);
        if (room < 0) return;

        foreach (var enc in _encounters)
            if (enc.Floor == CurrentFloor && enc.RoomId == room && !enc.Triggered && !enc.Cleared)
                StartEncounter(enc);
    }

    private void StartEncounter(Encounter enc)
    {
        enc.Triggered = true;
        SetDoors(enc, locked: true);

        foreach (var sp in enc.Spawns)
        {
            var kind = sp.Kind == SpawnKind.Boss ? EnemyKind.Warden : (sp.X + sp.Z) % 2 == 0 ? EnemyKind.Husk : EnemyKind.Spitter;
            var enemy = new Enemy(_nextId++, EnemySpec.Of(kind), new Vec3(sp.X + 0.5, sp.Y, sp.Z + 0.5), enc.Floor, enc.RoomId);
            _enemies.Add(enemy);
            enc.Enemies.Add(enemy);
            _events.Add(new EnemySpawned(enemy.Id, kind, enemy.Feet));
        }
        _events.Add(new EncounterStarted(enc.Floor, enc.RoomId, enc.Enemies.Count));
    }

    private void SetDoors(Encounter enc, bool locked)
    {
        int y0 = DungeonSite.FloorY(enc.Floor) + 1;
        foreach (var (x, z) in enc.DoorCells)
        {
            int wx = _site.OriginX + x, wz = _site.OriginZ + z;
            for (int y = y0; y <= y0 + 1; y++) _world.SetBlock(wx, y, wz, locked ? BlockType.Gate : BlockType.Door);
            _dirtyChunks.Add((wx >> 4, wz >> 4));
            // A door cell next to a chunk border also changes the neighbour's visible faces.
            if ((wx & 15) == 0) _dirtyChunks.Add(((wx >> 4) - 1, wz >> 4));
            if ((wx & 15) == 15) _dirtyChunks.Add(((wx >> 4) + 1, wz >> 4));
            if ((wz & 15) == 0) _dirtyChunks.Add((wx >> 4, (wz >> 4) - 1));
            if ((wz & 15) == 15) _dirtyChunks.Add((wx >> 4, (wz >> 4) + 1));
        }
        _events.Add(new DoorsChanged(enc.Floor, enc.RoomId, locked));
    }

    private void CheckEncounterClears()
    {
        foreach (var enc in _encounters)
        {
            if (!enc.Triggered || enc.Cleared || enc.Enemies.Any(e => !e.Dead)) continue;
            enc.Cleared = true;
            SetDoors(enc, locked: false);
            _events.Add(new EncounterCleared(enc.Floor, enc.RoomId));

            // Killing the boss on the last floor ends the dungeon.
            if (enc.Floor == _site.FloorCount - 1 && _site.Floors[enc.Floor].BossRoom?.Id == enc.RoomId) CompleteFloor(enc.Floor);
        }
    }

    /// <summary>
    /// Puts the player back at the start of the current floor: enemies vanish, doors open, encounters can
    /// start again, vitals and style reset, and the floor's restart count (which costs rank points) goes up.
    /// </summary>
    public void Respawn()
    {
        if (CurrentFloor >= 0) _floors[CurrentFloor].Restarts++;
        foreach (var enc in _encounters.Where(e => e.Triggered && !e.Cleared))
        {
            SetDoors(enc, locked: false);
            enc.Triggered = false;
            enc.Enemies.Clear();
        }
        _enemies.Clear();
        _projectiles.Clear();
        Vitals.Reset();
        Style.Reset();
        Portals.Clear();
        _parryWindow = _parryCooldown = 0;
        _meteor.Reset();
    }

    // ---- weapons ----------------------------------------------------------------------------

    private void Fire(WeaponSpec weapon, PlayerView view)
    {
        if (_cooldowns.GetValueOrDefault(weapon.Name) > 0) return;
        _cooldowns[weapon.Name] = weapon.Cooldown;
        Style.NotifyWeaponUsed(weapon.Name);

        var hitThisShot = new HashSet<int>();
        bool parried = false;
        for (int i = 0; i < weapon.Pellets; i++)
        {
            var dir = weapon.Pellets == 1 ? view.Forward : Spread(view.Forward, weapon.SpreadRadians);

            // The shot may leave through a portal and carry on from the other one.
            var segments = Portals.TraceRay(Blocks, view.Eye, dir, weapon.Range);
            Enemy? target = null;
            Vec3 end = view.Eye;
            double travelled = 0, best = double.MaxValue, bestTotal = 0;
            var path = new List<(Vec3, Vec3)>();

            foreach (var (origin, direction, length) in segments)
            {
                Enemy? hitHere = null;
                double nearest = length;
                foreach (var e in _enemies)
                {
                    if (e.Dead) continue;
                    if (RayCast.HitBox(origin, direction, e.Bounds, length) is { } d && d <= nearest) { nearest = d; hitHere = e; }
                }

                var stop = origin + direction.Normalized() * nearest;
                path.Add((origin, stop));
                end = stop;
                if (hitHere != null)
                {
                    target = hitHere;
                    best = nearest;
                    bestTotal = travelled + nearest;
                    break;
                }
                travelled += length;
            }

            if (i == 0) _events.Add(new ShotFired(weapon.Name, view.Eye, end, path));
            if (target == null) continue;
            double distance = bestTotal;
            _ = best;

            var zone = target.ZoneAt(end.Y);
            double mult = zone == HitZone.Head ? weapon.HeadMultiplier : zone == HitZone.Limb ? weapon.LimbMultiplier : 1.0;
            double damage = weapon.Damage * mult;

            if (weapon.Pellets > 1 && distance <= ShotgunParryReach && target.Telegraphing && !parried)
            {
                parried = true;
                ParryEnemy(target, ParryKind.Shotgun);
            }

            Vitals.HealFromHit(zone, distance, weapon.PelletHeal);
            bool firstOnThisEnemy = hitThisShot.Add(target.Id);
            if (firstOnThisEnemy)
            {
                if (zone == HitZone.Head) Style.Award(StyleBonus.Headshot, weapon.Name);
                else if (zone == HitZone.Limb) Style.Award(StyleBonus.LimbHit, weapon.Name);
            }
            DamageEnemy(target, damage, zone, end, weapon.Name, view.Airborne);
        }
    }

    private void FirePortal(PortalColor color, PlayerView view)
    {
        var result = Portals.Place(color, view.Eye, view.Forward, view.Forward, Blocks);
        var p = result.Portal;
        _events.Add(new PortalPlaced(color, result.Placed,
            p?.Frame.Center ?? view.Eye, p?.Frame.Normal ?? new Vec3(0, 0, 0), p?.Frame.Up ?? new Vec3(0, 1, 0), result.Failure));
    }

    private Vec3 Spread(Vec3 forward, double radians)
    {
        var f = forward.Normalized();
        // Build any perpendicular basis and offset within a disc.
        var up = Math.Abs(f.Y) > 0.95 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
        var right = new Vec3(f.Y * up.Z - f.Z * up.Y, f.Z * up.X - f.X * up.Z, f.X * up.Y - f.Y * up.X).Normalized();
        var upv = new Vec3(right.Y * f.Z - right.Z * f.Y, right.Z * f.X - right.X * f.Z, right.X * f.Y - right.Y * f.X).Normalized();
        double angle = _rng.NextDouble() * Math.PI * 2, radius = Math.Sqrt(_rng.NextDouble()) * radians;
        return (f + right * (Math.Cos(angle) * radius) + upv * (Math.Sin(angle) * radius)).Normalized();
    }

    private void DamageEnemy(Enemy e, double damage, HitZone zone, Vec3 point, string weapon, bool airborne)
    {
        if (e.Dead) return;
        e.Health -= damage;
        bool killed = e.Health <= 0;
        _events.Add(new EnemyHit(e.Id, zone, damage, killed, point));
        if (!killed) return;

        e.State = EnemyState.Dead;
        _floors[e.Floor].Kills++;
        Style.RegisterKill(weapon, airborne);
        _events.Add(new EnemyKilled(e.Id, e.Kind));
    }

    // ---- parry ------------------------------------------------------------------------------

    private bool TryParry(PlayerView view)
    {
        var forward = view.Forward.Normalized();
        foreach (var e in _enemies)
        {
            if (e.Dead || !e.Telegraphing) continue;
            var to = (e.Feet + new Vec3(0, 1.0, 0)) - view.Eye;
            if (to.Length <= ParryReach && to.Normalized().Dot(forward) > 0.3)
            {
                ParryEnemy(e, ParryKind.Melee);
                return true;
            }
        }

        foreach (var p in _projectiles)
        {
            if (p.Reflected) continue;
            var to = p.Position - view.Eye;
            if (to.Length <= ParryReach && to.Normalized().Dot(forward) > 0.3)
            {
                p.Reflected = true;
                p.Velocity = forward * (p.Velocity.Length * ReflectedSpeedBoost);
                p.Damage *= ReflectedDamageBoost;
                p.Age = 0;
                double healed = Vitals.Parry();
                Style.Award(StyleBonus.Parry);
                _events.Add(new ProjectileReflected(p.Id));
                _events.Add(new Parried(ParryKind.Projectile, healed));
                return true;
            }
        }
        return false;
    }

    private void ParryEnemy(Enemy e, ParryKind kind)
    {
        double healed = Vitals.Parry();
        Style.Award(StyleBonus.Parry);
        e.State = EnemyState.Stunned;
        e.Timer = ParryStunSeconds;
        _events.Add(new Parried(kind, healed));
        DamageEnemy(e, ParryDamage * (kind == ParryKind.Shotgun ? 1.5 : 1.0), HitZone.Torso, e.Feet + new Vec3(0, 1, 0), "parry", false);
    }

    // ---- meteor (momentum kills) --------------------------------------------------------------

    private void UpdateMeteor(double dt, PlayerView view)
    {
        foreach (var hit in _meteor.Update(dt, Time, view, _enemies))
        {
            DamageEnemy(hit.Enemy, hit.Damage, HitZone.Torso, hit.Enemy.Feet + new Vec3(0, hit.Enemy.Spec.Height / 2, 0), "meteor", view.Airborne);
            double points = Style.Award(StyleBonus.Meteor, extraMultiplier: hit.LongFall ? MeteorTuning.LongFallMultiplier : 1.0);
            // Never touches Vitals: the impact cannot hurt the player. The host applies SpeedKept to the body.
            _events.Add(new MeteorImpact(hit.Enemy.Id, hit.Speed, hit.Damage, points, hit.LongFall, MeteorTuning.SpeedKept));
        }
    }

    // ---- enemies and projectiles ------------------------------------------------------------

    private void UpdateEnemy(Enemy e, double dt, PlayerView view)
    {
        if (e.Dead) return;

        var toPlayer = view.Feet - e.Feet;
        double flat = Math.Sqrt(toPlayer.X * toPlayer.X + toPlayer.Z * toPlayer.Z);

        switch (e.State)
        {
            case EnemyState.Stunned:
                e.Timer -= dt;
                if (e.Timer <= 0) e.State = EnemyState.Chasing;
                break;

            case EnemyState.Windup:
                e.Timer -= dt;
                if (e.Timer <= 0) ResolveAttack(e, view, flat);
                break;

            case EnemyState.Recover:
                e.Timer -= dt;
                if (e.Timer <= 0) e.State = EnemyState.Chasing;
                break;

            default:
                Chase(e, dt, view, toPlayer, flat);
                break;
        }
    }

    private void Chase(Enemy e, double dt, PlayerView view, Vec3 toPlayer, double flat)
    {
        var spec = e.Spec;

        if (spec.MeleeRange > 0 && flat <= spec.MeleeRange && Math.Abs(toPlayer.Y) < 2)
        {
            BeginWindup(e);
            return;
        }

        // Where to walk: straight at the player, or, if a portal makes the player closer, at the player's reflection
        // behind the portal's face, which carries the enemy through the portal and out beside them.
        var goal = view.Feet;
        bool viaPortal = false;
        if (Portals.Linked)
        {
            double direct = e.Feet.DistanceTo(view.Feet);
            foreach (var entrance in new[] { Portals.Teal!, Portals.Violet! })
            {
                var exit = Portals.Other(entrance)!;
                var mirrored = PortalTransform.Point(exit, entrance, view.Feet);
                double via = e.Feet.DistanceTo(mirrored);
                var face = entrance.Frame.Center + entrance.Frame.Normal * 0.5;
                if (via + 1.5 < direct && RayCast.HasLineOfSight(Blocks, e.Feet + new Vec3(0, 1.0, 0), face))
                {
                    goal = mirrored;
                    direct = via + 1.5;
                    viaPortal = true;
                }
            }
        }

        double gx = goal.X - e.Feet.X, gz = goal.Z - e.Feet.Z;
        double glen = Math.Sqrt(gx * gx + gz * gz);
        double dx = glen < 1e-6 ? 0 : gx / glen, dz = glen < 1e-6 ? 0 : gz / glen;

        if (!spec.IsRanged || viaPortal)
        {
            Walk(e, dt, dx, dz);
            return;
        }

        bool sees = RayCast.HasLineOfSight(Blocks, e.Feet + new Vec3(0, 1.4, 0), view.Eye);
        if (sees && flat >= spec.PreferredMin && flat <= spec.PreferredMax)
        {
            BeginWindup(e);
            return;
        }

        double dir = flat < spec.PreferredMin && sees ? -1 : 1;
        Walk(e, dt, dx * dir, dz * dir);
    }

    /// <summary>Moves an enemy, letting it walk through portal openings and come out of the other one.</summary>
    private void Walk(Enemy e, double dt, double dx, double dz)
    {
        var crossing = PortalWalker.Step(e.Body, Portals, dt, dx, dz, false, Blocks, new Vec3(dx, 0, dz), e.Spec.Speed);
        if (crossing is { } c) _events.Add(new PortalTraversed("enemy", c.Entered.Color));
    }

    private void BeginWindup(Enemy e)
    {
        e.State = EnemyState.Windup;
        e.Timer = e.Spec.WindupSeconds;
        _events.Add(new EnemyTelegraph(e.Id));
    }

    private void ResolveAttack(Enemy e, PlayerView view, double flat)
    {
        var spec = e.Spec;
        bool inMelee = spec.MeleeRange > 0 && flat <= spec.MeleeRange + 0.4 && Math.Abs(view.Feet.Y - e.Body.Y) < 2;
        if (inMelee)
        {
            HurtPlayer(spec.MeleeDamage);
        }
        else if (spec.IsRanged)
        {
            var origin = e.Feet + new Vec3(0, 1.4, 0);
            var aim = view.Eye + new Vec3(0, -0.4, 0);
            for (int i = 0; i < spec.Volley; i++)
            {
                var dir = (aim - origin).Normalized();
                if (spec.Volley > 1)
                {
                    double yaw = (i - (spec.Volley - 1) / 2.0) * 0.22;
                    double c = Math.Cos(yaw), s = Math.Sin(yaw);
                    dir = new Vec3(dir.X * c - dir.Z * s, dir.Y, dir.X * s + dir.Z * c).Normalized();
                }
                var p = new Projectile(_nextId++, origin, dir * ProjectileSpeed, spec.ProjectileDamage);
                _projectiles.Add(p);
                _events.Add(new ProjectileFired(p.Id, origin));
            }
        }

        e.State = EnemyState.Recover;
        e.Timer = spec.CooldownSeconds;
    }

    private void HurtPlayer(double damage)
    {
        double taken = Vitals.Damage(damage);
        if (taken <= 0) return;
        Style.OnDamaged();
        _events.Add(new PlayerHurt(taken, Vitals.Health));
        if (Vitals.Dead) _events.Add(new PlayerDied());
    }

    private void UpdateProjectiles(double dt, PlayerView view)
    {
        var playerBox = Box.AroundFeet(view.Feet, Physics.PlayerBody.HalfWidth + Projectile.Radius, Physics.PlayerBody.Height + Projectile.Radius);
        foreach (var p in _projectiles.ToList())
        {
            p.Age += dt;
            var next = p.Position + p.Velocity * dt;

            var seen = Portals.WithHoles(Blocks);
            bool hitWall = BlockInfo.IsSolid(seen((int)Math.Floor(next.X), (int)Math.Floor(next.Y), (int)Math.Floor(next.Z)));
            if (!hitWall && Portals.TryCross(p.Position, next, p.Velocity, p.Velocity) is { } through)
            {
                p.Position = through.Position;
                p.Velocity = through.Velocity.Normalized() * p.Velocity.Length;
                _events.Add(new PortalTraversed("projectile", through.Entered.Color));
                continue;
            }
            if (hitWall || p.Age > Projectile.LifetimeSeconds)
            {
                Remove(p, exploded: p.Reflected && hitWall, hitWall ? p.Position : next);
                if (p.Reflected && hitWall) Explode(p.Position, view);
                continue;
            }
            p.Position = next;

            if (!p.Reflected && playerBox.Contains(next))
            {
                HurtPlayer(p.Damage);
                Remove(p, exploded: false, next);
                continue;
            }

            if (p.Reflected)
            {
                var victim = _enemies.FirstOrDefault(e => !e.Dead && Inflate(e.Bounds, Projectile.Radius).Contains(next));
                if (victim != null)
                {
                    Remove(p, exploded: true, next);
                    Style.Award(StyleBonus.Chargeback);
                    Explode(next, view, directHit: victim);
                }
            }
        }
    }

    private void Remove(Projectile p, bool exploded, Vec3 at)
    {
        _projectiles.Remove(p);
        _events.Add(new ProjectileGone(p.Id, exploded, at));
    }

    private void Explode(Vec3 at, PlayerView view, Enemy? directHit = null)
    {
        foreach (var e in _enemies.ToList())
        {
            if (e.Dead) continue;
            double dist = (e.Feet + new Vec3(0, 0.9, 0)).DistanceTo(at);
            if (dist > ExplosionRadius && e != directHit) continue;
            DamageEnemy(e, ExplosionDamage * (e == directHit ? 1.0 : 1 - dist / (ExplosionRadius * 1.5)), HitZone.Torso, at, "explosion", view.Airborne);
        }
    }

    private static Box Inflate(Box b, double r) =>
        new(new Vec3(b.Min.X - r, b.Min.Y - r, b.Min.Z - r), new Vec3(b.Max.X + r, b.Max.Y + r, b.Max.Z + r));
}
