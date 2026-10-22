using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Portals;
using Hollowfall.Core.Progression;
using Hollowfall.Core.Relations;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Core.Combat;

/// <summary>Where the player is and what they are doing this tick. The host owns movement; the sim owns combat.</summary>
/// <remarks><paramref name="Velocity"/> and <paramref name="Crossed"/> (true on the step the player came out of a portal) drive the Meteor.</remarks>
public readonly record struct PlayerView(Vec3 Feet, Vec3 Eye, Vec3 Forward, bool Airborne, bool Sliding, Vec3 Velocity = default, bool Crossed = false);

/// <summary>Buttons pressed this tick (edges, not holds: the host applies fire-rate by calling once per press).</summary>
public readonly record struct PlayerActions(bool FireRevolver, bool FireShotgun, bool Parry, bool PortalTeal = false, bool PortalViolet = false, bool Interact = false, bool ClosePortals = false, bool FireMortar = false, bool UseCream = false);

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
    public const double SlamShockwaveRadius = 3.5;
    public const double SlamShockwaveDamage = 30;
    public const double AirSlamHeight = 8;
    public const double HollowfallWindowSeconds = 8;
    public const double ArrivalDelaySeconds = 0.5;

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
        public double Paused;
        public int Enemies;
        public FloorTargets Targets;
    }

    private readonly VoxelWorld _world;
    private readonly DungeonSite _site;
    private readonly SeededRandom _rng;
    private readonly SlamBreach _breach = new();
    private double _hollowfallUntil = -1;
    private CollapseSequence? _collapse;
    private readonly HashSet<int> _collapsedFloors = [];
    private readonly List<(int X, int Y, int Z)> _collapsedCells = [];
    private int _pendingFall = -1;
    private double _arrivalAt = -1;
    private bool _falling;
    private readonly List<Encounter> _encounters = [];
    private readonly FloorProgress[] _floors;
    private readonly List<Enemy> _enemies = [];
    public const int SurfaceFloor = -1;
    private const int VillageReach = 2;
    private readonly HashSet<(int X, int Z)> _villagesPopulated = [];
    private (int X, int Z)? _villageChunk;
    private readonly List<Projectile> _projectiles = [];
    private readonly List<MortarShell> _shells = [];
    private readonly List<CombatEvent> _events = [];
    private readonly HashSet<(int, int)> _dirtyChunks = [];
    private readonly Dictionary<string, double> _cooldowns = [];
    private readonly Interactables _items;
    private double _parryWindow, _parryCooldown;
    private readonly MeteorTracker _meteor = new();
    private int _nextId = 1;
    private int _creams;
    private readonly HashSet<int> _populated = [];

    /// <summary>
    /// The relationship system: factions, the player's reputation, and the memories of creatures. Null until
    /// <see cref="EnableRelations"/>; with none, every enemy chases as it always has and no creatures appear.
    /// </summary>
    public RelationSystem? Relations { get; private set; }

    // Pits: the last safe place the player stood, the fall being recovered from, and the hints already given this run.
    private Vec3? _safeSpot;
    private int _safeFloor = -1;
    private double _recoverIn = -1, _pitGrace;
    private bool _fellBefore, _edgeSeen;

    /// <summary>The player's portal pair. The host moves the player through it with <see cref="PortalWalker"/>.</summary>
    public PortalSystem Portals { get; } = new();

    public PlayerVitals Vitals { get; }

    /// <summary>The character's stats for this run, read once when it starts (see RunStats). Vigour sets the maximum health; Might scales the guns and parries.</summary>
    public RunStats Stats { get; }

    /// <summary>Jars of cream the run still has: the bag's count when the run started, less those used.</summary>
    public int CreamsLeft => _creams;

    /// <summary>Seconds left in the open parry window (0 when closed), which a Wide Parry perk lengthens.</summary>
    public double ParryWindowLeft => _parryWindow;

    /// <summary>Keys, chests and spike traps.</summary>
    public Interactables Items => _items;

    /// <summary>What the player has seen of each floor.</summary>
    public FloorMap Map { get; }
    public StyleMeter Style { get; } = new();
    public double Time { get; private set; }

    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<Projectile> Projectiles => _projectiles;
    public IReadOnlyList<MortarShell> Shells => _shells;

    public int CurrentFloor { get; private set; } = -1;
    public bool Dead => Vitals.Dead;

    public CombatSim(VoxelWorld world, DungeonSite site, RunStats stats = default)
    {
        _world = world;
        _site = site;
        Stats = stats;
        _creams = stats.ClampedCreams;
        Vitals = new PlayerVitals(stats.MaxHealth);
        _rng = new SeededRandom(world.Seed, site.RegionX, site.RegionZ, 700);
        _items = new Interactables(site.Spawns, world.Seed, d => { double before = Vitals.Health; HurtPlayer(d); return before - Vitals.Health; }, Vitals.Heal, _events.Add,
            cleanse: Vitals.Cleanse, boostStyle: m => Style.Award(StyleBonus.Parry, extraMultiplier: m));
        Map = new FloorMap(site);
        _floors = new FloorProgress[site.FloorCount];
        for (int k = 0; k < site.FloorCount; k++)
        {
            _floors[k] = new FloorProgress();
            BuildEncounters(k);
        }
    }

    /// <summary>
    /// Switches the relationship system on: creatures of the Folk, wanderers and vermin appear as their floors are reached,
    /// hurting and killing changes your standing, and witnesses remember. Call once, before play.
    /// </summary>
    public RelationSystem EnableRelations(Reputation? start = null)
    {
        Relations ??= new RelationSystem(_world.Seed, (a, b) => RayCast.HasLineOfSight(Blocks, a, b), _events.Add, start);
        foreach (var e in _enemies) Adopt(e);
        if (CurrentFloor >= 0) PopulateFloor(CurrentFloor);
        return Relations;
    }

    private Enemy Adopt(Enemy e)
    {
        if (Relations != null && e.Mind == null) e.Mind = Relations.CreateMind(e.Kind, e.Feet);
        return e;
    }

    private void PopulateFloor(int floor)
    {
        if (Relations == null || !_populated.Add(floor)) return;
        foreach (var sp in _site.Spawns.Where(s => s.Kind == SpawnKind.Creature && s.Floor == floor))
        {
            var enemy = Adopt(new Enemy(_nextId++, SpecFor((EnemyKind)sp.Variant, floor, sp.RoomId), new Vec3(sp.X + 0.5, sp.Y, sp.Z + 0.5), floor, sp.RoomId));
            _enemies.Add(enemy);
            _events.Add(new EnemySpawned(enemy.Id, enemy.Kind, enemy.Feet));
        }
    }

    /// <summary>The spec for an enemy of <paramref name="kind"/> in this room: the base spec, changed by the room's mutator (see <see cref="RoomMutators"/>).</summary>
    private EnemySpec SpecFor(EnemyKind kind, int floor, int roomId) => RoomMutators.Apply(EnemySpec.Of(kind), RoomMutators.For(_world.Seed, floor, roomId, kind));

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
        PopulateVillages(view);
        Style.Tick(dt);
        Style.NotifyMoving(view.Airborne || view.Sliding);
        Vitals.Tick(dt, Style.HardDamageSpeed);
        foreach (var key in _cooldowns.Keys.ToList()) _cooldowns[key] = Math.Max(0, _cooldowns[key] - dt);
        _parryCooldown = Math.Max(0, _parryCooldown - dt);

        if (Vitals.Dead) return;

        _falling = _pendingFall >= 0 && view.Airborne && view.Feet.Y < DungeonSite.FloorY(_pendingFall) + 0.9
            && (CurrentFloor == _pendingFall || CurrentFloor == _pendingFall + 1);
        if (_falling) _floors[_pendingFall].Paused += dt;
        TrackFloor(view);
        if (_pendingFall >= 0 && CurrentFloor == _pendingFall + 1 && !view.Airborne)
        {
            _pendingFall = -1;
            _arrivalAt = Time + ArrivalDelaySeconds;
        }
        if (_collapse != null && _collapse.Tick(dt, _world, (x, y, z) => MarkEdited(x, z, 0))) _collapse = null;
        TrackPits(dt, view);
        if (CurrentFloor >= 0) Map.Reveal(CurrentFloor, view.Feet.X, view.Feet.Z);
        _items.Tick(dt, view, actions.Interact);
        if (Relations != null)
        {
            Relations.Tick(dt);
            if (actions.Interact && !TryBoon(view)) TryOffering(view);
            SenseOmen(view);
        }
        Vitals.HitHealScale = _items.Perks.HealScale;
        TriggerEncounters(view);
        UpdateMeteor(dt, view);

        if (actions.Parry && _parryCooldown <= 0 && _parryWindow <= 0) _parryWindow = ParryWindowSeconds + _items.Perks.ParryWindowBonus;
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
        if (actions.ClosePortals) ClosePortals();

        if (actions.FireRevolver) Fire(WeaponSpec.Revolver, view);
        if (actions.FireShotgun) Fire(WeaponSpec.Shotgun, view);
        if (actions.FireMortar) FireMortar(view);
        if (actions.UseCream) UseCream();

        foreach (var e in _enemies.ToList()) UpdateEnemy(e, dt, view);
        UpdateProjectiles(dt, view);
        UpdateShells(dt, view);
        CheckEncounterClears();
        _enemies.RemoveAll(e => e.Dead);
    }

    /// <summary>The folk of each village stand at their doors once the player comes near it; they stay until the run restarts.</summary>
    private void PopulateVillages(PlayerView view)
    {
        if (Relations == null) return;
        int cx = (int)Math.Floor(view.Feet.X / Chunk.Size), cz = (int)Math.Floor(view.Feet.Z / Chunk.Size);
        if (_villageChunk == (cx, cz)) return;
        _villageChunk = (cx, cz);
        for (int dx = -VillageReach; dx <= VillageReach; dx++)
            for (int dz = -VillageReach; dz <= VillageReach; dz++)
                foreach (var plan in _world.Generator.Villages.Overlapping(cx + dx, cz + dz))
                {
                    if (!_villagesPopulated.Add((plan.CenterX, plan.CenterZ))) continue;
                    foreach (var (x, z) in plan.Residents)
                        SpawnEnemy(EnemyKind.Villager, new Vec3(x + 0.5, plan.Ground + 1, z + 0.5), SurfaceFloor);
                }
    }

    /// <summary>Adds an enemy outside any encounter, for tests, debugging and scripted fights.</summary>
    public Enemy SpawnEnemy(EnemyKind kind, Vec3 feet, int floor = 0, int roomId = -1)
    {
        var enemy = Adopt(new Enemy(_nextId++, SpecFor(kind, floor, roomId), feet, floor, roomId));
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
        int from = CurrentFloor;
        if (CurrentFloor >= 0 && floor > CurrentFloor) CompleteFloor(CurrentFloor);
        CurrentFloor = floor;
        Portals.SetFixed(_site.RiftPassages(floor));
        PopulateFloor(floor);

        var p = _floors[floor];
        if (!p.Started)
        {
            p.Started = true;
            p.StartTime = Time;
            p.StartStyle = Style.TotalScore;
        }

        if (from >= 0 && floor > from && view.Airborne && Time <= _hollowfallUntil && _encounters.Any(e => e.Floor == floor))
        {
            Style.Award(StyleBonus.Hollowfall);
            _hollowfallUntil = -1;
        }
    }

    /// <summary>
    /// Called on the step the player lands from a slam: shocks the enemies around the landing, awards the slam, and cracks or
    /// breaks the brick underfoot. <paramref name="fallBlocks"/> is how far the slam dropped.
    /// </summary>
    public void Slam(Vec3 feet, double fallBlocks)
    {
        if (Vitals.Dead) return;

        Style.Award(fallBlocks > AirSlamHeight ? StyleBonus.AirSlam : StyleBonus.GroundSlam);
        foreach (var e in _enemies.ToList())
        {
            if (e.Dead || e.Feet.DistanceTo(feet) > SlamShockwaveRadius) continue;
            DamageEnemy(e, SlamShockwaveDamage * Stats.DamageScale, HitZone.Torso, e.Feet + new Vec3(0, 1, 0), "slam", false);
        }

        int ux = (int)Math.Floor(feet.X), uy = (int)Math.Floor(feet.Y - 1), uz = (int)Math.Floor(feet.Z);
        var result = _breach.Land(_world, ux, uy, uz, Time);
        if (result != FloorSlam.None) MarkEdited(ux, uz, SlamBreach.HoleRadius);
        if (result == FloorSlam.Broken) _hollowfallUntil = Time + HollowfallWindowSeconds;
    }

    /// <summary>Queues the chunks around a block edit for re-meshing, including the neighbour whose border faces it changes.</summary>
    private void MarkEdited(int x, int z, int reach)
    {
        for (int cx = (x - reach - 1) >> 4; cx <= (x + reach + 1) >> 4; cx++)
            for (int cz = (z - reach - 1) >> 4; cz <= (z + reach + 1) >> 4; cz++) _dirtyChunks.Add((cx, cz));
    }

    private void CompleteFloor(int floor)
    {
        var p = _floors[floor];
        if (p.Completed || !p.Started) return;
        p.Completed = true;
        p.FinishTime = Time;
        var result = FloorRanking.Score(p.Targets, Time - p.StartTime - p.Paused, p.Kills, Style.TotalScore - p.StartStyle, p.Restarts);
        _events.Add(new FloorCompleted(floor, result, Time - p.StartTime - p.Paused));
    }

    // ---- pits ---------------------------------------------------------------------------------

    /// <summary>Health a fall costs: a little on bare stone, more on spikes, none in water (where the cost is the long climb out).</summary>
    public static double FallDamage(GapHazard hazard) => hazard switch { GapHazard.Spikes => 20, GapHazard.Bare => 8, _ => 0 };

    /// <summary>Seconds before the player is put back on the ledge: a stumble on stone or spikes, a long swim in water.</summary>
    public static double RecoverySeconds(GapHazard hazard) => hazard == GapHazard.Water ? 1.6 : 0.4;

    private const double RecoveryGraceSeconds = 0.5;

    private static bool IsPit(CellKind k) => k is CellKind.Pit or CellKind.PitSpikes or CellKind.PitWater;

    private static GapHazard HazardOf(CellKind k) => k == CellKind.PitSpikes ? GapHazard.Spikes : k == CellKind.PitWater ? GapHazard.Water : GapHazard.Bare;

    /// <summary>
    /// Keeps the player from being stuck in a pit. Falling in costs a little health (never the last of it), then after a moment the
    /// player is put back at the last safe place they stood, on their own side of the gap. Also notes the first time the player
    /// stands at the lip of a gap, and where it is safe to put them back.
    /// </summary>
    private void TrackPits(double dt, PlayerView view)
    {
        int floor = _site.FloorIndexAt(view.Feet.X, view.Feet.Y + 0.1, view.Feet.Z);
        if (floor < 0) return;
        var layout = _site.Floors[floor];
        int cx = (int)Math.Floor(view.Feet.X) - _site.OriginX, cz = (int)Math.Floor(view.Feet.Z) - _site.OriginZ;
        var kind = layout.CellAt(cx, cz);
        double lip = DungeonSite.FloorY(floor) + 1 + LinkMix.Rise;
        bool inPit = IsPit(kind) && view.Feet.Y < lip - 0.6;
        _pitGrace = Math.Max(0, _pitGrace - dt);

        if (_recoverIn >= 0)
        {
            // Climbed out some other way (a portal on the pit wall, say): nothing to recover from.
            if (!inPit) { _recoverIn = -1; return; }
            _recoverIn -= dt;
            if (_recoverIn <= 0)
            {
                _recoverIn = -1;
                _pitGrace = RecoveryGraceSeconds;
                _events.Add(new PlayerRecovered(SafeSpotFor(floor, cx, cz)));
            }
            return;
        }

        // The host moves the body on the next frame: do not count the same fall twice while it catches up.
        if (inPit && _pitGrace > 0) return;

        if (inPit)
        {
            var hazard = HazardOf(kind);
            double damage = Math.Min(FallDamage(hazard), Vitals.Health - 1); // a fall never kills
            if (damage > 0) HurtPlayer(damage);
            _events.Add(new PlayerFell(hazard, Math.Max(0, damage), !_fellBefore));
            _fellBefore = true;
            _recoverIn = RecoverySeconds(hazard);
            return;
        }

        if (view.Airborne) return;
        double surface = DungeonSite.FloorY(floor) + 1 + layout.HeightAt(cx, cz);
        if (kind is CellKind.Floor or CellKind.Door && Math.Abs(view.Feet.Y - surface) < 0.3)
        {
            // Not at the very lip, so a recovery never leaves the player a step from falling again.
            bool nearPit = false;
            for (int dz = -1; dz <= 1 && !nearPit; dz++)
                for (int dx = -1; dx <= 1 && !nearPit; dx++)
                    nearPit = IsPit(layout.CellAt(cx + dx, cz + dz));
            if (!nearPit)
            {
                _safeSpot = new Vec3(_site.OriginX + cx + 0.5, surface, _site.OriginZ + cz + 0.5);
                _safeFloor = floor;
            }

            if (!_edgeSeen && layout.GapAt(cx, cz) is { } gap && ((cx, cz) == gap.EdgeFrom || (cx, cz) == gap.EdgeTo))
            {
                _edgeSeen = true;
                _events.Add(new PitEdgeReached(gap.Kind));
            }
        }
    }

    /// <summary>Where to put a player who fell in at a cell: the last safe place they stood, or failing that the hallway the pit belongs to.</summary>
    private Vec3 SafeSpotFor(int floor, int cx, int cz)
    {
        if (_safeSpot is { } spot && _safeFloor == floor) return spot;
        var layout = _site.Floors[floor];
        var gap = layout.GapAt(cx, cz) ?? layout.Gaps.OrderBy(g => Math.Abs(g.Span[0].X - cx) + Math.Abs(g.Span[0].Z - cz)).First();
        // The hallway end nearer the player, two cells back from the lip.
        bool fromSide = Math.Abs(gap.EdgeFrom.X - cx) + Math.Abs(gap.EdgeFrom.Z - cz) <= Math.Abs(gap.EdgeTo.X - cx) + Math.Abs(gap.EdgeTo.Z - cz);
        var hall = gap.Hall(fromSide);
        var cell = hall[Math.Max(0, hall.Count - 3)];
        return new Vec3(_site.OriginX + cell.X + 0.5, DungeonSite.FloorY(floor) + 1 + layout.HeightAt(cell.X, cell.Z), _site.OriginZ + cell.Z + 0.5);
    }

    private void TriggerEncounters(PlayerView view)
    {
        if (CurrentFloor < 0) return;
        var layout = _site.Floors[CurrentFloor];
        int lx = (int)Math.Floor(view.Feet.X) - _site.OriginX, lz = (int)Math.Floor(view.Feet.Z) - _site.OriginZ;
        int room = layout.RoomIdAt(lx, lz);
        if (room < 0 || Time < _arrivalAt) return;

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
            var kind = sp.Kind == SpawnKind.Boss ? EnemyKind.Warden : HollowKindAt(sp.X, sp.Z);
            var enemy = Adopt(new Enemy(_nextId++, SpecFor(kind, enc.Floor, enc.RoomId), new Vec3(sp.X + 0.5, sp.Y, sp.Z + 0.5), enc.Floor, enc.RoomId));
            _enemies.Add(enemy);
            enc.Enemies.Add(enemy);
            _events.Add(new EnemySpawned(enemy.Id, kind, enemy.Feet));
        }
        if (Relations is { OmenArmed: true } relations) SpringOmen(enc, relations);
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

            bool bossCleared = _site.Floors[enc.Floor].BossRoom?.Id == enc.RoomId;
            if (enc.Floor < _site.FloorCount - 1 && !_collapsedFloors.Contains(enc.Floor)
                && (bossCleared || _encounters.Where(e => e.Floor == enc.Floor).All(e => e.Cleared)))
                StartCollapse(enc);
        }
    }

    /// <summary>The floor under a cleared boss room or last arena gives way (docs/features.md 3.3).</summary>
    private void StartCollapse(Encounter enc)
    {
        var room = _site.Floors[enc.Floor].Rooms.First(r => r.Id == enc.RoomId);
        _collapse = CollapseSequence.Plan(_world, enc.Floor, _site.OriginX + room.CenterX, _site.OriginZ + room.CenterZ);
        _collapsedFloors.Add(enc.Floor);
        _collapsedCells.AddRange(_collapse.Cells);
        _pendingFall = enc.Floor;
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
        _villagesPopulated.Clear();
        _villageChunk = null;
        _projectiles.Clear();
        _shells.Clear();
        if (Relations != null)
        {
            Relations.OmenArmed = false;
            _populated.Clear();
            if (CurrentFloor >= 0) PopulateFloor(CurrentFloor);
        }
        Vitals.Reset();
        Style.Reset();
        // The meter restarts from zero, so unfinished floors measure style from zero too, and kills in rooms that must be
        // fought again no longer count (otherwise the HUD could show more kills than the floor has enemies).
        foreach (var progress in _floors.Where(f => !f.Completed)) progress.StartStyle = 0;
        for (int f = 0; f < _floors.Length; f++)
            if (!_floors[f].Completed)
                _floors[f].Kills = _encounters.Where(e => e.Floor == f && e.Cleared).Sum(e => e.Enemies.Count(en => en.Dead));
        Portals.Clear();
        _safeSpot = null;
        _recoverIn = -1;
        _pitGrace = 0;
        _parryWindow = _parryCooldown = 0;
        _meteor.Reset();
        _collapse = null;
        _pendingFall = -1;
        _falling = false;
        _arrivalAt = -1;
        foreach (var (x, y, z) in _collapsedCells)
        {
            _world.SetBlock(x, y, z, _site.BlockAt(x, y, z) ?? BlockType.Air);
            MarkEdited(x, z, 0);
        }
        _collapsedCells.Clear();
    }

    // ---- weapons ----------------------------------------------------------------------------

    public const double MortarCooldownSeconds = 1.4;

    private void FireMortar(PlayerView view)
    {
        if (_cooldowns.GetValueOrDefault("mortar") > 0) return;
        _cooldowns["mortar"] = MortarCooldownSeconds;
        Style.NotifyWeaponUsed("mortar");
        _shells.Add(new MortarShell(_nextId++, view.Eye, view.Forward.Normalized() * MortarShell.Speed));
    }

    private void UpdateShells(double dt, PlayerView view)
    {
        foreach (var s in _shells.ToList())
        {
            s.Age += dt;
            s.Velocity -= new Vec3(0, MortarShell.Gravity * dt, 0);
            var next = s.Position + s.Velocity * dt;

            var hitEnemy = _enemies.FirstOrDefault(e => !e.Dead && e.Mind is not { Disposition: Disposition.Allied } && Inflate(e.Bounds, Projectile.Radius).Contains(next));
            if (hitEnemy == null && s.Age <= MortarShell.LifetimeSeconds && !BlockInfo.IsSolid(Blocks((int)Math.Floor(next.X), (int)Math.Floor(next.Y), (int)Math.Floor(next.Z))))
            {
                s.Position = next;
                continue;
            }

            Detonate(s, hitEnemy != null ? next : s.Position, view);
        }
    }

    private void Detonate(MortarShell shell, Vec3 at, PlayerView view)
    {
        _shells.Remove(shell);
        if (MortarBlast.Carve(_world, at) > 0) MarkEdited((int)Math.Floor(at.X), (int)Math.Floor(at.Z), (int)Math.Ceiling(MortarBlast.Radius));
        foreach (var e in _enemies.ToList())
        {
            if (e.Dead || e.Mind is { Disposition: Disposition.Allied }) continue;
            double dist = (e.Feet + new Vec3(0, 0.9, 0)).DistanceTo(at);
            if (dist > MortarBlast.Radius) continue;
            DamageEnemy(e, MortarBlast.DamageAt(dist) * Stats.DamageScale, HitZone.Torso, at, "mortar", view.Airborne);
        }
        _events.Add(new ProjectileGone(shell.Id, Exploded: true, at));
    }

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
                    if (e.Dead || e.Mind is { Disposition: Disposition.Allied }) continue; // shots pass through friends
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
            double damage = weapon.Damage * mult * Stats.DamageScale;

            if (weapon.Pellets > 1 && distance <= ShotgunParryReach && target.Telegraphing && !parried)
            {
                parried = true;
                ParryEnemy(target, ParryKind.Shotgun);
            }

            Vitals.HealFromHit(zone, distance, weapon.PelletHeal);
            bool firstOnThisEnemy = hitThisShot.Add(target.Id);
            if (firstOnThisEnemy && !Innocent(target))
            {
                if (zone == HitZone.Head) Style.Award(StyleBonus.Headshot, weapon.Name);
                else if (zone == HitZone.Limb) Style.Award(StyleBonus.LimbHit, weapon.Name);
            }
            DamageEnemy(target, damage, zone, end, weapon.Name, view.Airborne);
        }
    }

    private void FirePortal(PortalColor color, PlayerView view)
    {
        var replaced = Portals.Get(color);
        int floor = _site.FloorIndexAt(view.Feet.X, view.Feet.Y + 0.1, view.Feet.Z);
        var result = Portals.Place(color, view.Eye, view.Forward, view.Forward, Blocks, platesOnly: floor >= DungeonSite.RiftPlateFloor);
        var p = result.Portal;
        if (result.Placed && replaced != null) Close(replaced);
        _events.Add(new PortalPlaced(color, result.Placed,
            p?.Frame.Center ?? view.Eye, p?.Frame.Normal ?? new Vec3(0, 0, 0), p?.Frame.Up ?? new Vec3(0, 1, 0), result.Failure));
    }

    /// <summary>Removes one portal on purpose (the player, a pact or a boss) and severs anything standing in it.</summary>
    public bool ClosePortal(PortalColor color)
    {
        if (Portals.Get(color) is not { } portal) return false;
        Portals.Remove(color);
        Close(portal);
        return true;
    }

    /// <summary>Closes both portals, severing whatever stands in either opening.</summary>
    /// <summary>Closes both portals. Rift openings cannot be closed, but a close that takes a placed portal also cuts whoever stands in them.</summary>
    public bool ClosePortals()
    {
        bool closed = ClosePortal(PortalColor.Teal) | ClosePortal(PortalColor.Violet);
        if (!closed) return false;
        foreach (var rift in Portals.Rifts) SeverRules.Apply(rift, _enemies, Style, _events.Add, KillSevered);
        return true;
    }

    private void Close(Portal old)
    {
        _events.Add(new PortalClosed(old.Color, old.Frame.Center, old.Frame.Normal, old.Frame.Up));
        SeverRules.Apply(old, _enemies, Style, _events.Add, KillSevered);
    }

    private void KillSevered(Enemy e)
    {
        if (!IsCreature(e)) _floors[e.Floor].Kills++;
        Relations?.PlayerHurt(e, e.Spec.Health, true, _enemies, Time);
        OnDeath(e);
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
        Relations?.PlayerHurt(e, damage, killed, _enemies, Time);
        if (!killed) return;

        e.State = EnemyState.Dead;
        if (!IsCreature(e)) _floors[e.Floor].Kills++;
        if (!IsCreature(e) || e.Mind is { Species.Pacifist: false }) Style.RegisterKill(weapon, airborne);
        _events.Add(new EnemyKilled(e.Id, e.Kind));
        OnDeath(e);
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
        DamageEnemy(e, ParryDamage * (kind == ParryKind.Shotgun ? 1.5 : 1.0) * Stats.DamageScale, HitZone.Torso, e.Feet + new Vec3(0, 1, 0), "parry", false);
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

        // With the relationship system on, creatures decide what to do from their disposition; the Hollow's own never do.
        bool creature = IsCreature(e);
        if (creature) Think(e, dt, view, flat);

        switch (e.State)
        {
            case EnemyState.Stunned:
                e.Timer -= dt;
                if (e.Timer <= 0) e.State = EnemyState.Chasing;
                break;

            case EnemyState.Windup:
                e.Timer -= dt;
                if (creature && e.Mind!.Intent != Intent.Attack) e.State = EnemyState.Chasing; // it thought better of it
                else if (e.Timer <= 0) ResolveAttack(e, view, flat);
                break;

            case EnemyState.Recover:
                e.Timer -= dt;
                if (e.Timer <= 0) e.State = EnemyState.Chasing;
                break;

            default:
                if (creature) Act(e, dt, view, toPlayer, flat);
                else Chase(e, dt, view, toPlayer, flat);
                break;
        }
    }

    // ---- creatures (the relationship system) ---------------------------------------------------

    public const double AllyStrikeDamage = 10;
    public const double AllyStrikeCooldown = 1.1;
    public const double AllyStrikeReach = 1.9;
    public const double BoonReach = 2.8;
    public const double BoonHeal = 25;

    /// <summary>Whether an enemy is one of the Folk, a wanderer or vermin: it thinks for itself and is not part of any encounter.</summary>
    private bool IsCreature(Enemy e) => Relations != null && e.Mind is { Species.AlwaysHostile: false };

    private void Think(Enemy e, double dt, PlayerView view, double flat)
    {
        var m = e.Mind!;
        double dist = e.Feet.DistanceTo(view.Feet);
        double approach = double.IsNaN(m.LastDistance) || dt <= 0 ? 0 : (m.LastDistance - dist) / dt;
        m.LastDistance = dist;
        m.StrikeCooldown = Math.Max(0, m.StrikeCooldown - dt);
        bool sees = dist <= 40 && RayCast.HasLineOfSight(Blocks, e.Feet + new Vec3(0, 1.0, 0), view.Eye);
        double fromHome = Math.Sqrt(Math.Pow(view.Feet.X - m.Home.X, 2) + Math.Pow(view.Feet.Z - m.Home.Z, 2));
        double hostile = m.Disposition == Disposition.Allied && NearestHostile(e, Brain.AssistRange) is { } h ? e.Feet.DistanceTo(h.Feet) : double.PositiveInfinity;

        var events = m.Update(dt, Relations!.Reputation.Tier(m.Faction), new Perception(dist, approach, sees, fromHome, hostile));
        if (events.HasFlag(MindEvents.Provoked)) _events.Add(new EntityProvoked(e.Id, m.Faction));
        if (events.HasFlag(MindEvents.Calmed)) _events.Add(new EntityCalmed(e.Id, m.Faction));
        if (events.HasFlag(MindEvents.Allied)) _events.Add(new EntityAllied(e.Id, m.Faction));
    }

    private Enemy? NearestHostile(Enemy from, double range)
    {
        Enemy? best = null;
        double bestDistance = range;
        foreach (var other in _enemies)
        {
            if (other.Dead || other == from || other.Mind is not { } om || FactionGraph.Between(om.Faction, Faction.Delver) != Stance.Enemy) continue;
            double d = from.Feet.DistanceTo(other.Feet);
            if (d < bestDistance && RayCast.HasLineOfSight(Blocks, from.Feet + new Vec3(0, 1.0, 0), other.Feet + new Vec3(0, 1.0, 0))) { best = other; bestDistance = d; }
        }
        return best;
    }

    private void Act(Enemy e, double dt, PlayerView view, Vec3 toPlayer, double flat)
    {
        var m = e.Mind!;
        switch (m.Intent)
        {
            case Intent.Attack:
                if (e.Spec.MeleeDamage > 0 || e.Spec.IsRanged) Chase(e, dt, view, toPlayer, flat);
                break;

            case Intent.Flee:
                Flee(e, dt, view);
                break;

            case Intent.Follow:
                if (flat > 0.1) Walk(e, dt, toPlayer.X / flat, toPlayer.Z / flat, 0.9);
                break;

            case Intent.Assist:
                Assist(e, dt);
                break;

            case Intent.Warn:
                break; // squared up, holding its ground

            default:
                Wander(e, dt);
                break;
        }
    }

    private void Flee(Enemy e, double dt, PlayerView view)
    {
        double dx = e.Feet.X - view.Feet.X, dz = e.Feet.Z - view.Feet.Z;
        double len = Math.Sqrt(dx * dx + dz * dz);
        if (len < 1e-6) { dx = 1; dz = 0; len = 1; }
        dx /= len;
        dz /= len;

        var before = e.Feet;
        Walk(e, dt, dx, dz, 1.25);
        if (e.Feet.DistanceTo(before) >= 0.25 * e.Spec.Speed * dt) return;

        // Cornered against a wall: slide along it, one way and then the other.
        foreach (double angle in new[] { 1.2, -1.2 })
        {
            double c = Math.Cos(angle), s = Math.Sin(angle);
            Walk(e, dt, dx * c - dz * s, dx * s + dz * c, 1.25);
            if (e.Feet.DistanceTo(before) >= 0.25 * e.Spec.Speed * dt) return;
        }
    }

    private void Wander(Enemy e, double dt)
    {
        var m = e.Mind!;
        int slot = (int)Math.Floor(Time / 3.0 + e.Id * 1.7);
        if ((slot & 1) == 1) return; // pausing

        double hx = m.Home.X - e.Feet.X, hz = m.Home.Z - e.Feet.Z;
        double home = Math.Sqrt(hx * hx + hz * hz);
        if (home > 3.5) { Walk(e, dt, hx / home, hz / home, 0.4); return; }
        double angle = Terrain.SeedHash.ToUnit(Terrain.SeedHash.Hash(_world.Seed, e.Id, slot, 941)) * Math.PI * 2;
        Walk(e, dt, Math.Cos(angle), Math.Sin(angle), 0.35);
    }

    private void Assist(Enemy e, double dt)
    {
        if (NearestHostile(e, Brain.AssistRange) is not { } target) return;
        double dx = target.Feet.X - e.Feet.X, dz = target.Feet.Z - e.Feet.Z;
        double flat = Math.Sqrt(dx * dx + dz * dz);
        if (flat > AllyStrikeReach * 0.8) { Walk(e, dt, dx / flat, dz / flat, 1.1); return; }
        if (e.Mind!.StrikeCooldown > 0) return;
        e.Mind.StrikeCooldown = AllyStrikeCooldown;

        // An ally's blow is not the player's: no blood, no style, no change of standing.
        target.Health -= AllyStrikeDamage;
        bool killed = target.Health <= 0;
        _events.Add(new EnemyHit(target.Id, HitZone.Torso, AllyStrikeDamage, killed, target.Feet + new Vec3(0, target.Spec.Height / 2, 0)));
        if (!killed) return;
        target.State = EnemyState.Dead;
        if (!IsCreature(target)) _floors[target.Floor].Kills++;
        _events.Add(new EnemyKilled(target.Id, target.Kind));
        OnDeath(target);
    }

    /// <summary>Hurting a pacifist earns no style: there is no flair in it.</summary>
    private bool Innocent(Enemy e) => IsCreature(e) && e.Mind!.Species.Pacifist;

    private void OnDeath(Enemy e)
    {
        // A Bean Nighe who can no longer wring out her cloth warns of nothing.
        if (Relations != null && e.Mind is { Species.Kind: EnemyKind.BeanNighe }) Relations.OmenArmed = false;
        if (_falling) Style.Award(StyleBonus.Freefall);
    }

    /// <summary>Interact beside a Brownie or villager while its folk think well of you: it repays you once with a warm hearth or a jar of cream.</summary>
    private bool TryBoon(PlayerView view)
    {
        foreach (var e in _enemies)
        {
            if (e.Dead || e.Mind is not { Species.Kind: EnemyKind.Brownie or EnemyKind.Villager, BoonGiven: false } m) continue;
            if (Relations!.Reputation.Tier(m.Faction) < ReputationTier.Friendly) continue;
            double dx = e.Feet.X - view.Feet.X, dz = e.Feet.Z - view.Feet.Z;
            if (Math.Sqrt(dx * dx + dz * dz) > BoonReach || Math.Abs(e.Feet.Y - view.Feet.Y) > 2) continue;
            m.BoonGiven = true;
            m.Memory.Gift();
            double healed = Vitals.Heal(BoonHeal);
            Vitals.Cleanse();
            _events.Add(new BoonGiven(e.Id, "warmth", healed));
            return true;
        }
        return false;
    }

    public const double OfferingCost = 10;

    /// <summary>
    /// Interact beside a creature that can fight at your side, while it is not afraid or angry and the Folk think well of you (Friendly or better): you give it a
    /// little of your blood, once, and it remembers the gift (standing and gratitude rise, which can make it an ally).
    /// </summary>
    private void TryOffering(PlayerView view)
    {
        if (Vitals.Health <= OfferingCost + 15) return;
        foreach (var e in _enemies)
        {
            if (e.Dead || e.Mind is not { Species.CanAlly: true, Offered: false, Provoked: false, Scared: false } m) continue;
            if (Relations!.Reputation.Tier(m.Faction) < ReputationTier.Friendly) continue;
            double dx = e.Feet.X - view.Feet.X, dz = e.Feet.Z - view.Feet.Z;
            if (Math.Sqrt(dx * dx + dz * dz) > BoonReach || Math.Abs(e.Feet.Y - view.Feet.Y) > 2) continue;
            m.Offered = true;
            Vitals.Damage(OfferingCost);
            Relations.PlayerGave(e, Time);
            _events.Add(new OfferingAccepted(e.Id, m.Faction));
            return;
        }
    }

    /// <summary>A Bean Nighe near the player is a warning: the next encounter will hold an extra wave.</summary>
    private void SenseOmen(PlayerView view)
    {
        if (Relations!.OmenArmed) return;
        foreach (var e in _enemies)
        {
            if (e.Dead || e.Mind is not { Species.Kind: EnemyKind.BeanNighe } || e.Floor != CurrentFloor) continue;
            double dx = e.Feet.X - view.Feet.X, dz = e.Feet.Z - view.Feet.Z;
            if (Math.Sqrt(dx * dx + dz * dz) > RelationSystem.OmenRadius) continue;
            Relations.OmenArmed = true;
            _events.Add(new OmenSensed(e.Id));
            return;
        }
    }

    private void SpringOmen(Encounter enc, RelationSystem relations)
    {
        relations.OmenArmed = false;
        var layout = _site.Floors[enc.Floor];
        var room = layout.Rooms[enc.RoomId];
        var used = enc.Spawns.Select(sp => (sp.X, sp.Z)).ToHashSet();
        var cells = new List<(int X, int Z)>();
        for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
            for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
                if (layout.CellAt(x, z) == CellKind.Floor && layout.RoomIdAt(x, z) == room.Id && !SpawnPlanner.NearStairs(layout, x, z) && !used.Contains((_site.OriginX + x, _site.OriginZ + z)))
                    cells.Add((x, z));
        new SeededRandom(_world.Seed, enc.Floor, enc.RoomId, 950).Shuffle(cells);

        int extra = Math.Min(cells.Count, Math.Clamp((enc.Spawns.Count + 1) / 2 + 1, 2, 4));
        for (int i = 0; i < extra; i++)
        {
            var (wx, wy, wz) = _site.ToWorld(enc.Floor, cells[i].X, cells[i].Z);
            var kind = HollowKindAt(wx, wz);
            var enemy = Adopt(new Enemy(_nextId++, SpecFor(kind, enc.Floor, enc.RoomId), new Vec3(wx + 0.5, wy, wz + 0.5), enc.Floor, enc.RoomId));
            _enemies.Add(enemy);
            enc.Enemies.Add(enemy);
            _events.Add(new EnemySpawned(enemy.Id, kind, enemy.Feet));
        }
        _floors[enc.Floor].Enemies += extra;
        _events.Add(new OmenFulfilled(enc.Floor, enc.RoomId, extra));
    }

    private void Chase(Enemy e, double dt, PlayerView view, Vec3 toPlayer, double flat)
    {
        var spec = e.Spec;

        if (spec.MeleeRange > 0 && flat <= spec.MeleeRange && Math.Abs(toPlayer.Y) < 2)
        {
            if (HasAttackSlot(e)) BeginWindup(e);
            else Circle(e, dt, toPlayer, flat);
            return;
        }

        // Where to walk: straight at the player, or, if a portal makes the player closer, at the player's reflection
        // behind the portal's face, which carries the enemy through the portal and out beside them.
        var goal = view.Feet;
        bool viaPortal = false;
        double direct = e.Feet.DistanceTo(view.Feet);
        foreach (var entrance in Portals.Openings.ToArray())
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
            if (HasAttackSlot(e)) BeginWindup(e);
            else Circle(e, dt, toPlayer, flat);
            return;
        }

        double dir = flat < spec.PreferredMin && sees ? -1 : 1;
        Walk(e, dt, dx * dir, dz * dir);
    }

    /// <summary>A room lets only this many enemies wind up at once, so a crowd takes its turns instead of swarming the player.</summary>
    public const int AttackSlots = 2;

    /// <summary>The Hollow kind on a cell: odd cells are Spitters, a quarter of the even cells are Cù-sìth, the rest Husks.</summary>
    public static EnemyKind HollowKindAt(int x, int z) =>
        (x + z) % 2 != 0 ? EnemyKind.Spitter : (x + z) / 2 % 4 == 0 ? EnemyKind.CuSith : EnemyKind.Husk;

    private bool HasAttackSlot(Enemy e) =>
        _enemies.Count(o => !o.Dead && o.State == EnemyState.Windup && o.Floor == e.Floor && o.RoomId == e.RoomId) < AttackSlots;

    /// <summary>Waiting for a slot: walks sideways around the player at a slower pace, keeping its distance, instead of standing still.</summary>
    private void Circle(Enemy e, double dt, Vec3 toPlayer, double flat)
    {
        if (flat < 1e-6) return;
        double side = e.Id % 2 == 0 ? 1 : -1;
        Walk(e, dt, -toPlayer.Z / flat * side, toPlayer.X / flat * side, 0.6);
    }

    /// <summary>Moves an enemy, letting it walk through portal openings and come out of the other one.</summary>
    private void Walk(Enemy e, double dt, double dx, double dz, double speedScale = 1)
    {
        (dx, dz) = KeepOffPits(e, dx, dz);
        var crossing = PortalWalker.Step(e.Body, Portals, dt, dx, dz, false, Blocks, new Vec3(dx, 0, dz), e.Spec.Speed * speedScale);
        if (crossing is { } c) _events.Add(new PortalTraversed("enemy", c.Entered.Color));
    }

    /// <summary>Enemies stop at the edge of a pit instead of walking into it: the part of a step that would take them over is dropped.</summary>
    private (double Dx, double Dz) KeepOffPits(Enemy e, double dx, double dz)
    {
        if (e.Floor == SurfaceFloor) return (dx, dz);
        var layout = _site.Floors[e.Floor];
        double reach = e.Spec.HalfWidth + 0.4;
        CellKind At(double x, double z) => layout.CellAt((int)Math.Floor(x) - _site.OriginX, (int)Math.Floor(z) - _site.OriginZ);
        double x0 = e.Feet.X, z0 = e.Feet.Z;
        if (IsPit(At(x0, z0))) return (dx, dz); // already in one: nothing to protect
        if (dx != 0 && IsPit(At(x0 + Math.Sign(dx) * reach, z0))) dx = 0;
        if (dz != 0 && IsPit(At(x0, z0 + Math.Sign(dz) * reach))) dz = 0;
        if (dx != 0 && dz != 0 && IsPit(At(x0 + Math.Sign(dx) * reach, z0 + Math.Sign(dz) * reach))) dx = dz = 0;
        return (dx, dz);
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
        double taken = Vitals.Damage(damage * (1 - Stats.WardFraction));
        if (taken <= 0) return;
        Style.OnDamaged();
        _events.Add(new PlayerHurt(taken, Vitals.Health));
        if (Vitals.Dead) _events.Add(new PlayerDied());
    }

    /// <summary>Eats a jar of cream to heal, if the run has one and the player is hurt. A jar is not used up at full health.</summary>
    private void UseCream()
    {
        if (_creams <= 0 || Vitals.Dead || Vitals.Health >= Vitals.Ceiling) return;
        _creams--;
        _events.Add(new CreamUsed(Vitals.Heal(Inventory.CreamHeal)));
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
            if (e.Dead || e.Mind is { Disposition: Disposition.Allied }) continue;
            double dist = (e.Feet + new Vec3(0, 0.9, 0)).DistanceTo(at);
            if (dist > ExplosionRadius && e != directHit) continue;
            DamageEnemy(e, ExplosionDamage * (e == directHit ? 1.0 : 1 - dist / (ExplosionRadius * 1.5)), HitZone.Torso, at, "explosion", view.Airborne);
        }
    }

    private static Box Inflate(Box b, double r) =>
        new(new Vec3(b.Min.X - r, b.Min.Y - r, b.Min.Z - r), new Vec3(b.Max.X + r, b.Max.Y + r, b.Max.Z + r));
}
