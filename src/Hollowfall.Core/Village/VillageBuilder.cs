using Hollowfall.Core.World;

namespace Hollowfall.Core.Village;

/// <summary>Writes a village into a freshly generated chunk: levels the yard, lays the roads and plaza, and raises the cottages.</summary>
public static class VillageBuilder
{
    public static void Apply(Chunk chunk, VillagePlan plan)
    {
        int ox = chunk.WorldMinX, oz = chunk.WorldMinZ;
        int g = plan.Ground;

        for (int lx = 0; lx < Chunk.Size; lx++)
            for (int lz = 0; lz < Chunk.Size; lz++)
            {
                int x = ox + lx, z = oz + lz;
                if (!plan.InYard(x, z)) continue;

                for (int y = g - VillagePlan.FoundationDepth; y < g; y++)
                    if (!BlockInfo.IsSolid(chunk[lx, y, lz])) chunk[lx, y, lz] = BlockType.Dirt;

                var house = plan.HouseAt(x, z);
                BlockType ground = house is not null ? BlockType.Plank
                    : plan.IsPlaza(x, z) || plan.IsRoad(x, z) ? BlockType.Gravel
                    : BlockType.Grass;
                chunk[lx, g, lz] = ground;

                for (int y = g + 1; y < Chunk.Height; y++) chunk[lx, y, lz] = BlockType.Air;

                if (house is { } h && h.IsWall(x, z))
                    for (int y = g + 1; y <= g + VillagePlan.WallHeight; y++)
                    {
                        bool doorway = h.Door == (x, z) && y <= g + 2;
                        if (!doorway) chunk[lx, y, lz] = BlockType.Plank;
                    }

                if (plan.RoofTop(x, z) is { } top)
                    for (int y = g + VillagePlan.WallHeight + 1; y <= top; y++)
                        chunk[lx, y, lz] = BlockType.Thatch;
            }
    }
}
