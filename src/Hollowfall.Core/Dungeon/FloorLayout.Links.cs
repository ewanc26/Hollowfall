namespace Hollowfall.Core.Dungeon;

/// <summary>
/// Mixed room links. After the rooms and corridors are laid out, each ordinary link is given a <see cref="LinkKind"/>: some stay
/// walkable corridors, some become gaps the player must cross with a portal. Every room is guaranteed at least one portal gap
/// (and so an entrance hallway leading to it). Gaps are built into a straight stretch of the link's own corridor, cut into the
/// facing edge of the rooms where the street between them is too short. The room graph does not change, so depths, roles and the
/// exit stay exactly as they were.
/// </summary>
public sealed partial class FloorLayout
{
    private sealed record LinkRoute(List<(int X, int Z)> Path, int ExitIndex, int EnterIndex, int From, int To);

    private sealed class Run
    {
        public (int X, int Z)[] Cells = [];
        public int Dx, Dz;
        public int From, To;
        public int Length => Cells.Length;
    }

    private const int StairsClearance = 5;

    private bool NearAnchor(int x, int z)
    {
        if (Math.Abs(x - EntranceX) <= StairsClearance && Math.Abs(z - EntranceZ) <= StairsClearance) return true;
        return ExitStairs is { } e && Math.Abs(x - e.X) <= StairsClearance && Math.Abs(z - e.Z) <= StairsClearance;
    }

    /// <summary>Whether a pillar here would stay clear of every gap and its hallways.</summary>
    private bool ClearOfGaps(int x, int z)
    {
        for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
                if ((uint)(x + dx) < Size && (uint)(z + dz) < Size && _reserved[(z + dz) * Size + x + dx]) return false;
        return true;
    }

    private bool IsOpenKind(int x, int z) => CellAt(x, z) is CellKind.Floor or CellKind.Door;

    /// <summary>Whether a cell can be part of a gap's hallway or pit: open ground of this corridor or of one of its two rooms.</summary>
    private bool StripCellOk(int x, int z, int from, int to)
    {
        if (x < 3 || z < 3 || x >= Size - 3 || z >= Size - 3) return false;
        int i = z * Size + x;
        if (_reserved[i] || NearAnchor(x, z)) return false;
        if (_cells[i] is not (CellKind.Floor or CellKind.Door)) return false;
        // A room keeps its centre: the gap may not reach into the middle of either room.
        if (CentreOf(from, x, z) || CentreOf(to, x, z)) return false;
        int room = _roomAt[i];
        if (room == -1) return _digs[i] <= 1;
        return room == from || room == to;
    }

    private bool CentreOf(int room, int x, int z) =>
        (uint)room < (uint)_rooms.Count && Math.Abs(x - _rooms[room].CenterX) <= 2 && Math.Abs(z - _rooms[room].CenterZ) <= 2;

    /// <summary>Whether a cell beside the hallway can be turned into wall: rock already, or floor of one of the two rooms.</summary>
    private bool FlankCellOk(int x, int z, int from, int to)
    {
        if (x < 2 || z < 2 || x >= Size - 2 || z >= Size - 2) return false;
        int i = z * Size + x;
        if (_reserved[i] || CentreOf(from, x, z) || CentreOf(to, x, z)) return false;
        if (_cells[i] == CellKind.Rock) return true;
        return _cells[i] == CellKind.Floor && (_roomAt[i] == from || _roomAt[i] == to);
    }

    private bool ColumnOk(int x, int z, int latX, int latZ, int from, int to)
    {
        for (int l = -1; l <= 1; l++)
            if (!StripCellOk(x + latX * l, z + latZ * l, from, to)) return false;
        for (int l = -2; l <= 2; l += 4)
            if (!FlankCellOk(x + latX * l, z + latZ * l, from, to)) return false;
        return true;
    }

    private bool OpenColumn(int x, int z, int latX, int latZ)
    {
        for (int l = -1; l <= 1; l++)
            if (!IsOpenKind(x + latX * l, z + latZ * l)) return false;
        return true;
    }

