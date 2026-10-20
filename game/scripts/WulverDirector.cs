using Godot;
using Hollowfall.Core.Audio;
using Hollowfall.Core.Combat;
using Hollowfall.Core.Story;

namespace Hollowfall.Game;

/// <summary>
/// Connects the Wulver in Core to the screen: shows "speak" when the player is near him, and on Interact lets him speak (his name over
/// his line), plays his greeting and applies his gift once. A plain object owned by Main, like <see cref="StoryDirector"/>, so the smoke
/// test can drive it. The press that speaks to him is used up, so it never also opens a chest.
/// </summary>
public sealed class WulverDirector
{
    private const double LineSeconds = 9;

    private readonly WulverEncounter _encounter;
    private readonly StoryUi _ui;
    private readonly WulverView? _view;
    private readonly PlayerVitals _vitals;
    private readonly Perks _perks;
    private readonly Action<SoundId> _play;

    /// <summary>The label of the Interact button as the player has bound it (set by the host).</summary>
    public Func<string> InteractLabel { get; set; } = () => "Interact";

    public WulverEncounter Encounter => _encounter;

    /// <summary>What he last said (for tests).</summary>
    public WulverSpeech? LastSpeech { get; private set; }

    public WulverDirector(WulverEncounter encounter, PlayerVitals vitals, Perks perks, StoryUi ui, WulverView? view, Action<SoundId> play)
    {
        _encounter = encounter;
        _vitals = vitals;
        _perks = perks;
        _ui = ui;
        _view = view;
        _play = play;
    }

    /// <summary>Called every physics step after the story's own filter. Returns the actions with the press used up if it spoke to him.</summary>
    public PlayerActions Filter(PlayerActions actions, Vec3 feet, bool alive)
    {
        if (!alive || _ui.Modal) return actions;
        if (!_encounter.IsNear(feet.X, feet.Y, feet.Z)) return actions;

        // While he is speaking the line is the thing to read; the prompt would sit on top of it.
        _ui.SetPrompt(_ui.Speaking ? null : $"{InteractLabel()}  speak");
        if (actions.Interact)
        {
            Speak();
            actions = actions with { Interact = false };
        }
        return actions;
    }

    public void Speak()
    {
        var speech = _encounter.Speak(_vitals, _perks);
        LastSpeech = speech;
        _play(SoundId.WulverGreeting);
        _ui.ShowSpeech(speech.Speaker, speech.Text, LineSeconds);
        if (speech.GiftGiven) _view?.SetGiftTaken(true);
    }
}
