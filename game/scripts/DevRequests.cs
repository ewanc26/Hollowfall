namespace Hollowfall.Game;

/// <summary>
/// One-shot requests from the developer menu that must survive a scene reload: each is read once by the scene that
/// starts next and then cleared, so a later normal game is unaffected.
/// </summary>
public static class DevRequests
{
    public static bool GluedRoom { get; set; }

    public static string? ShowcasePage { get; set; }

    public static bool TakeGluedRoom()
    {
        bool wanted = GluedRoom;
        GluedRoom = false;
        return wanted;
    }

    public static string? TakeShowcasePage()
    {
        string? page = ShowcasePage;
        ShowcasePage = null;
        return page;
    }
}
