namespace Hollowfall.Core.Dungeon;

/// <summary>
/// One dungeon floor: a 96x96 grid of cells holding rooms joined by 3-wide corridors.
/// Built from a BSP split (so rooms never overlap), joined along the split tree (so every
/// room is reachable), then given extra loops, some behind secret doors. Entirely a pure
/// function of its inputs; it knows nothing about voxels or chunks.
/// </summary>
public sealed partial class FloorLayout
{
    public const int Size = 96;
    private const int MinLeaf = 20;
    private const int MaxDepth = 4;
    private const int AnchorClear = 6;

    private readonly CellKind[] _cells = new CellKind[Size * Size];
    private readonly short[] _roomAt = new short[Size * Size];
    private readonly List<Room> _rooms = [];
    private readonly List<RoomLink> _links = [];

    // Mixed links: how many corridors dug each cell, the height of each cell's floor above the room floor, the route of each
    // link, and the gaps built on them.
    private readonly byte[] _digs = new byte[Size * Size];
    private readonly byte[] _height = new byte[Size * Size];
    private readonly bool[] _reserved = new bool[Size * Size];
    private readonly List<LinkRoute> _routes = [];
    private readonly List<GapSpec> _gaps = [];

    public int Floor { get; }

    /// <summary>Where this floor is entered from above (surface for floor 0), in layout cells.</summary>
    public int EntranceX { get; }
    public int EntranceZ { get; }

    /// <summary>Where the stairs down start, or null on the last floor.</summary>
    public (int X, int Z)? ExitStairs { get; private set; }

    public bool IsLastFloor { get; }
    public IReadOnlyList<Room> Rooms => _rooms;
    public IReadOnlyList<RoomLink> Links => _links;
    public ReadOnlySpan<CellKind> Cells => _cells;

    /// <summary>The gaps (portal gaps and shaft gaps) that join rooms on this floor.</summary>
    public IReadOnlyList<GapSpec> Gaps => _gaps;

    /// <summary>How many blocks a cell's floor stands above the room floor (0 for most cells, 3 on gap hallways and shaft tunnels).</summary>
    public int HeightAt(int x, int z) => (uint)x < Size && (uint)z < Size ? _height[z * Size + x] : 0;

    /// <summary>The floor heights of every cell, for comparing layouts.</summary>
    public ReadOnlySpan<byte> Heights => _height;

    /// <summary>False only if no way to give every room a gap was found (it never is for the layouts the game makes).</summary>
    public bool GapsComplete { get; private set; } = true;

    /// <summary>Why the gaps could not be completed, for diagnostics.</summary>
    public string? GapFailure { get; private set; }

    public Room EntranceRoom { get; private set; } = null!;
    public Room? ExitRoom { get; private set; }
    public Room? BossRoom { get; private set; }

    private sealed class Node(Rect rect)
    {
        public Rect Rect = rect;
        public Node? A, B;
        public int RoomId = -1;
    }

    private FloorLayout(int floor, int entranceX, int entranceZ, bool last)
    {
        Floor = floor;
        EntranceX = entranceX;
        EntranceZ = entranceZ;
        IsLastFloor = last;
        Array.Fill(_roomAt, (short)-1);
    }

    public CellKind CellAt(int x, int z) =>
        (uint)x < Size && (uint)z < Size ? _cells[z * Size + x] : CellKind.Rock;

    public int RoomIdAt(int x, int z) =>
        (uint)x < Size && (uint)z < Size ? _roomAt[z * Size + x] : -1;

    public static bool IsOpen(CellKind k) => k != CellKind.Rock;

    /// <summary>True for cells a player can stand in without opening anything.</summary>
    public static bool IsWalkable(CellKind k) => k == CellKind.Floor || k == CellKind.Door;

    /// <summary>A rock cell touching open space (8-neighbourhood): it becomes a wall.</summary>
    public bool IsWallCell(int x, int z)
    {
        if (CellAt(x, z) != CellKind.Rock) return false;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (IsOpen(CellAt(x + dx, z + dz))) return true;
        return false;
    }

