using Hollowfall.Core.Terrain;
using Hollowfall.Core.Village;
using Hollowfall.Core.World;

namespace Hollowfall.Tests;

public class VillageTests
{
    private static IEnumerable<(TerrainGenerator Terrain, VillagePlan Plan)> VillagesAcrossSeeds()
    {
        for (long seed = 1; seed <= 40; seed++)
        {
            var terrain = new TerrainGenerator(seed);
            for (int cx = -3; cx <= 3; cx++)
                for (int cz = -3; cz <= 3; cz++)
                    if (terrain.Villages.PlanFor(cx, cz) is { } plan) yield return (terrain, plan);
        }
    }

    [Fact]
    public void VillagesTurnUpSomewhereInTheWorld()
    {
        Assert.True(VillagesAcrossSeeds().Take(12).Count() >= 12);
    }

    [Fact]
    public void APlanIsAPureFunctionOfTheSeedAndCell()
    {
        var a = new TerrainGenerator(7).Villages;
        var b = new TerrainGenerator(7).Villages;
        for (int cx = -2; cx <= 2; cx++)
            for (int cz = -2; cz <= 2; cz++)
            {
                var pa = a.PlanFor(cx, cz);
                var pb = b.PlanFor(cx, cz);
                Assert.Equal(pa is null, pb is null);
                if (pa is null || pb is null) continue;
                Assert.Equal(pa.Houses, pb.Houses);
                Assert.Equal(pa.Roads, pb.Roads);
                Assert.Equal(pa.Ground, pb.Ground);
            }
    }

    [Fact]
    public void HousesSitInTheYardClearOfThePlazaAndEachOther()
    {
        foreach (var (_, plan) in VillagesAcrossSeeds())
        {
            Assert.InRange(plan.Houses.Count, 3, 6);
            foreach (var h in plan.Houses)
            {
                for (int x = h.X; x < h.X + h.Width; x++)
                    for (int z = h.Z; z < h.Z + h.Depth; z++)
                    {
                        Assert.True(plan.InYard(x, z));
                        Assert.False(plan.IsPlaza(x, z));
                    }
            }
            for (int i = 0; i < plan.Houses.Count; i++)
                for (int j = i + 1; j < plan.Houses.Count; j++)
                {
                    var a = plan.Houses[i];
                    var b = plan.Houses[j];
                    bool apart = a.X + a.Width < b.X || b.X + b.Width < a.X || a.Z + a.Depth < b.Z || b.Z + b.Depth < a.Z;
                    Assert.True(apart, $"houses {i} and {j} overlap or touch");
                }
        }
    }

    [Fact]
    public void EveryDoorstepIsOnARoadAndTheRoadsReachTheWell()
    {
        foreach (var (_, plan) in VillagesAcrossSeeds())
        {
            Assert.True(plan.IsRoad(plan.CenterX, plan.CenterZ));
            foreach (var h in plan.Houses)
                Assert.True(plan.IsRoad(h.Doorstep.X, h.Doorstep.Z));
            Assert.All(plan.Roads, r => Assert.Null(plan.HouseAt(r.X, r.Z)));
        }
    }

    [Fact]
    public void TheGroundOfAVillageIsTheHeightAtItsCentre()
    {
        foreach (var (terrain, plan) in VillagesAcrossSeeds())
            Assert.Equal(terrain.SampleColumn(plan.CenterX, plan.CenterZ).Height, plan.Ground);
    }

    [Fact]
    public void ACottageHasPlankWallsADoorwayAndAThatchRoof()
    {
        var (_, plan) = VillagesAcrossSeeds().First();
        var house = plan.Houses[0];
        var chunk = new Chunk(FloorDiv(house.X, Chunk.Size), FloorDiv(house.Z, Chunk.Size));
        VillageBuilder.Apply(chunk, plan);

        int y = plan.Ground + 1;
        var (dx, dz) = house.Door;
        Assert.Equal(BlockType.Air, chunk[dx - chunk.WorldMinX, y, dz - chunk.WorldMinZ]);
        Assert.Equal(BlockType.Air, chunk[dx - chunk.WorldMinX, y + 1, dz - chunk.WorldMinZ]);
        Assert.Equal(BlockType.Plank, chunk[house.X - chunk.WorldMinX, y, house.Z - chunk.WorldMinZ]);

        int cx = house.X + house.Width / 2, cz = house.Z + house.Depth / 2;
        int roof = plan.RoofTop(cx, cz) ?? throw new InvalidOperationException("no roof over the middle of a house");
        Assert.Equal(BlockType.Thatch, chunk[cx - chunk.WorldMinX, roof, cz - chunk.WorldMinZ]);
        Assert.Equal(BlockType.Air, chunk[cx - chunk.WorldMinX, roof + 1, cz - chunk.WorldMinZ]);
    }

    private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);
}
