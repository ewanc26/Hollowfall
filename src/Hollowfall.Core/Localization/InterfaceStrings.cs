using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;

namespace Hollowfall.Core.Localization;

/// <summary>
/// The menus, settings, controls screen, HUD labels, hints, prompts and banners. English is exactly what the game showed before it
/// was localised. Gàidhlig is given only where a source supports it; every other key has null Gàidhlig and shows its English (they are
/// listed in docs/gaidhlig.md under "Needs a speaker"). Keys are written out in full in the game scripts so a test can find them.
/// </summary>
public static class InterfaceStrings
{
    private static LocEntry G(string key, string en, string gd, ReviewStatus status, string source, string note = "") => new(key, en, gd, status, source, note);

    private static LocEntry T(string key, string en) => new(key, en, null, ReviewStatus.NeedsReview, "", "no verified Gàidhlig; English is shown");

    /// <summary>The key for an action's label, e.g. <c>action.moveForward</c>.</summary>
    public static string ActionKey(GameAction a) => "action." + Camel(a.ToString());

    /// <summary>The key for a controls group heading, e.g. <c>group.movement</c>.</summary>
    public static string GroupKey(ActionGroup g) => "group." + Camel(g.ToString());

    /// <summary>The key for a style rank, e.g. <c>rank.rowdy</c>.</summary>
    public static string RankKey(StyleRank r) => "rank." + Camel(r.ToString());