    private List<Run> FindRuns(LinkRoute route)
    {
        var runs = new List<Run>();
        var p = route.Path;
        int s = 0;
        while (s < p.Count - 1)
        {
            int dx = Math.Sign(p[s + 1].X - p[s].X), dz = Math.Sign(p[s + 1].Z - p[s].Z);
            int t = s + 1;
            while (t + 1 < p.Count && Math.Sign(p[t + 1].X - p[t].X) == dx && Math.Sign(p[t + 1].Z - p[t].Z) == dz) t++;

            bool cornerAtStart = s > 0, cornerAtEnd = t < p.Count - 1;
            int latX = -dz, latZ = dx;
            int start = -1;
            for (int idx = s; idx <= t + 1; idx++)
            {
                bool ok = idx <= t;
                // The corridor bends at a corner: keep clear of the cells where the other leg joins.
                if (ok && cornerAtStart && idx <= s + 1) ok = false;
                if (ok && cornerAtEnd && idx >= t - 1) ok = false;
                if (ok) ok = ColumnOk(p[idx].X, p[idx].Z, latX, latZ, route.From, route.To);

                if (ok)
                {
                    if (start < 0) start = idx;
                    continue;
                }
                if (start >= 0)
                {
                    AddRun(runs, route, start, idx - 1, dx, dz);
                    start = -1;
                }
            }
            s = t;
        }
        return runs;
    }

    private void AddRun(List<Run> runs, LinkRoute route, int first, int last, int dx, int dz)
    {
        var p = route.Path;
        // It has to span part of the street between the two rooms, not sit wholly inside a room.
        bool street = false;
        for (int idx = Math.Max(first, route.ExitIndex + 1); idx <= Math.Min(last, route.EnterIndex - 1); idx++)
            if (_roomAt[p[idx].Z * Size + p[idx].X] == -1) street = true;
        if (!street) return;
        runs.Add(new Run { Cells = [.. p.Skip(first).Take(last - first + 1)], Dx = dx, Dz = dz, From = route.From, To = route.To });
    }

    private Run? BestRun(int linkIndex)
    {
        if (linkIndex >= _routes.Count) return null;
        return FindRuns(_routes[linkIndex]).OrderByDescending(r => r.Length).FirstOrDefault();
    }

    // ---- planning ---------------------------------------------------------------------------

    private void PlanLinks(SeededRandom rng)
    {
        int n = _links.Count;
        var best = new Run?[n];
        for (int i = 0; i < n; i++)
            if (!_links[i].Secret) best[i] = BestRun(i);

        bool Eligible(int i) => best[i] is { } r && r.Length >= LinkMix.MinGapLength;

        var incident = new List<int>[_rooms.Count];
        for (int r = 0; r < incident.Length; r++) incident[r] = [];
        for (int i = 0; i < n; i++)
            if (!_links[i].Secret)
            {
                incident[_links[i].A].Add(i);
                incident[_links[i].B].Add(i);
            }

        var gaps = new List<int>();
        var covered = new bool[_rooms.Count];
        bool ok = true;
        void Fail(string why) { ok = false; GapFailure ??= why; }

        void Add(int i)
        {
            if (gaps.Contains(i)) return;
            gaps.Add(i);
            covered[_links[i].A] = covered[_links[i].B] = true;
        }

        // 1. A room with a single way out has to leave through a gap.
        foreach (var room in _rooms)
            if (incident[room.Id].Count == 1)
            {
                if (Eligible(incident[room.Id][0])) Add(incident[room.Id][0]);
                else Fail("leaf");
            }

        // 2. The first way out of the entrance room is a gap with a short span, so the player learns the trick on an easy one.
        int firstLink = -1;
        var entranceLinks = incident[EntranceRoom.Id].Where(Eligible).ToList();
        if (entranceLinks.Count == 0) Fail("entrance");
        else
        {
            var already = entranceLinks.Where(gaps.Contains).ToList();
            firstLink = already.Count > 0 ? already[rng.Next(already.Count)] : entranceLinks[rng.Next(entranceLinks.Count)];
            Add(firstLink);
        }

        // 3. Cover every other room, preferring one gap that serves two rooms.
        var order = _rooms.Select(r => r.Id).ToList();
        rng.Shuffle(order);
        foreach (int room in order)
        {
            if (covered[room]) continue;
            var options = incident[room].Where(Eligible).ToList();
            if (options.Count == 0)
            {
                Fail("cover");
                continue;
            }
            var both = options.Where(i => !covered[_links[i].A] && !covered[_links[i].B]).ToList();
            var pool = both.Count > 0 ? both : options;
            Add(pool[rng.Next(pool.Count)]);
        }

        // 4. Deeper floors turn more of the remaining links into gaps.
        var spare = Enumerable.Range(0, n).Where(i => !_links[i].Secret && Eligible(i) && !gaps.Contains(i)).ToList();
        rng.Shuffle(spare);
        foreach (int i in spare)
            if (rng.Chance(LinkMix.ExtraGapChance(Floor))) Add(i);

        // 5. A few of the rest become shaft gaps, deeper down.
        var shafts = new List<int>();
        foreach (int i in Enumerable.Range(0, n))
        {
            if (_links[i].Secret || gaps.Contains(i) || best[i] is not { } run || run.Length < LinkMix.MinShaftLength) continue;
            if (rng.Chance(LinkMix.ShaftChance(Floor))) shafts.Add(i);
        }

        // Build them. A gap that no longer fits because an earlier one took its space is dropped, and the check below
        // decides whether every room still has one.
        var hazardRng = new SeededRandom((long)rng.NextU64(), Floor, 0, 311);
        foreach (int i in gaps)
        {
            bool first = i == firstLink;
            var hazard = LinkMix.PickHazard(Floor, first, hazardRng);
            var now = FindRuns(_routes[i]).Where(r => r.Length >= LinkMix.MinGapLength).OrderByDescending(r => r.Length).FirstOrDefault();
            if (now == null) continue;
            BuildGap(i, now, LinkKind.PortalGap, hazard, first, hazardRng);
        }
        foreach (int i in shafts)
        {
            var now = FindRuns(_routes[i]).Where(r => r.Length >= LinkMix.MinShaftLength).OrderByDescending(r => r.Length).FirstOrDefault();
            if (now == null) continue;
            BuildGap(i, now, LinkKind.ShaftGap, GapHazard.Bare, false, hazardRng);
        }

        var coveredNow = new bool[_rooms.Count];
        foreach (var g in _gaps.Where(g => g.Kind == LinkKind.PortalGap))
        {
            coveredNow[_links[g.LinkIndex].A] = true;
            coveredNow[_links[g.LinkIndex].B] = true;
        }
        if (ok && !coveredNow.All(c => c)) Fail("built");
        if (ok && !RoomsStayWhole()) Fail("split");
        GapsComplete = ok;
    }

