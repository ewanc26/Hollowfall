using Hollowfall.Core.Localization;

namespace Hollowfall.Core.Story.Dialogue;

/// <summary>
/// Everything a conversation can ask about: how the player is doing and what they have done, and what has been remembered from
/// earlier conversations (<see cref="Flags"/> and <see cref="VisitCount"/> are saved with the story progress). Immutable; the runner
/// makes a new one each time an effect changes something.
/// </summary>
public sealed record DialogueContext
{
    private static readonly IReadOnlySet<string> NoFlags = new HashSet<string>();

    /// <summary>The numeric seed, for choosing between a line's seeded variants.</summary>
    public long Seed { get; init; }

    /// <summary>The seed as the player typed it (a word or a number).</summary>
    public string SeedText { get; init; } = "";

    /// <summary>How many conversations have been started before this one.</summary>
    public int VisitCount { get; init; }

    /// <summary>Current health as a fraction of the maximum, 0 to 1.</summary>
    public double HealthFraction { get; init; } = 1;

    public int Deaths { get; init; }
    public int Kills { get; init; }

    /// <summary>The deepest dungeon floor reached this run, counted from 0; -1 for none.</summary>
    public int DeepestFloor { get; init; } = -1;

    /// <summary>How many floors the dungeon has.</summary>
    public int FloorCount { get; init; }

    public int NotesRead { get; init; }
    public bool HasKey { get; init; }

    /// <summary>Whether his gift has already been given this run.</summary>
    public bool GiftGiven { get; init; }

    /// <summary>Whether the ending has been reached on this seed (saved).</summary>
    public bool EndingReached { get; init; }

    /// <summary>The interface language. In Gàidhlig and Bilingual a few of his lines also come in Gàidhlig, with the English beneath.</summary>
    public Language Language { get; init; } = Language.English;

    /// <summary>What he remembers, as short ids.</summary>
    public IReadOnlySet<string> Flags { get; init; } = NoFlags;

    public bool Has(string flag) => Flags.Contains(flag);

    /// <summary>Whether the player has been down to the last floor.</summary>
    public bool FinalFloorReached => FloorCount > 0 && DeepestFloor >= FloorCount - 1;
}
