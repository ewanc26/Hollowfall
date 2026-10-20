using Godot;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Dungeon;
using Hollowfall.Core.Meshing;
using Hollowfall.Core.Terrain;
using Hollowfall.Core.World;

namespace Hollowfall.Game;

/// <summary>
/// Headless check that the Godot shell can load Hollowfall.Core and run it. Prints a
/// "==== N passed, M failed ====" summary and exits non-zero on failure
/// (run-smoke-test.sh depends on both).
/// </summary>
public partial class SmokeTest : Node
{
    private int _passed;
    private int _failed;

    public override async void _Ready()
    {
        Check("terrain is deterministic", () =>
            new TerrainGenerator(7).GenerateChunk(2, 3).RawBlocks.SequenceEqual(new TerrainGenerator(7).GenerateChunk(2, 3).RawBlocks));

        Check("terrain has solid ground", () =>
        {
            var chunk = new TerrainGenerator(7).GenerateChunk(0, 0);
            return chunk.TopY(8, 8) > 0 && chunk[8, 0, 8] == BlockType.Bedrock;
        });

        Check("a dungeon is found and has floors", () =>
        {
            var generator = new TerrainGenerator(1);
            for (int rx = -2; rx <= 2; rx++)
                for (int rz = -2; rz <= 2; rz++)
                    if (generator.Dungeons!.GetSite(rx, rz) is { } site)
                        return site.FloorCount >= 2 && site.Floors.All(f => f.Rooms.Count >= 4);
            return false;
        });

        Check("dungeon chunks carry dungeon blocks", () =>
        {
            var generator = new TerrainGenerator(1);
            var site = generator.Dungeons!.GetSite(0, 1)!;
            var chunk = generator.GenerateChunk(site.EntranceX >> 4, site.EntranceZ >> 4);
            return chunk.RawBlocks.ToArray().Contains(BlockType.Stairs);
        });

        Check("a land chunk converts to a Godot mesh with surfaces", () =>
        {
            var world = new VoxelWorld(7);
            var mesh = ChunkMeshBuilder.ToArrayMesh(ChunkMesher.Build(0, 0, world.GetBlock));
            return mesh != null && mesh.GetSurfaceCount() >= 1 && mesh.GetAabb().Size.Y > 1;
        });

        Check("an all-air chunk converts to no mesh", () =>
            ChunkMeshBuilder.ToArrayMesh(ChunkMesher.Build(0, 0, (_, _, _) => BlockType.Air)) == null);

        await CheckMainSceneAsync();

        GD.Print($"==== {_passed} passed, {_failed} failed ====");
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    /// <summary>Boots the real game scene and waits for the world to stream in and the player to land.</summary>
    private async Task CheckMainSceneAsync()
    {
        const string name = "the main scene streams in a world and the player lands";
        try
        {
            var main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
            AddChild(main);

            var view = main.GetNode<WorldView>("World");
            var player = main.GetNode<PlayerController>("Player");
            var deadline = Time.GetTicksMsec() + 20_000;
            while (Time.GetTicksMsec() < deadline && !(view.ShownChunks >= 40 && player.Body.OnGround))
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            bool ok = view.ShownChunks >= 40 && player.Body.OnGround;
            if (ok) { _passed++; GD.Print($"  ok   {name} ({view.ShownChunks} chunks, feet at y={player.Body.Y:F1})"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: chunks={view.ShownChunks} onGround={player.Body.OnGround}"); }

            await CheckCombatAsync(main);
            main.QueueFree();
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    /// <summary>Combat and portals running inside the real scene: enemies get bodies, portals get nodes and cameras.</summary>
    private async Task CheckCombatAsync(Main main)
    {
        const string name = "combat and portals run in the scene";
        try
        {
            var combat = main.Combat ?? throw new InvalidOperationException("no dungeon, so no combat view");
            main.MovePlayerToFloor(0);
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var view = main.Player.View;
            var feet = new Vec3(view.Feet.X + 3, view.Feet.Y, view.Feet.Z);
            var enemy = combat.Sim.SpawnEnemy(EnemyKind.Husk, feet);

            // Fire both portals at the walls either side of the room.
            var east = view with { Forward = new Vec3(1, 0, 0) };
            var west = view with { Forward = new Vec3(-1, 0, 0) };
            combat.Step(1.0 / 60, east, new PlayerActions(false, false, false, PortalBlue: true));
            combat.Step(1.0 / 60, west, new PlayerActions(false, false, false, PortalOrange: true));
            for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            combat.UpdatePortalViews(main.Player.Camera);

            bool enemyDrawn = combat.GetNodeOrNull<MeshInstance3D>($"Enemy_{enemy.Id}") != null;
            bool portalsDrawn = combat.GetNodeOrNull<PortalView>("Portal_Blue") != null && combat.GetNodeOrNull<PortalView>("Portal_Orange") != null;
            bool linked = combat.Sim.Portals.Linked;

            bool ok = enemyDrawn && portalsDrawn && linked;
            if (ok) { _passed++; GD.Print($"  ok   {name}"); }
            else { _failed++; GD.PrintErr($"  FAIL {name}: enemyDrawn={enemyDrawn} portalsDrawn={portalsDrawn} linked={linked}"); }
        }
        catch (Exception e)
        {
            _failed++;
            GD.PrintErr($"  FAIL {name}: {e.Message}");
        }
    }

    private void Check(string name, Func<bool> test)
    {
        bool ok;
        try { ok = test(); }
        catch (Exception e)
        {
            GD.PrintErr($"  FAIL {name}: {e.Message}");
            ok = false;
        }
        if (ok) { _passed++; GD.Print($"  ok   {name}"); }
        else { _failed++; GD.PrintErr($"  FAIL {name}"); }
    }
}
