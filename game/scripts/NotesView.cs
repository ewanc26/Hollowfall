using Godot;
using Hollowfall.Core.Models;
using Hollowfall.Core.Story;

namespace Hollowfall.Game;

/// <summary>
/// Draws the lore notes as low-poly scrolls lying against the dungeon walls. An unread scroll has a glowing amber seal that
/// breathes slowly, so it can be spotted from across a room; a read one has gone dark. Where the notes are is decided in Core
/// (<see cref="NotePlacer"/>); this only draws them.
/// </summary>
public partial class NotesView : Node3D
{
    private readonly List<(NoteSpot Spot, ModelRig Rig)> _scrolls = [];
    private Func<string, bool> _isRead = _ => false;
    private double _time;

    public int Count => _scrolls.Count;

    public void Initialize(IReadOnlyList<NoteSpot> spots, Func<string, bool> isRead)
    {
        _isRead = isRead;
        foreach (var spot in spots)
        {
            var rig = ModelFactory.Build(ModelCatalog.Scroll, castShadows: false);
            rig.Root.Position = new Vector3((float)spot.X, (float)spot.Y, (float)spot.Z);
            // The scroll lies along the wall, so its length runs across the way the wall faces.
            rig.Root.Rotation = new Vector3(0, (float)Math.Atan2(-spot.FaceX, -spot.FaceZ) + (float)((spot.CellX * 7 + spot.CellZ * 3) % 5 - 2) * 0.06f, 0);
            AddChild(rig.Root);
            _scrolls.Add((spot, rig));
        }
        Refresh();
    }

    /// <summary>Darkens the seal of every note that has been read.</summary>
    public void Refresh()
    {
        foreach (var (spot, rig) in _scrolls)
            rig.SetGlow("seal", _isRead(spot.Note.Id) ? new Color(0.18f, 0.1f, 0.06f) : Colors.White);
    }

    public void Update(double delta)
    {
        _time += delta;
        foreach (var (spot, rig) in _scrolls)
            if (rig.Part("seal") is { } seal && !_isRead(spot.Note.Id))
                seal.Scale = Vector3.One * (float)(1.0 + 0.35 * Math.Sin(_time * 2.2 + spot.Index));
            else if (rig.Part("seal") is { } dead)
                dead.Scale = Vector3.One * 0.7f;
    }
}