    /// <summary>How many times a floor is laid out again, with a different split, if a room cannot be given a gap.</summary>
    public const int MaxLayoutAttempts = 200;

    private const int RedigAttempts = 20;

    public static FloorLayout Generate(long seed, int floor, int entranceX, int entranceZ, bool lastFloor)
    {
        if (entranceX < 8 || entranceX > Size - 8 || entranceZ < 8 || entranceZ > Size - 8)
            throw new ArgumentOutOfRangeException(nameof(entranceX), "entrance must be at least 8 cells from the edge");

        FloorLayout? best = null;
        for (int attempt = 0; attempt < MaxLayoutAttempts; attempt++)
        {
            var layout = Build(seed, floor, entranceX, entranceZ, lastFloor, attempt);
            if (layout.GapsComplete) return layout;
            best ??= layout;
        }
        return best!;
    }

    private static FloorLayout Build(long seed, int floor, int entranceX, int entranceZ, bool lastFloor, int attempt)
    {
        var layout = new FloorLayout(floor, entranceX, entranceZ, lastFloor);
        // Attempt 0 is the layout the floor always had. Later attempts keep the same split and rooms and only re-dig the corridors
        // and loops (so the rooms stay as they were); after RedigAttempts of those the split itself is redrawn.
        int redig = attempt % RedigAttempts, resplit = attempt / RedigAttempts;
        var rng = new SeededRandom(seed, floor, 0, resplit == 0 ? 300 : 300 + 1000 * resplit);

        var root = layout.Split(new Rect(0, 0, Size, Size), 0, rng);
        layout.MakeRooms(root, rng);
        layout.PaintRooms();
        if (redig > 0) rng = new SeededRandom(seed, floor, redig, 305 + 1000 * resplit);
        layout.ConnectTree(root, rng);
        layout.AddLoops(rng);
        layout.AssignRoles(rng);
        layout.PlanLinks(new SeededRandom(seed, floor, attempt, 310));
        layout.PlacePillars();
        return layout;
    }

    // ---- BSP -------------------------------------------------------------------------------

    private bool ContainsEntrance(Rect r) => r.Contains(EntranceX, EntranceZ);

    private Node Split(Rect r, int depth, SeededRandom rng)
    {
        var node = new Node(r);
        bool canX = r.W >= 2 * MinLeaf, canZ = r.H >= 2 * MinLeaf;
        bool stop = depth >= MaxDepth || (depth >= 2 && rng.Chance(0.3));
        if (stop || (!canX && !canZ)) return node;

        bool preferX = canX && (!canZ || r.W * 4 > r.H * 5 || (r.W * 4 >= r.H * 3 && rng.Chance(0.5)));
        for (int pass = 0; pass < 2; pass++)
        {
            bool splitX = pass == 0 ? preferX : !preferX;
            if (splitX ? !canX : !canZ) continue;
            int len = splitX ? r.W : r.H;
            int start = splitX ? r.X : r.Z;
            int anchor = splitX ? EntranceX : EntranceZ;
            if (!TryPickSplit(len, start, anchor, ContainsEntrance(r), rng, out int pos)) continue;

            node.A = Split(splitX ? new Rect(r.X, r.Z, pos, r.H) : new Rect(r.X, r.Z, r.W, pos), depth + 1, rng);
            node.B = Split(splitX ? new Rect(r.X + pos, r.Z, r.W - pos, r.H) : new Rect(r.X, r.Z + pos, r.W, r.H - pos), depth + 1, rng);
            return node;
        }
        return node;
    }

    private static bool TryPickSplit(int len, int start, int anchor, bool hasAnchor, SeededRandom rng, out int pos)
    {
        int lo = MinLeaf, hi = len - MinLeaf;
        pos = rng.Range(lo, hi);
        if (!hasAnchor) return true;

        int a = anchor - start;
        if (Math.Abs(pos - a) >= AnchorClear) return true;

        bool below = a - AnchorClear >= lo, above = a + AnchorClear <= hi;
        if (below && above) pos = rng.Chance(0.5) ? a - AnchorClear : a + AnchorClear;
        else if (below) pos = a - AnchorClear;
        else if (above) pos = a + AnchorClear;
        else return false;
        return true;
    }