    private static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];

    public static IReadOnlyList<LocEntry> All { get; } = Build();

    private static List<LocEntry> Build()
    {
        var list = new List<LocEntry>
        {
            // ---- shared menu words ----
            G("menu.play", "Play", "Cluich", ReviewStatus.Verified, "WK", "cluich is the imperative of the verb cluich, to play"),
            G("menu.settings", "Settings", "Roghainnean", ReviewStatus.Verified, "FF WK", "Firefox's own gd word for Settings; roghainn, an option or choice"),
            T("menu.controls", "Controls"),
            G("menu.quit", "Quit", "Fàg", ReviewStatus.Likely, "FF WK", "Firefox gd has 'Fàg an-seo'; fàg is leave/quit, transitive only, so a speaker should confirm the bare form"),
            G("menu.back", "Back", "Air ais", ReviewStatus.Likely, "WK", "air ais is the adverb 'back'; its use on a Back button is not confirmed"),
            T("menu.reset", "Reset"),

            // ---- title ----
            T("title.tagline", "A procedural dungeon crawler.\nMove fast. Heal by hurting things."),
            G("title.seed", "Seed", "Sìol", ReviewStatus.Verified, "WK", "sìol, seed (first sense)"),
            T("title.seedHint", "any text or number"),
            T("title.random", "Random"),

            // ---- pause ----
            G("pause.title", "PAUSED", "AIR STAD", ReviewStatus.Likely, "WK", "stad, a stop or pause; 'air stad' (at a stop) is reasoned, not quoted"),
            G("pause.resume", "Resume", "Lean air", ReviewStatus.Verified, "WK", "lean air, continue"),
            T("pause.respawn", "Respawn"),
            G("pause.journal", "Journal", "Leabhar-latha", ReviewStatus.Verified, "WK", "leabhar-latha, diary or journal"),
            T("pause.mainMenu", "Main menu"),

            // ---- journal ----
            G("journal.title", "JOURNAL", "LEABHAR-LATHA", ReviewStatus.Verified, "WK", "capitals of the entry above"),
            T("journal.notesRead", "{0}   -   Notes read: {1}"),
            T("journal.empty", "Nothing read yet. Notes lie against the walls of the dungeon: walk up to a scroll and use it."),

            // ---- controls ----
            T("controls.title", "CONTROLS"),
            T("controls.help", "Click a binding, then press the new key, button or stick direction. Esc cancels."),
            T("controls.colAction", "Action"),
            T("controls.colKeys", "Keyboard / mouse"),
            T("controls.colPad", "Gamepad"),
            T("controls.resetAll", "Reset to defaults"),
            T("controls.listenPad", "press a button or move a stick…"),
            T("controls.listenKey", "press a key or click…"),
            T("controls.binding", "Binding \"{0}\". Esc cancels."),
            T("controls.mouse", "mouse"),
            T("controls.now", "{0} is now {1}."),
            T("controls.swapUnbound", "{0} was used by {1}, which is now unbound."),
            T("controls.swapped", "{0} was used by {1}; the two swapped."),
            T("controls.inUse", "{0} is used by {1}, which cannot take another input. Nothing changed."),
            T("controls.reserved", "{0} cannot be used for {1}."),
            T("controls.wrongKind", "{0} cannot use {1}."),
            T("controls.same", "That binding is already set."),
            T("controls.resetDone", "Bindings reset to defaults."),
            G(GroupKey(ActionGroup.Movement), "MOVEMENT", "GLUASAD", ReviewStatus.Likely, "WK", "gluasad is the verbal noun of gluais, move"),
            T(GroupKey(ActionGroup.Look), "LOOK"),
            T(GroupKey(ActionGroup.Combat), "COMBAT"),
            T(GroupKey(ActionGroup.Portals), "PORTALS"),
            T(GroupKey(ActionGroup.System), "SYSTEM"),

            // ---- action labels ----
            G(ActionKey(GameAction.MoveForward), "Move forward", "Gluais air adhart", ReviewStatus.Likely, "WK", "gluais (move) + air adhart (forward)"),
            G(ActionKey(GameAction.MoveBack), "Move back", "Gluais air ais", ReviewStatus.Likely, "WK", "gluais (move) + air ais (back)"),
            T(ActionKey(GameAction.MoveLeft), "Move left"),
            T(ActionKey(GameAction.MoveRight), "Move right"),
            T(ActionKey(GameAction.LookUp), "Look up"),
            T(ActionKey(GameAction.LookDown), "Look down"),
            T(ActionKey(GameAction.LookLeft), "Look left"),
            T(ActionKey(GameAction.LookRight), "Look right"),
            G(ActionKey(GameAction.Jump), "Jump", "Leum", ReviewStatus.Likely, "WK", "leum, to jump; the bare verb is the imperative"),
            T(ActionKey(GameAction.FirePrimary), "Fire revolver"),
            T(ActionKey(GameAction.FireSecondary), "Fire shotgun"),
            T(ActionKey(GameAction.Parry), "Parry"),
            T(ActionKey(GameAction.PortalTeal), "Teal portal"),
            T(ActionKey(GameAction.PortalViolet), "Violet portal"),
            T(ActionKey(GameAction.Respawn), "Respawn"),
            T(ActionKey(GameAction.FlyToggle), "Free-fly"),
            T(ActionKey(GameAction.FlyDown), "Fly down"),
            T(ActionKey(GameAction.DebugInfo), "Debug overlay"),
            T(ActionKey(GameAction.Interact), "Open and use"),
            T(ActionKey(GameAction.ClosePortals), "Close portals (sever)"),
            T(ActionKey(GameAction.Pause), "Pause menu"),

            // ---- settings ----
            G("settings.title", "SETTINGS", "ROGHAINNEAN", ReviewStatus.Verified, "FF", "capitals of Firefox gd's 'Roghainnean'"),
            G("settings.language", "Language", "Cànan", ReviewStatus.Verified, "WK", "cànan, a language"),
            T("settings.mouseSensitivity", "Mouse sensitivity"),
            T("settings.padLookSpeed", "Gamepad look speed"),
            T("settings.deadzone", "Stick dead zone"),
            T("settings.fov", "Field of view"),
            T("settings.uiSize", "Interface size"),
            T("settings.autoScale", "Scale the interface with the window"),
            T("settings.master", "Master volume"),
            T("settings.effects", "Effects volume"),
            T("settings.ambience", "Ambience volume"),
            T("settings.music", "Music volume"),
            T("settings.viewDistance", "Render distance"),
            T("settings.chunks", "{0} chunks"),
            T("settings.prompts", "Controller prompts"),
            T("settings.promptsAuto", "Auto (follow controller)"),
            T("settings.invertMouse", "Invert mouse Y"),
            T("settings.invertPad", "Invert gamepad Y"),

            // ---- HUD ----
            new("hud.floor", "Floor {0} / {1}", "Làr {0} / {1}", ReviewStatus.NeedsReview, "WK", "làr is the floor of a room or the ground; whether it fits a level of a dungeon (or lobhta, a storey) needs a speaker, so it is held back"),
            G("hud.keys", "Keys {0}", "Iuchraichean {0}", ReviewStatus.Likely, "WK", "iuchraichean is the plural of iuchair, key; the number sits beside it, so no count agreement is needed"),
            T("hud.result.title", "FLOOR {0} COMPLETE"),
            T("hud.result.rank", "RANK  {0}"),
            T("hud.result.stats", "Time {0}    Kills {1}    Style {2}"),
            T("hud.result.restarts", "Restarts {0}  (−{0} points)"),

            // ---- hints under the HUD ----
            T("hint.revolver", "revolver"),
            T("hint.shotgun", "shotgun"),
            T("hint.parry", "parry"),
            T("hint.tealPortal", "teal portal"),
            T("hint.violetPortal", "violet portal"),
            T("hint.sever", "sever"),
            G("hint.open", "open", "fosgail", ReviewStatus.Likely, "WK", "fosgail, open; the bare verb is the imperative"),
            G("hint.jump", "jump", "leum", ReviewStatus.Likely, "WK", "leum, to jump"),
            G("hint.menu", "menu", "clàr-taice", ReviewStatus.Likely, "DRU", "a software translation of 'Menu'; not in the dictionaries checked"),

            // ---- prompts and panels ----
            G("prompt.read", "{0}  read: {1}", "{0}  leugh: {1}", ReviewStatus.Likely, "WK", "leugh, read (the imperative; Wiktionary quotes 'Leugh seo', Read this)"),
            G("prompt.speak", "{0}  speak", "{0}  bruidhinn", ReviewStatus.Likely, "WK", "bruidhinn, to speak or talk; the bare verb is the imperative"),
            T("prompt.closeHint", "{0} or {1} to close"),
            T("prompt.leaveHint", "{0} or {1} to leave"),

            // ---- banners ----
            G("banner.died", "YOU DIED", "THA THU MARBH", ReviewStatus.Likely, "WK", "Tha thu (you are) + marbh, dead"),
            T("banner.respawn", "{0} to respawn"),
            T("banner.enemies", "Enemies {0}: kill them to open the doors"),
            T("banner.clear", "CLEAR"),
            T("banner.floorDone", "FLOOR {0} COMPLETE  {1}  {2}"),
            T("banner.floorStats", "time {0}  kills {1}  style {2}"),
            T("banner.firstClear", "first clear"),
            T("banner.newBest", "NEW BEST  (was {0})"),
            T("banner.best", "best {0}"),
            G("banner.key", "KEY", "IUCHAIR", ReviewStatus.Likely, "WK", "iuchair, key"),
            T("banner.chest", "CHEST"),
            T("banner.lockedChest", "LOCKED CHEST"),
            T("banner.chestHealed", "CHEST  +{0} health"),
            T("banner.lockedChestHealed", "LOCKED CHEST OPENED  +{0} health"),
            T("banner.locked", "locked: find the key on this floor"),
            T("banner.spikes", "SPIKES  -{0}"),
            T("banner.noPortal", "can't place a portal there ({0})"),
            T("banner.meteor", "METEOR"),
            T("banner.meteor2", "METEOR x2"),
        };

        foreach (var rank in Enum.GetValues<StyleRank>().Where(r => r != StyleRank.None))
            list.Add(T(RankKey(rank), rank.ToString().ToUpperInvariant()));
        return list;
    }
}
