using Hollowfall.Core.Combat;
using Hollowfall.Core.Localization;

namespace Hollowfall.Game;

/// <summary>
/// The pact panel between floors (docs/features.md 4.1). When a floor completes it asks the simulation for the run's offer — pacts
/// drawn deterministically from the seed and the floor index, none of them taken this run — and shows it on the story panel: a button
/// per pact with its name, boon and burden, and a way to take none. The game tree pauses while the panel is open, so the choice
/// happens between simulation steps: taking one calls <see cref="CombatSim.TakePact"/> and is recorded for the replay, exactly like a
/// respawn, and nothing about the panel can touch the fixed-step simulation's determinism.
/// </summary>
public sealed class PactDirector
{
    private readonly CombatSim _sim;
    private readonly StoryUi _ui;
    private readonly Loc _loc;
    private readonly Action<int, PactKind?> _notePact;
    private readonly Func<string> _interactLabel;
    private readonly Func<string> _menuLabel;
    private int _floor = -1;
    private IReadOnlyList<PactKind> _pending = [];

    public PactDirector(CombatSim sim, StoryUi ui, Loc loc, Action<int, PactKind?> notePact, Func<string> interactLabel, Func<string> menuLabel)
    {
        _sim = sim;
        _ui = ui;
        _loc = loc;
        _notePact = notePact;
        _interactLabel = interactLabel;
        _menuLabel = menuLabel;
    }

    /// <summary>True while this director's offer is on screen.</summary>
    public bool Offering { get; private set; }

    /// <summary>
    /// Set by a scripted run (the smoke test): every floor's offer is declined and recorded without a panel, so scripted
    /// bots never wait on a player. The sim's determinism is untouched either way.
    /// </summary>
    public bool AutoDecline { get; set; }

    /// <summary>Call for every completed floor: offers the pacts the run has not taken yet, or nothing when none remain.</summary>
    public void OnFloorCompleted(FloorCompleted e)
    {
        if (AutoDecline)
        {
            _notePact(e.Floor, null); // a decline, recorded like any other, so the run's replay stays whole
            return;
        }
        if (_sim.FloorCompleted(e.Floor)) Offer(e.Floor, _sim.PactOffer(e.Floor));
    }

    /// <summary>Shows the offer for a floor, pausing the game behind it (tests and demos call this directly).</summary>
    public void Offer(int floor, IReadOnlyList<PactKind> offers)
    {
        if (offers.Count == 0) return;
        _floor = floor;
        _pending = offers;
        Offering = true;
        var labels = offers.Select(PactLabel).ToList();
        labels.Add(_loc.Tr("pact.decline"));
        _ui.ShowDialogue(_loc.Tr("pact.title"), _loc.Tr("pact.strength"), labels, Choose, _loc.Tr("pact.hint", _interactLabel(), _menuLabel()));
    }

    private string PactLabel(PactKind pact) =>
        $"{_loc.Tr(InterfaceStrings.PactKey(pact, "name"))}\n" +
        $"{_loc.Tr("pact.boon")}: {_loc.Tr(InterfaceStrings.PactKey(pact, "boon"))}\n" +
        $"{_loc.Tr("pact.burden")}: {_loc.Tr(InterfaceStrings.PactKey(pact, "burden"))}";

    private void Choose(int index)
    {
        Offering = false;
        var kind = index >= 0 && index < _pending.Count ? (PactKind?)_pending[index] : null;
        if (kind is { } pact) _sim.TakePact(pact);
        _notePact(_floor, kind); // recorded between steps, like a respawn, so the replay reproduces the choice
        _ui.Close();
    }

    /// <summary>Presses a choice button as the player would (for tests and demos).</summary>
    public void Press(int index) => _ui.PressDialogueChoice(index);
}
