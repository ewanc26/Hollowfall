using Godot;
using Hollowfall.Core.Dungeon;

namespace Hollowfall.Game;

/// <summary>
/// The corner map: the part of the floor the player has seen, centred on them, north up. It is one small image that is
/// rewritten only when something new is revealed, and drawn scrolled to the player.
/// </summary>
public partial class MiniMap : Control
{
    private const int Window = 44; // cells shown across
    private const float Size = 176;

    private static readonly Color WallColour = new(0.55f, 0.62f, 0.8f);
    private static readonly Color FloorColour = new(0.2f, 0.25f, 0.36f);
    private static readonly Color DoorColour = new(0.95f, 0.72f, 0.25f);
    private static readonly Color PillarColour = new(0.38f, 0.43f, 0.56f);
    private static readonly Color StairsColour = new(0.3f, 0.95f, 0.8f);
    private static readonly Color HallColour = new(0.28f, 0.62f, 0.66f);
    private static readonly Color PitColour = new(0.06f, 0.05f, 0.08f);

    private readonly Image _image = Image.CreateEmpty(FloorLayout.Size + 2 * Window, FloorLayout.Size + 2 * Window, false, Image.Format.Rgba8);
    private ImageTexture? _texture;
    private FloorMap? _map;
    private int _floor = -1, _version = -1;
    private Vector2 _player;
    private float _yaw;

    public MiniMap()
    {
        CustomMinimumSize = new Vector2(Size, Size);
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        ClipContents = true;
    }

    /// <summary>Points the map at a floor and the player's place on it (in layout cells) and facing.</summary>
    public void Show(FloorMap map, int floor, Vector2 playerCell, float yaw)
    {
        if (_map != map || _floor != floor || _version != map.Version) Rebuild(map, floor);
        _player = playerCell;
        _yaw = yaw;
        QueueRedraw();
    }

    private void Rebuild(FloorMap map, int floor)
    {
        _map = map;
        _floor = floor;
        _version = map.Version;
        _image.Fill(Colors.Transparent);
        for (int z = 0; z < FloorLayout.Size; z++)
            for (int x = 0; x < FloorLayout.Size; x++)
            {
                var cell = map.At(floor, x, z);
                if (cell == MapCell.Unknown) continue;
                _image.SetPixel(x + Window, z + Window, cell switch
                {
                    MapCell.Wall => WallColour,
                    MapCell.Door => DoorColour,
                    MapCell.Pillar => PillarColour,
                    MapCell.Stairs => StairsColour,
                    MapCell.Hall => HallColour,
                    MapCell.Pit => PitColour,
                    _ => FloorColour,
                });
            }
        if (_texture == null) _texture = ImageTexture.CreateFromImage(_image);
        else _texture.Update(_image);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size, Size)), new Color(0.03f, 0.03f, 0.05f, 0.7f));
        if (_texture == null) return;

        float scale = Size / Window;
        var centre = _player + new Vector2(Window, Window);
        var source = new Rect2(centre - new Vector2(Window, Window) / 2, new Vector2(Window, Window));
        DrawTextureRectRegion(_texture, new Rect2(Vector2.Zero, new Vector2(Size, Size)), source);

        // The player: an arrow pointing the way they face (yaw 0 looks down -Z, which is up on the map).
        var middle = new Vector2(Size, Size) / 2;
        var forward = new Vector2(-Mathf.Sin(_yaw), -Mathf.Cos(_yaw));
        var side = new Vector2(-forward.Y, forward.X);
        float r = scale * 1.8f;
        DrawColoredPolygon([middle + forward * r, middle - forward * r * 0.7f + side * r * 0.6f, middle - forward * r * 0.7f - side * r * 0.6f], new Color(1f, 0.95f, 0.5f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size, Size)), new Color(0.4f, 0.45f, 0.6f, 0.8f), false, 2);
    }
}