    // ---- rooms -----------------------------------------------------------------------------

    private void MakeRooms(Node node, SeededRandom rng)
    {
        if (node.A != null && node.B != null)
        {
            MakeRooms(node.A, rng);
            MakeRooms(node.B, rng);
            return;
        }

        var r = node.Rect;
        bool forced = ContainsEntrance(r);
        int Inset() => forced ? 2 : rng.Range(2, 4);
        int l = Inset(), t = Inset(), rr = Inset(), b = Inset();
        var bounds = new Rect(r.X + l, r.Z + t, r.W - l - rr, r.H - t - b);

        RoomShape shape = RoomShape.Rect;
        if (!forced)
        {
            int roll = rng.Next(100);
            shape = roll < 40 ? RoomShape.Rect : roll < 62 ? RoomShape.L : roll < 82 ? RoomShape.Circle : RoomShape.Cavern;
        }

        var room = new Room(_rooms.Count, bounds, shape);
        if (shape == RoomShape.L)
        {
            room.CutSx = rng.Chance(0.5) ? 1 : -1;
            room.CutSz = rng.Chance(0.5) ? 1 : -1;
            room.CutA = Math.Max(3, bounds.W / 6);
            room.CutB = Math.Max(3, bounds.H / 6);
        }
        else if (shape == RoomShape.Cavern)
        {
            var blobs = new List<Blob> { new(room.CenterX, room.CenterZ, bounds.W / 3, bounds.H / 3) };
            for (int i = 0; i < 4; i++)
            {
                int ox = rng.Range(-bounds.W / 5, bounds.W / 5), oz = rng.Range(-bounds.H / 5, bounds.H / 5);
                blobs.Add(new Blob(room.CenterX + ox, room.CenterZ + oz,
                    rng.Range(bounds.W / 6, bounds.W / 4), rng.Range(bounds.H / 6, bounds.H / 4)));
            }
            room.Blobs = [.. blobs];
        }

        node.RoomId = room.Id;
        _rooms.Add(room);
    }

    private void PaintRooms()
    {
        foreach (var room in _rooms)
            for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
                for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
                    if (room.Contains(x, z))
                    {
                        _cells[z * Size + x] = CellKind.Floor;
                        _roomAt[z * Size + x] = (short)room.Id;
                    }
        EntranceRoom = _rooms[_roomAt[EntranceZ * Size + EntranceX]];
    }

    // ---- corridors -------------------------------------------------------------------------

    private List<int> ConnectTree(Node node, SeededRandom rng)
    {
        if (node.A == null || node.B == null) return [node.RoomId];

        var left = ConnectTree(node.A, rng);
        var right = ConnectTree(node.B, rng);

        int bestA = left[0], bestB = right[0], best = int.MaxValue;
        foreach (int a in left)
            foreach (int b in right)
            {
                int d = Math.Abs(_rooms[a].CenterX - _rooms[b].CenterX) + Math.Abs(_rooms[a].CenterZ - _rooms[b].CenterZ);
                if (d < best) { best = d; bestA = a; bestB = b; }
            }
        Carve(bestA, bestB, secret: false, rng);
        left.AddRange(right);
        return left;
    }

    private void AddLoops(SeededRandom rng)
    {
        var pairs = new List<(int A, int B)>();
        for (int a = 0; a < _rooms.Count; a++)
            for (int b = a + 1; b < _rooms.Count; b++)
            {
                if (HasLink(a, b)) continue;
                int d = Math.Abs(_rooms[a].CenterX - _rooms[b].CenterX) + Math.Abs(_rooms[a].CenterZ - _rooms[b].CenterZ);
                if (d <= 48) pairs.Add((a, b));
            }
        rng.Shuffle(pairs);

        int budget = Math.Max(1, _rooms.Count / 3);
        var open = new List<(int A, int B)>();
        var hidden = new List<(int A, int B)>();
        foreach (var (a, b) in pairs)
        {
            if (budget == 0) break;
            if (!rng.Chance(0.5)) continue;
            (rng.Chance(0.4) ? hidden : open).Add((a, b));
            budget--;
        }

        // Open loops first: a later open corridor could otherwise overwrite a secret door.
        foreach (var (a, b) in open)
            if (!HasLink(a, b)) Carve(a, b, secret: false, rng);
        foreach (var (a, b) in hidden)
            if (!HasLink(a, b)) Carve(a, b, secret: true, rng);
    }