    /// <summary>Whether cutting the hallways out of the rooms left each one in a single piece (a sliver may be cut off, no more).</summary>
    private bool RoomsStayWhole()
    {
        foreach (var room in _rooms)
        {
            var cells = new HashSet<int>();
            for (int z = room.Bounds.Z; z < room.Bounds.Bottom; z++)
                for (int x = room.Bounds.X; x < room.Bounds.Right; x++)
                    if (_roomAt[z * Size + x] == room.Id) cells.Add(z * Size + x);
            if (cells.Count == 0) return false;

            var seen = new HashSet<int>();
            int start = cells.Contains(room.CenterZ * Size + room.CenterX) ? room.CenterZ * Size + room.CenterX : cells.First();
            var stack = new Stack<int>([start]);
            seen.Add(start);
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                int cx = c % Size, cz = c / Size;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ni = (cz + dz) * Size + cx + dx;
                    if (cells.Contains(ni) && seen.Add(ni)) stack.Push(ni);
                }
            }
            if (seen.Count < cells.Count * 0.97) return false;
        }
        return true;
    }

    // ---- building ---------------------------------------------------------------------------

    private bool BuildGap(int linkIndex, Run run, LinkKind kind, GapHazard hazard, bool first, SeededRandom rng)
    {
        bool shaft = kind == LinkKind.ShaftGap;
        int hallMin = shaft ? LinkMix.ShaftHallMin : LinkMix.HallMin, hallMax = shaft ? LinkMix.ShaftHallMax : LinkMix.HallMax;
        int spanMin = shaft ? LinkMix.TunnelMin : first ? LinkMix.FirstSpanMin : LinkMix.SpanMin;
        int spanMax = shaft ? LinkMix.TunnelMax : first ? LinkMix.FirstSpanMax : LinkMix.SpanMax;

        // Sizes: aim for random ones, then shrink to what fits.
        int h0 = rng.Range(hallMin, hallMax), h1 = rng.Range(hallMin, hallMax), span = rng.Range(spanMin, spanMax);
        while (2 + h0 + h1 + span > run.Length)
        {
            if (span > spanMin) span--;
            else if (h0 > hallMin && h0 >= h1) h0--;
            else if (h1 > hallMin) h1--;
            else if (h0 > hallMin) h0--;
            else return false;
        }
        int total = 2 + h0 + h1 + span;

        // Slide the window along the run, from the middle outwards, until both ends meet open ground.
        int latX = -run.Dz, latZ = run.Dx;
        int centred = (run.Length - total) / 2;
        for (int k = 0; k <= run.Length - total; k++)
        {
            int u0 = centred + ((k & 1) == 0 ? k / 2 : -(k + 1) / 2);
            if (u0 < 0 || u0 + total > run.Length) continue;
            var (bx, bz) = (run.Cells[u0].X - run.Dx, run.Cells[u0].Z - run.Dz);
            var (ax, az) = (run.Cells[u0 + total - 1].X + run.Dx, run.Cells[u0 + total - 1].Z + run.Dz);
            if (!OpenColumn(bx, bz, latX, latZ) || !OpenColumn(ax, az, latX, latZ)) continue;
            Apply(linkIndex, run, u0, h0, span, h1, kind, hazard);
            return true;
        }
        return false;
    }

    private void Apply(int linkIndex, Run run, int u0, int h0, int span, int h1, LinkKind kind, GapHazard hazard)
    {
        bool shaft = kind == LinkKind.ShaftGap;
        int total = 2 + h0 + h1 + span;
        int latX = -run.Dz, latZ = run.Dx;
        var pitKind = hazard switch { GapHazard.Spikes => CellKind.PitSpikes, GapHazard.Water => CellKind.PitWater, _ => CellKind.Pit };

        bool doorFrom = _roomAt[(run.Cells[u0].Z - run.Dz) * Size + run.Cells[u0].X - run.Dx] >= 0;
        bool doorTo = _roomAt[(run.Cells[u0 + total - 1].Z + run.Dz) * Size + run.Cells[u0 + total - 1].X + run.Dx] >= 0;

        var hallFrom = new List<(int, int)>();
        var spanCells = new List<(int, int)>();
        var hallTo = new List<(int, int)>();

        for (int j = 0; j < total; j++)
        {
            var (cx, cz) = run.Cells[u0 + j];
            bool inFrom = j <= h0, inTo = j >= total - 1 - h1;
            if (inFrom) hallFrom.Add((cx, cz));
            else if (inTo) hallTo.Add((cx, cz));
            else spanCells.Add((cx, cz));

            for (int l = -2; l <= 2; l++)
            {
                int x = cx + latX * l, z = cz + latZ * l;
                int i = z * Size + x;
                _roomAt[i] = -1;
                if (Math.Abs(l) == 2)
                {
                    _cells[i] = CellKind.Rock;
                    _height[i] = 0;
                    continue;
                }

                if (inFrom || inTo)
                {
                    int fromEnd = inFrom ? j : total - 1 - j; // 0 at the door cell
                    bool door = fromEnd == 0 && (inFrom ? doorFrom : doorTo);
                    _cells[i] = door ? CellKind.Door : CellKind.Floor;
                    _height[i] = (byte)(shaft || fromEnd <= 1 ? 0 : Math.Min(fromEnd - 1, LinkMix.Rise));
                }
                else if (shaft)
                {
                    _cells[i] = CellKind.Floor;
                    _height[i] = LinkMix.Rise;
                }
                else
                {
                    _cells[i] = pitKind;
                    _height[i] = 0;
                }
            }

            // Keep other gaps and the pillars well away.
            for (int l = -3; l <= 3; l++)
            {
                int x = cx + latX * l, z = cz + latZ * l;
                if ((uint)x < Size && (uint)z < Size) _reserved[z * Size + x] = true;
            }
        }
        foreach (var (ex, ez) in new[] { (run.Cells[u0].X - run.Dx, run.Cells[u0].Z - run.Dz), (run.Cells[u0 + total - 1].X + run.Dx, run.Cells[u0 + total - 1].Z + run.Dz) })
            for (int l = -3; l <= 3; l++)
            {
                int x = ex + latX * l, z = ez + latZ * l;
                if ((uint)x < Size && (uint)z < Size) _reserved[z * Size + x] = true;
            }

        _links[linkIndex] = _links[linkIndex] with { Kind = kind };
        _gaps.Add(new GapSpec
        {
            LinkIndex = linkIndex,
            Kind = kind,
            Hazard = hazard,
            DirX = run.Dx,
            DirZ = run.Dz,
            RoomFrom = run.From,
            RoomTo = run.To,
            HallFrom = hallFrom,
            Span = spanCells,
            HallTo = hallTo,
            Rise = LinkMix.Rise,
        });
    }
}
