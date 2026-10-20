using Godot;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Story;

namespace Hollowfall.Game;

/// <summary>
/// Connects the story in Core to the screen: it decides when a note can be read, which floor card to show, and which line goes
/// with a boss, a death, a finished floor or the ending, and remembers what has been read. All the words and all the choices
/// come from <see cref="StoryPlan"/>; this class only reacts to what the game reports. It is a plain object, owned by Main,
/// so the smoke test can drive it without a running game.
/// </summary>
public sealed class StoryDirector
{
    private readonly StoryPlan _plan;
    private readonly StoryProgress _progress;
    private readonly string _seedName;
    private readonly string _savePath;
    private readonly IReadOnlyList<NoteSpot> _spots;
    private readonly StoryUi _ui;
    private readonly NotesView? _notes;
    private readonly Action<SoundId> _play;
    private readonly HashSet<int> _cardsShown = [];
    private readonly int _bossRoomId;
    private bool _bossIntroShown;
    private int _deaths;
    private double _endingDelay = -1;
    private bool _endingShown;

    /// <summary>Whether the ending card has been shown (for tests).</summary>
    public bool EndingShown => _endingShown;

    public StoryDirector(StoryPlan plan, StoryProgress progress, string seedName, string savePath, IReadOnlyList<NoteSpot> spots, int bossRoomId, StoryUi ui, NotesView? notes, Action<SoundId> play)
    {
        _plan = plan;
        _progress = progress;
        _seedName = seedName;
        _savePath = savePath;
        _spots = spots;
        _bossRoomId = bossRoomId;
        _ui = ui;
        _notes = notes;
        _play = play;
    }

    public StoryPlan Plan => _plan;

    public int BossRoomId => _bossRoomId;

    /// <summary>The label of the button that reads a note and dismisses a page, as the player has bound it (set by the host).</summary>
    public Func<string> InteractLabel { get; set; } = () => "Interact";

    /// <summary>The label of the menu button, for the hint on a page.</summary>
    public Func<string> MenuLabel { get; set; } = () => "Menu";

    public IReadOnlyList<NoteSpot> Spots => _spots;

    public bool IsRead(string noteId) => _progress.IsRead(_seedName, noteId);

    public int NotesRead => _progress.ReadIds(_seedName).Count;

    private string CloseHint => $"{InteractLabel()} or {MenuLabel()} to close";

    // ---- notes --------------------------------------------------------------------------------

    /// <summary>
    /// Called every physics step with what the player pressed. Shows the prompt over a nearby note, opens it on Interact and
    /// returns the actions the rest of the game should see: the press that read a note (or closed a page) is used up, so it
    /// never also opens a chest.
    /// </summary>
    public PlayerActions Filter(PlayerActions actions, Vec3 feet, bool alive)
    {
        if (_ui.SwallowInteract) actions = actions with { Interact = false };
        if (!alive || _ui.Modal)
        {
            _ui.SetPrompt(null);
            return actions;
        }

        var near = NotePlacer.Nearest(_spots, feet.X, feet.Y, feet.Z);
        _ui.SetPrompt(near is { } n ? $"{InteractLabel()}  read: {n.Note.Title}" : null);
        if (near is { } spot && actions.Interact)
        {
            Read(spot);
            actions = actions with { Interact = false };
        }
        return actions;
    }

    /// <summary>Opens a note, remembers it in the journal and saves.</summary>
    public void Read(NoteSpot spot)
    {
        if (_progress.MarkRead(_seedName, spot.Note.Id)) _progress.TrySave(_savePath);
        _notes?.Refresh();
        _play(SoundId.NoteRead);
        _ui.ShowNote(spot.Note, FloorLabel(spot.Note.Floor), CloseHint);
    }

    private string FloorLabel(int floor) => _plan.CardFor(floor) is { } card ? card.Title : $"Floor {floor + 1}";

    /// <summary>What the journal lists: every note read on this seed, with the floor it was found on.</summary>
    public IReadOnlyList<(string Floor, StoryNote Note)> Journal() =>
        [.. _plan.Journal(_progress.ReadIds(_seedName)).Select(n => (FloorLabel(n.Floor), n))];

    // ---- the telling --------------------------------------------------------------------------

    /// <summary>Whether the opening has already been shown for this seed.</summary>
    public bool IntroSeen => _progress.IntroSeen(_seedName);

    /// <summary>Shows the opening crawl, once per seed. Marked seen as soon as it is shown, so quitting during it does not repeat it.</summary>
    public bool ShowIntroIfNew()
    {
        if (_progress.IntroSeen(_seedName)) return false;
        _progress.MarkIntroSeen(_seedName);
        _progress.TrySave(_savePath);
        _ui.ShowCrawl(_plan.Title, _plan.Intro, CloseHint);
        return true;
    }

    /// <summary>Called each frame with the floor the player is on: shows a floor's card the first time it is entered.</summary>
    public void OnFloor(int floor)
    {
        if (floor < 0 || !_cardsShown.Add(floor) || _plan.CardFor(floor) is not { } card) return;
        // After a floor is finished its result is up for a moment; let it have the screen first.
        _ui.ShowCard(card, _cardsShown.Count > 1 ? 1.5 : 0.4);
        _play(SoundId.TitleCard);
    }

    /// <summary>Reacts to combat events: the boss's line, a finished floor's line, a death, and the ending.</summary>
    public void OnEvent(CombatEvent e)
    {
        switch (e)
        {
            case EncounterStarted s when s.Floor == _plan.FloorCount - 1 && s.RoomId == _bossRoomId && !_bossIntroShown:
                _bossIntroShown = true;
                _ui.ShowCaption($"WARDEN {_plan.WardenName.ToUpperInvariant()}\n{_plan.BossIntro}", 7);
                break;
            case FloorCompleted f when _plan.IsBossFloor(f.Floor):
                _endingDelay = 3.2;
                break;
            case FloorCompleted f:
                _ui.ShowCaption(_plan.FloorCompleteLine(f.Result.Overall), 5);
                break;
            case PlayerDied:
                _ui.ShowCaption(_plan.DeathLine(Math.Max(_lastFloor, 0), _deaths++), 3600);
                break;
        }
    }

    private int _lastFloor;

    /// <summary>Called by the host when the player respawns, to clear the death line.</summary>
    public void OnRespawn() => _ui.ClearCaption();

    /// <summary>Advances the delay before the ending card. Call every frame with the frame's delta.</summary>
    public void Tick(double delta, int floor)
    {
        _lastFloor = floor;
        if (_endingDelay < 0 || _ui.Modal) return;
        _endingDelay -= delta;
        if (_endingDelay <= 0) ShowEnding();
    }

    public void ShowEnding()
    {
        _endingDelay = -1;
        if (_endingShown) return;
        _endingShown = true;
        _progress.MarkEndingSeen(_seedName);
        _progress.TrySave(_savePath);
        _ui.ShowCrawl(_plan.Ending.Title, _plan.Ending.Paragraphs, CloseHint, 0.9f);
    }
}
