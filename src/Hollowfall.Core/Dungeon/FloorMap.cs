namespace Hollowfall.Core.Dungeon;

public enum MapCell : byte
{
    Unknown,
    Wall,
    Floor,
    Door,
    Pillar,
    Stairs,

    /// <summary>A gap hallway or a shaft gap's tunnel: a way that needs a portal at its far end.</summary>
    Hall,

    /// <summary>The pit of a portal gap.</summary>
    Pit,
}

/// <summary>
/// What the player has seen of each floor, for the minimap. Cells are revealed by line of sight from where the player
/// stands, so a room fills in as you look around it and a corridor as you walk down it; secret doors look like wall until
/// they are found. Pure data: the host draws it.
/// </summary>
public sealed class FloorMap
{
    public const int SightRadius = 9;

    private readonly DungeonSite _site;
    private readonly bool[][] _seen;
    private readonly (int X, int Z)?[] _lastCell;

    /// <summary>Goes up whenever anything new is revealed, so the host redraws only when it must.</summary>
    public int Version { get; private set; }

    public FloorMap(DungeonSite site)
    {
        _site = site;
        _seen = [.. Enumerable.Range(0, site.FloorCount).Select(_ => new bool[FloorLayout.Size * FloorLayout.Size])];
        _lastCell = new (int, int)?[site.FloorCount];
    }

    public int FloorCount => _seen.Length;

    public bool IsSeen(int floor, int x, int z) => InRange(x, z) && _seen[floor][z * FloorLayout.Size + x];

    public int SeenCount(int floor) => _seen[floor].Count(s => s);

    private static bool InRange(int x, int z) => (uint)x < FloorLayout.Size && (uint)z < FloorLayout.Size;

    /// <summary>What to draw for a cell: unknown until seen.</summary>
    public MapCell At(int floor, int x, int z)
    {
        if (!IsSeen(floor, x, z)) return MapCell.Unknown;
        var layout = _site.Floors[floor];
        if (layout.ExitStairs is { } s && Math.Abs(s.X - x) <= 1 && Math.Abs(s.Z - z) <= 1 && layout.CellAt(x, z) != CellKind.Rock) return MapCell.Stairs;
        return layout.CellAt(x, z) switch
        {
            CellKind.Pit or CellKind.PitSpikes or CellKind.PitWater => MapCell.Pit,
            CellKind.Floor when layout.GapAt(x, z) != null => MapCell.Hall,
            CellKind.Floor => MapCell.Floor,
            CellKind.Door => MapCell.Door,
            CellKind.Pillar => MapCell.Pillar,
            _ => MapCell.Wall, // rock and secret doors alike
        };
    }

    /// <summary>The layout cell a world position is in on this site.</summary>
    public (int X, int Z) CellOf(double wx, double wz) => ((int)Math.Floor(wx) - _site.OriginX, (int)Math.Floor(wz) - _site.OriginZ);

    /// <summary>Reveals what can be seen from a world position on a floor. Cheap to call every tick: it does nothing until the player changes cell.</summary>
    public void Reveal(int floor, double wx, double wz)
    {
        var (cx, cz) = CellOf(wx, wz);
        if (_lastCell[floor] == (cx, cz) || !InRange(cx, cz)) return;
        _lastCell[floor] = (cx, cz);

        var layout = _site.Floors[floor];
        bool changed = Mark(floor, cx, cz);
        for (int dz = -SightRadius; dz <= SightRadius; dz++)
            for (int dx = -SightRadius; dx <= SightRadius; dx++)
            {
                if (dx * dx + dz * dz > SightRadius * SightRadius) continue;
                int x = cx + dx, z = cz + dz;
                if (!InRange(x, z) || _seen[floor][z * FloorLayout.Size + x]) continue;
                if (CanSee(layout, cx, cz, x, z)) changed |= Mark(floor, x, z);
            }
        changed |= RevealAcrossGap(floor, layout, cx, cz);
        if (changed) Version++;
    }

    /// <summary>How far along a gap the player sees from its hallway, in cells: far enough to take in the far hallway of the longest pit.</summary>
    public const int GapSightRange = 36;

    /// <summary>Standing in a gap's hallway you can see straight across the pit, so the far hallway shows on the map beyond the usual radius.</summary>
    private bool RevealAcrossGap(int floor, FloorLayout layout, int cx, int cz)
    {
        if (layout.GapAt(cx, cz) is not { } gap) return false;
        bool changed = false;
        foreach (int sign in new[] { 1, -1 })
            for (int along = 1; along <= GapSightRange; along++)
                for (int lat = -1; lat <= 1; lat++)
                {
                    int x = cx + gap.DirX * along * sign - gap.DirZ * lat, z = cz + gap.DirZ * along * sign + gap.DirX * lat;
                    if (!InRange(x, z) || _seen[floor][z * FloorLayout.Size + x]) continue;
                    if (CanSee(layout, cx, cz, x, z)) changed |= Mark(floor, x, z);
                }
        return changed;
    }

    private bool Mark(int floor, int x, int z)
    {
        ref bool slot = ref _seen[floor][z * FloorLayout.Size + x];
        if (slot) return false;
        slot = true;
        return true;
    }

    /// <summary>Line of sight over the grid: open cells and doors let it through, rock and secret doors stop it (but are themselves seen).</summary>
    private static bool CanSee(FloorLayout layout, int x0, int z0, int x1, int z1)
    {
        int dx = Math.Abs(x1 - x0), dz = Math.Abs(z1 - z0);
        int sx = x0 < x1 ? 1 : -1, sz = z0 < z1 ? 1 : -1;
        int err = dx - dz, x = x0, z = z0;
        while (x != x1 || z != z1)
        {
            int e2 = 2 * err;
            if (e2 > -dz) { err -= dz; x += sx; }
            if (e2 < dx) { err += dx; z += sz; }
            if (x == x1 && z == z1) return true; // the target itself may be a wall: it is seen, the way past it is not
            if (layout.CellAt(x, z) is CellKind.Rock or CellKind.SecretDoor) return false;
        }
        return true;
    }
}
