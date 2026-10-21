using Godot;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Localization;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;

namespace Hollowfall.Game;

/// <summary>
/// Connects the cat in Core to the screen: a one-line caption the first time he comes into view, "speak" when you are near, and on Interact his
/// conversation in the choice panel. Sitting with him gives a little warmth (see <see cref="CatEncounter"/>), once per run, and what he now
/// remembers is saved with the story progress. A plain object owned by Main, like <see cref="WulverDirector"/>, so the smoke test can drive it.
/// The press that opens the panel is used up, so it never also opens a chest.
/// </summary>
public sealed class CatDirector
{
    private const double CaptionSeconds = 4.0;

    private readonly CatEncounter _encounter;
    private readonly StoryUi _ui;
    private readonly PlayerVitals _vitals;
    private readonly Action<SoundId, Vector3?> _play;
    private readonly StoryProgress _progress;
    private readonly string _seedName, _savePath;
    private bool _warmthShown;

    /// <summary>The label of the Interact button as the player has bound it (set by the host).</summary>
    public Func<string> InteractLabel { get; set; } = () => "Interact";

    /// <summary>The label of the menu button (set by the host).</summary>
    public Func<string> MenuLabel { get; set; } = () => "Menu";

    public Loc Loc { get; set; } = new();

    public CatEncounter Encounter => _encounter;

    /// <summary>How many times the one-line caption has been shown (for tests).</summary>
    public int Sighted { get; private set; }

    public CatDirector(CatEncounter encounter, PlayerVitals vitals, StoryUi ui, StoryProgress progress, string seedName, string savePath, Action<SoundId, Vector3?> play)
    {
        _encounter = encounter;
        _vitals = vitals;
        _ui = ui;
        _progress = progress;
        _seedName = seedName;
        _savePath = savePath;
        _play = play;
        _ui.Closed += OnClosed;
    }

    private Vector3 At => new((float)_encounter.Spot.X, (float)_encounter.Spot.Y + 0.4f, (float)_encounter.Spot.Z);

    /// <summary>Called every physics step after the Wulver's filter. Returns the actions with the press used up if it spoke to him.</summary>
    public PlayerActions Filter(PlayerActions actions, Vec3 feet, bool alive)
    {
        if (!alive || _ui.Modal) return actions;

        if (_encounter.Sighting(feet.X, feet.Y, feet.Z) is { } caption)
        {
            _ui.ShowCaption(caption, CaptionSeconds);
            Sighted++;
        }
        if (!_encounter.IsNear(feet.X, feet.Y, feet.Z)) return actions;

        _ui.SetPrompt(_ui.Speaking ? null : $"{InteractLabel()}  speak");
        if (actions.Interact)
        {
            Talk();
            actions = actions with { Interact = false };
        }
        return actions;
    }

    /// <summary>Opens the conversation at the entry that fits.</summary>
    public void Talk()
    {
        var runner = _encounter.Begin(_vitals);
        _play(SoundId.CatMeow, At);
        _ui.ClearCaption();
        Save();
        if (runner.Finished) return;
        _ui.ShowDialogue(CatDialogue.Name, runner.Text, Labels(runner), Choose, $"{InteractLabel()} or {MenuLabel()} to leave");
    }

    private static List<string> Labels(DialogueRunner runner) => [.. runner.Choices.Select(c => c.Text)];

    private void Choose(int index)
    {
        var runner = _encounter.Conversation;
        bool hadGift = _encounter.GiftTaken;
        if (runner == null || !_encounter.Choose(index, _vitals))
            return;
        if (!hadGift && _encounter.GiftTaken) _warmthShown = true;
        Save();
        if (runner.Finished)
        {
            _ui.Close();
            return;
        }
        _ui.UpdateDialogue(runner.Text, Labels(runner));
    }

    /// <summary>Closing the panel by any route ends the conversation; what was chosen is already saved.</summary>
    private void OnClosed()
    {
        if (_encounter.Conversation == null && !_warmthShown) return;
        _encounter.End();
        Save();
        if (_warmthShown)
        {
            _warmthShown = false;
            _ui.ShowCaption(Loc.Tr("cat.warmth"), 3.5);
        }
    }

    private void Save()
    {
        _progress.SetCat(_seedName, _encounter.Flags, _encounter.Visits);
        _progress.TrySave(_savePath);
    }
}
