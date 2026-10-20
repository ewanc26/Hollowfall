using Godot;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Story;
using Hollowfall.Core.Story.Dialogue;

namespace Hollowfall.Game;

/// <summary>
/// Connects the Wulver in Core to the screen: he greets you once as you come up the hill (no key press), shows "speak" when you are near
/// him, and on Interact opens his conversation tree in the choice panel, plays his greeting sound, hands over his gift when a node gives
/// it and saves what he now remembers with the story progress. A plain object owned by Main, like <see cref="StoryDirector"/>, so the
/// smoke test can drive it. The press that opens the panel is used up, so it never also opens a chest.
/// </summary>
public sealed class WulverDirector
{
    private const double GreetingSeconds = 4.5;

    private readonly WulverEncounter _encounter;
    private readonly StoryUi _ui;
    private readonly WulverView? _view;
    private readonly PlayerVitals _vitals;
    private readonly Perks _perks;
    private readonly Action<SoundId> _play;
    private readonly StoryProgress _progress;
    private readonly string _seedName, _savePath;

    /// <summary>The label of the Interact button as the player has bound it (set by the host).</summary>
    public Func<string> InteractLabel { get; set; } = () => "Interact";

    /// <summary>The label of the menu button (set by the host).</summary>
    public Func<string> MenuLabel { get; set; } = () => "Menu";

    /// <summary>How the player is doing, for his answers (set by the host).</summary>
    public Func<WulverFacts> Facts { get; set; } = () => default;

    public WulverEncounter Encounter => _encounter;

    /// <summary>How many greetings he has given (for tests).</summary>
    public int Greeted { get; private set; }

    public WulverDirector(WulverEncounter encounter, PlayerVitals vitals, Perks perks, StoryUi ui, WulverView? view, StoryProgress progress, string seedName, string savePath, Action<SoundId> play)
    {
        _encounter = encounter;
        _vitals = vitals;
        _perks = perks;
        _ui = ui;
        _view = view;
        _progress = progress;
        _seedName = seedName;
        _savePath = savePath;
        _play = play;
        _ui.Closed += OnClosed;
    }

    /// <summary>Called every physics step after the story's own filter. Returns the actions with the press used up if it spoke to him.</summary>
    public PlayerActions Filter(PlayerActions actions, Vec3 feet, bool alive)
    {
        if (!alive || _ui.Modal) return actions;

        // He notices you as you come up the hill and greets you once, without a key press.
        if (_encounter.Approach(feet.X, feet.Y, feet.Z) is { } greeting)
        {
            _ui.ShowSpeech(WulverDialogue.Name, greeting, GreetingSeconds);
            Greeted++;
        }
        if (!_encounter.IsNear(feet.X, feet.Y, feet.Z)) return actions;

        // While his greeting is up the words are the thing to read; the prompt would sit on top of them.
        _ui.SetPrompt(_ui.Speaking ? null : $"{InteractLabel()}  speak");
        if (actions.Interact)
        {
            Talk();
            actions = actions with { Interact = false };
        }
        return actions;
    }

    /// <summary>Opens the conversation at the entry that fits the player.</summary>
    public void Talk()
    {
        var runner = _encounter.Begin(Facts() with { EndingReached = _progress.EndingSeen(_seedName) }, _vitals, _perks);
        _play(SoundId.WulverGreeting);
        _ui.ClearCaption();
        Save();
        if (runner.Finished) return;
        _ui.ShowDialogue(WulverDialogue.Name, runner.Text, Labels(runner), Choose, $"{InteractLabel()} or {MenuLabel()} to leave");
        if (_encounter.GiftTaken) _view?.SetGiftTaken(true);
    }

    private static List<string> Labels(DialogueRunner runner) => [.. runner.Choices.Select(c => c.Text)];

    private void Choose(int index)
    {
        var runner = _encounter.Conversation;
        if (runner == null || !_encounter.Choose(index, _vitals, _perks))
            return;
        if (_encounter.GiftTaken) _view?.SetGiftTaken(true);
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
        if (_encounter.Conversation == null) return;
        _encounter.End();
        Save();
    }

    private void Save()
    {
        _progress.SetWulver(_seedName, _encounter.Flags, _encounter.Visits);
        _progress.TrySave(_savePath);
    }
}
