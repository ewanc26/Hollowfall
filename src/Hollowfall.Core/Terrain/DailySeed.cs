namespace Hollowfall.Core.Terrain;

/// <summary>
/// The daily dungeon: one seed for each UTC day, so everyone who plays on the same day descends the same floors. It is an
/// ordinary seed text, so it is shared, saved and replayed like any other; a daily run is only a way of choosing the seed.
/// </summary>
public static class DailySeed
{
    private const string Prefix = "daily-";

    /// <summary>The seed text for a day, for example <c>daily-2026-10-21</c>.</summary>
    public static string For(DateOnly day) => Prefix + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The seed for today in UTC, so the same dungeon is offered everywhere on the same day.</summary>
    public static string Today(DateTime utcNow) => For(DateOnly.FromDateTime(utcNow));
}