    private bool HasLink(int a, int b) =>
        _links.Any(l => (l.A == a && l.B == b) || (l.A == b && l.B == a));

    private List<(int X, int Z)> Route(Room a, Room b, bool horizontalFirst)
    {
        var path = new List<(int, int)>();
        int x = a.CenterX, z = a.CenterZ;
        path.Add((x, z));
        void Walk(bool alongX)
        {
            int target = alongX ? b.CenterX : b.CenterZ;
            while ((alongX ? x : z) != target)
            {
                if (alongX) x += Math.Sign(target - x); else z += Math.Sign(target - z);
                path.Add((x, z));
            }
        }
        Walk(horizontalFirst);
        Walk(!horizontalFirst);
        return path;
    }

    private int ForeignRooms(List<(int X, int Z)> path, int a, int b)
    {
        var seen = new HashSet<int>();
        foreach (var (x, z) in path)
        {
            int id = _roomAt[z * Size + x];
            if (id >= 0 && id != a && id != b) seen.Add(id);
        }
        return seen.Count;
    }

    /// <summary>
    /// Digs a 3-wide corridor between two room centres, with doors where it crosses a room
    /// boundary. If the path cuts through other rooms, the links are recorded through them,
    /// so the room graph always matches what the grid really connects.
    /// </summary>
    private void Carve(int a, int b, bool secret, SeededRandom rng)
    {
        var ra = _rooms[a];
        var rb = _rooms[b];
        var h = Route(ra, rb, true);
        var v = Route(ra, rb, false);
        int fh = ForeignRooms(h, a, b), fv = ForeignRooms(v, a, b);
        var path = fh < fv ? h : fv < fh ? v : rng.Chance(0.5) ? h : v;

        var fresh = new HashSet<int>();
        var dug = new HashSet<int>();
        foreach (var (px, pz) in path)
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int x = px + dx, z = pz + dz;
                    if (x < 1 || z < 1 || x >= Size - 1 || z >= Size - 1) continue;
                    int i = z * Size + x;
                    if (dug.Add(i) && _digs[i] < byte.MaxValue) _digs[i]++;
                    if (_cells[i] == CellKind.Rock)
                    {
                        _cells[i] = CellKind.Floor;
                        fresh.Add(i);
                    }
                }

        int linksBefore = _links.Count;
        int secretDoors = 0;
        int prevRoom = a;
        int exitIndex = 0;
        for (int i = 1; i < path.Count; i++)
        {
            int before = _roomAt[path[i - 1].Z * Size + path[i - 1].X];
            int after = _roomAt[path[i].Z * Size + path[i].X];
            if (before == after) continue;
            if (before >= 0 && after < 0) exitIndex = i - 1;

            if (after >= 0 && after != prevRoom)
            {
                if (AddLink(prevRoom, after, secret))
                    _routes.Add(new LinkRoute(path, exitIndex, i, prevRoom, after));
                prevRoom = after;
            }

            (int X, int Z) threshold;
            if (before < 0 && after >= 0) threshold = path[i - 1];
            else if (before >= 0 && after < 0) threshold = path[i];
            else continue;

            int dirX = path[i].X - path[i - 1].X;
            secretDoors += MarkDoor(threshold, perpendicularIsX: dirX == 0, secret, fresh);
        }

        // A "secret" corridor that overlapped existing passages and got no hidden door is
        // simply an open loop; record it as one so the room graph stays truthful.
        if (secret && secretDoors == 0)
            for (int i = linksBefore; i < _links.Count; i++)
                _links[i] = _links[i] with { Secret = false, Kind = LinkKind.Corridor };
    }

    private bool AddLink(int a, int b, bool secret)
    {
        if (a == b || HasLink(a, b)) return false;
        _links.Add(new RoomLink(Math.Min(a, b), Math.Max(a, b), secret, secret ? LinkKind.SecretDoor : LinkKind.Corridor));
        return true;
    }

    private int MarkDoor((int X, int Z) at, bool perpendicularIsX, bool secret, HashSet<int> fresh)
    {
        int secretCells = 0;
        for (int p = -1; p <= 1; p++)
        {
            int x = at.X + (perpendicularIsX ? p : 0), z = at.Z + (perpendicularIsX ? 0 : p);
            if ((uint)x >= Size || (uint)z >= Size) continue;
            int i = z * Size + x;
            if (_roomAt[i] >= 0 || _cells[i] == CellKind.Rock) continue;
            // A secret door may only seal cells its own corridor dug, never an existing passage.
            if (secret)
            {
                if (fresh.Contains(i))
                {
                    _cells[i] = CellKind.SecretDoor;
                    secretCells++;
                }
            }
            else
            {
                _cells[i] = CellKind.Door;
            }
        }
        return secretCells;
    }

    // ---- roles -----------------------------------------------------------------------------

    private void AssignRoles(SeededRandom rng)
    {
        var adj = new List<int>[_rooms.Count];
        var degree = new int[_rooms.Count];
        for (int i = 0; i < adj.Length; i++) adj[i] = [];
        foreach (var l in _links)
        {
            degree[l.A]++;
            degree[l.B]++;
            if (l.Secret) continue;
            adj[l.A].Add(l.B);
            adj[l.B].Add(l.A);
        }

        var depth = Enumerable.Repeat(-1, _rooms.Count).ToArray();
        var queue = new Queue<int>();
        depth[EntranceRoom.Id] = 0;
        queue.Enqueue(EntranceRoom.Id);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (int n in adj[cur])
                if (depth[n] < 0)
                {
                    depth[n] = depth[cur] + 1;
                    queue.Enqueue(n);
                }
        }
        foreach (var r in _rooms) r.Depth = Math.Max(depth[r.Id], 0);

        EntranceRoom.Role = RoomRole.Entrance;
        var farthest = _rooms.Where(r => r != EntranceRoom).OrderByDescending(r => r.Depth).ThenBy(r => r.Id).First();
        if (IsLastFloor)
        {
            BossRoom = farthest;
            farthest.Role = RoomRole.Boss;
        }
        else
        {
            ExitRoom = farthest;
            farthest.Role = RoomRole.Exit;
            ExitStairs = (farthest.CenterX, farthest.CenterZ);
        }

        var deadEnds = _rooms
            .Where(r => r.Role == RoomRole.Standard && degree[r.Id] == 1)
            .OrderByDescending(r => r.Depth).ThenBy(r => r.Id)
            .Take(2);
        foreach (var r in deadEnds) r.Role = RoomRole.Treasure;

        var safe = _rooms.Where(r => r.Role == RoomRole.Standard).OrderBy(r => r.Depth).ThenBy(r => r.Id).FirstOrDefault();
        if (safe != null && _rooms.Count >= 5) safe.Role = RoomRole.Safe;

        foreach (var r in _rooms)
            if (r.Role == RoomRole.Standard && rng.Chance(0.25))
                r.Role = RoomRole.Trap;
    }

    private bool NearStairs(int x, int z)
    {
        if (Math.Abs(x - EntranceX) <= 4 && Math.Abs(z - EntranceZ) <= 4) return true;
        return ExitStairs is { } e && Math.Abs(x - e.X) <= 4 && Math.Abs(z - e.Z) <= 4;
    }

    private void PlacePillars()
    {
        foreach (var room in _rooms)
        {
            if (room.Shape is not (RoomShape.Rect or RoomShape.Circle)) continue;
            if (room.Bounds.W < 13 || room.Bounds.H < 13) continue;

            int dx = room.Bounds.W / 4, dz = room.Bounds.H / 4;
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                int x = room.CenterX + sx * dx, z = room.CenterZ + sz * dz;
                int i = z * Size + x;
                if (_cells[i] == CellKind.Floor && _roomAt[i] == room.Id && !NearStairs(x, z) && ClearOfGaps(x, z))
                    _cells[i] = CellKind.Pillar;
            }
        }
    }
}
