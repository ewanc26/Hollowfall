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

    /// <summary>The key for a pact's words, e.g. <c>pact.hollowHeart.name</c>.</summary>
    public static string PactKey(PactKind pact, string part) => "pact." + Camel(pact.ToString()) + "." + part;

    private static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];

    public static IReadOnlyList<LocEntry> All { get; } = Build();

    private static List<LocEntry> Build()
    {
        var list = new List<LocEntry>
        {
            // ---- shared menu words ----
            G("menu.play", "Play", "Cluich", ReviewStatus.Verified, "WK", "cluich is the imperative of the verb cluich, to play"),
            G("menu.settings", "Settings", "Roghainnean", ReviewStatus.Verified, "FF WK", "Firefox's own gd word for Settings; roghainn, an option or choice"),
            G("menu.controls", "Controls", "Smachd", ReviewStatus.Likely, "WK", "smachd is the noun control or command; used as a heading here, a speaker should say whether the plural smachdan suits better"),
            G("menu.quit", "Quit", "Fàg an-seo", ReviewStatus.Verified, "FF WK", "Firefox gd's Quit on Windows and Linux (macOS has 'Fàg' plus the app's name); fàg is leave or quit"),
            G("menu.back", "Back", "Air ais", ReviewStatus.Likely, "WK", "air ais is the adverb 'back'; its use on a Back button is not confirmed"),
            G("menu.reset", "Reset", "Aisig", ReviewStatus.Likely, "FF", "from Firefox gd's restoration wording (Aisig na bun-roghainnean); the bare noun for a Reset button is not quoted"),

            // ---- title ----
            G("title.tagline", "A procedural dungeon crawler.\nMove fast. Heal by hurting things.", "Sealgair-dhùn air a chruthachadh le àireamh.\nGluais luath. Faigh slàint le leòn.", ReviewStatus.NeedsReview, "WK", "composed from sourced words (sealgair, hunter; dùn, fortress; àireamh, number; luath, fast; slàint, health; leòn, wounding); a speaker should rewrite the line"),
            G("title.seed", "Seed", "Sìol", ReviewStatus.Verified, "WK", "sìol, seed (first sense)"),
            G("title.seedHint", "any text or number", "teacsa no àireamh sam bith", ReviewStatus.Likely, "WK", "teacsa, text; àireamh, number; sam bith, any"),
            G("title.random", "Random", "Air thuaiream", ReviewStatus.Likely, "WK", "air thuaiream, at random (idiom); whether it suits a Random button needs a speaker"),
            G("title.daily", "Daily", "Làitheil", ReviewStatus.Verified, "WK", "làitheil, daily or everyday (adjective)"),
            G("title.board", "Verified today", "Air a dhearbhadh an-diugh", ReviewStatus.Likely, "WK", "dearbh, verify (air a dhearbhadh, verified); an-diugh, today; composed"),
            G("title.boardNone", "No verified run yet today.", "Chan eil ruith dhearbhte ann fhathast an-diugh.", ReviewStatus.Likely, "WK", "ruith, a run or race; chan eil … fhathast, there is not … yet; composed"),
            G("title.boardRow", "Floor {0}: grade {1}, {2} points, {3} s", "Làr {0}: inbhe {1}, {2} puing, {3} s", ReviewStatus.NeedsReview, "WK", "làr as held back for hud.floor; inbhe as hud.result.rank; puing, point; composed"),

            // ---- pause ----
            G("pause.title", "PAUSED", "AIR STAD", ReviewStatus.NeedsReview, "WK FF", "held back: Firefox gd's Pause is the command 'Cuir 'na stad'; 'air stad' (at a stop, from stad, a stop or pause) is a guess at a heading that names the state"),
            G("pause.resume", "Resume", "Lean air", ReviewStatus.Verified, "FF WK", "Firefox gd's Resume (downloads) is 'Lean air'; Wiktionary lists lean air as continue"),
            G("pause.respawn", "Respawn", "Tilleadh", ReviewStatus.Likely, "WK", "tilleadh, the act of returning (till, return); the gaming sense of respawn is coined and needs a speaker"),
            G("pause.standing", "Standing", "Seasamh", ReviewStatus.Likely, "WK", "seasamh is the verbal noun of seas, to stand, and is listed as position or attitude; its use as this screen's name is not quoted"),
            G("pause.character", "Character", "Caractar", ReviewStatus.Likely, "WK", "caractar, character (the borrowed word); the sense of a game persona needs a speaker"),
            G("pause.bag", "Bag", "Poca", ReviewStatus.Verified, "WK", "poca, bag or sack"),
            G("bag.title", "Bag", "POCA", ReviewStatus.Verified, "WK", "capitals of poca, bag"),
            G("bag.slots", "Slots used: {0} of {1}", "Àitichean air a chleachdadh: {0} à {1}", ReviewStatus.NeedsReview, "WK", "àite, place (àitichean, places) standing in for slot; air a chleachdadh, in use; à, out of; composed"),
            G("bag.coin", "Coin {0}", "Bonn {0}", ReviewStatus.Verified, "WK", "bonn, coin (one of its senses); the number sits beside it"),
            G("bag.none", "Nothing is carried yet.", "Chan eil dad air a ghiùlan fhathast.", ReviewStatus.Likely, "WK", "dad, anything; giùlan, carrying; composed"),
            G("bag.wear", "Wear", "Caith", ReviewStatus.Likely, "WK", "caith, to wear (clothes); the bare verb is the imperative"),
            G("bag.takeOff", "Take off", "Thoir air falbh", ReviewStatus.Likely, "WK", "thoir, take (imperative); air falbh, away or off; the phrase for taking clothes off needs a speaker"),
            G("bag.worn", "worn", "caithte", ReviewStatus.Likely, "WK", "caithte, worn out or used up, the past participle of caith"),
            G("bag.ready", "Ready", "Deiseil", ReviewStatus.Verified, "WK", "deiseil, ready or prepared"),
            G("bag.readyMark", "ready", "deiseil", ReviewStatus.Verified, "WK", "same word as bag.ready, lower case for the mark beside an item"),
            G("bag.readyHint", "The ready item is what the quick-use button spends in a run; only one comes with you.", "'S e an nì deiseil an tè a bhios putan grad-chleachdaidh a' chosgadh ann an ruith; cha tig ach fear leat", ReviewStatus.NeedsReview, "WK", "nì, item or thing; deiseil as bag.ready; grad, quick; cosg, spend; ruith, run; composed, a speaker should check it"),
            G("bag.wearHint", "Worn charms and relics take effect from your next run.", "Bidh buaidh aig na seunan agus na taisgeulan caithte bhon ath-ruith.", ReviewStatus.NeedsReview, "WK", "buaidh, effect; seun, charm; taisgeul, a relic or treasure (coined here); caithte as bag.worn; composed, a speaker should check it"),

            // ---- the character (progression) ----
            G("character.title", "CHARACTER", "CARACTAR", ReviewStatus.Likely, "WK", "capitals of caractar above"),
            G("character.none", "No character yet. Defeat something to begin.", "Chan eil caractar ann fhathast. Buail rudeigin airson tòiseachadh.", ReviewStatus.Likely, "WK", "buail, strike; rudeigin, something; tòiseachadh, beginning; composed"),
            G("character.level", "Level {0}", "Ìre {0}", ReviewStatus.Verified, "WK", "ìre, degree or level; the number sits beside it"),
            G("character.nextLevel", "{0} of {1} experience to the next level", "{0} à {1} eòlas chun na h-ath ìre", ReviewStatus.Likely, "WK", "eòlas, experience (also knowledge); chun na h-ath ìre, towards the next level; composed"),
            G("character.maxLevel", "Highest level reached", "An ìre as àirde a ràinig thu", ReviewStatus.Likely, "WK", "as àirde, highest; ràinig, reached; composed"),
            G("character.points", "Points to spend: {0}", "Puinn ri chaitheamh: {0}", ReviewStatus.Likely, "WK", "puing, point (genitive plural puinn); caitheamh, spending (from caith, the verb also used for Wear); composed"),
            G("character.vigour", "Vigour {0}", "Neart {0}", ReviewStatus.Verified, "WK", "neart, strength, power or vigour; the number sits beside it"),
            G("character.vigourHint", "+10 maximum health a point", "+10 slàinte as motha airson gach puing", ReviewStatus.Likely, "WK", "slàinte, health; as motha, most or maximum; gach, each; composed"),
            G("character.might", "Might {0}", "Cumhachd {0}", ReviewStatus.Verified, "WK", "cumhachd, power or might; the number sits beside it"),
            G("character.mightHint", "+5 percent damage from guns and parries a point", "+5 ceudad bho ghunnaichean agus dìon gach puing", ReviewStatus.NeedsReview, "WK", "ceudad, per cent; gunna, gun; dìon, defence used for parry; composed, a speaker should check it"),
            G("character.nextRun", "Points take effect from your next run. A run keeps the stats it began with.", "Bidh puingean an gnìomh bhon ath-ruith. Cumaidh an ruith na staitist leis an tòisich e", ReviewStatus.NeedsReview, "WK", "gnìomh, action or effect; staitist is the borrowed plural of statistic; tòisich, begin; composed, a speaker should check it"),

            // ---- quests (progression) ----
            G("quest.title", "QUESTS", "IARRTASAN", ReviewStatus.Likely, "WK", "iarrtas, a request or errand (iarrtasan, plural); the quest sense is from games and needs a speaker"),
            G("quest.none", "No quests yet. Ask among the Folk.", "Chan eil iarrtas ann fhathast. Faighnich am measg muinntir an dùin.", ReviewStatus.NeedsReview, "WK", "faighnich, ask; am measg, among; muinntir, people; dùin, of the fortress; composed"),
            G("quest.husks", "Clear the Husks", "Falamhaich na Sligich", ReviewStatus.Likely, "WK", "falamhaich, empty or clear out (imperative); Slige is the Gàidhlig name of the Husks in docs/mythology.md"),
            G("quest.spitters", "Clear the Spitters", "Falamhaich na Plathairean", ReviewStatus.Likely, "WK", "as above; Plathair is the Gàidhlig name of the Spitters in docs/mythology.md"),
            G("quest.giver", "Given by {0}", "Bho {0}", ReviewStatus.Likely, "WK", "bho, from; 'given by' reads as 'from' in Gàidhlig"),
            G("quest.progress", "{0} of {1} defeated", "{0} à {1} air an ceannsachadh", ReviewStatus.Likely, "WK", "ceannsaich, conquer or subdue; air an ceannsachadh, defeated; composed"),
            G("quest.reward", "Reward: {0} experience, {1} coin and {2}", "Duais: {0} eòlas, {1} bonn agus {2}", ReviewStatus.Likely, "WK", "duais, reward or prize; bonn, coin as in bag.coin; composed"),
            G("quest.take", "Take", "Gabh", ReviewStatus.Likely, "WK", "gabh, take (imperative); its use on a Take button is not confirmed"),
            G("quest.taken", "Taken", "Air a ghabhail", ReviewStatus.Likely, "WK", "gabh, take; air a ghabhail, taken"),
            G("quest.done", "Finished", "Deiseil", ReviewStatus.Verified, "WK", "deiseil, ready, prepared or finished"),
            G("quest.refused", "Refused", "Air a dhiùltadh", ReviewStatus.Likely, "WK", "diùlt, refuse; air a dhiùltadh, refused"),
            G("quest.nextRun", "Quests count the kills you make from now on, in every run.", "Cunntaidh na h-iarrtanas na marbhadh a nì thu bho seo a-mach, anns gach ruith", ReviewStatus.NeedsReview, "WK", "cunnt, count; iarrtas, errand as quest.title; marbhadh, killing; bho seo a-mach, from now on; composed, a speaker should check it"),
            G("pause.quests", "Quests", "Iarrtasan", ReviewStatus.Likely, "WK", "plural of iarrtas, an errand, as quest.title"),
            G("pause.journal", "Journal", "Leabhar-latha", ReviewStatus.Verified, "WK", "leabhar-latha, diary or journal"),
            G("pause.mainMenu", "Main menu", "A' phrìomh chlàr-taice", ReviewStatus.Likely, "WK", "prìomh, main or chief; clàr-taice, menu (as hint.menu); lenited and slenderised after the feminine article; composed"),

            // ---- journal ----
            G("journal.title", "JOURNAL", "LEABHAR-LATHA", ReviewStatus.Verified, "WK", "capitals of the entry above"),
            G("journal.notesRead", "{0}   -   Notes read: {1}", "{0}   -   Nòtaichean air an leughadh: {1}", ReviewStatus.Likely, "WK", "nòta, note; air an leughadh, read (leugh as prompt.read); the shape of the line is kept"),
            G("journal.empty", "Nothing read yet. Notes lie against the walls of the dungeon: walk up to a scroll and use it.", "Chan eil dad air a leughadh fhathast. Tha nòtaichean ri na ballachan anns an dùn: coisich gu sgrìoll agus cleachd e.", ReviewStatus.NeedsReview, "WK", "balla, wall; coisich, walk (imperative); sgrìoll, scroll (the borrowed word); cleachd, use (as Interact); composed, a speaker should check it"),

            // ---- controls ----
            G("controls.title", "CONTROLS", "SMACHD", ReviewStatus.Likely, "WK", "capitals of smachd, control or command; the heading use needs a speaker"),
            G("controls.help", "Click a binding, then press the new key, button or stick direction. Esc cancels.", "Briog air ceangal, agus an uair sin brùth air a' phutan, an iuchair ùr no taobh a' bhata. Sguiridh Esc dheth", ReviewStatus.NeedsReview, "WK", "briog, click; ceangal, binding; brùth, press; putan, button; iuchair, key; ùr, new; bata, stick; composed sentence, a speaker should check it"),
            G("controls.colAction", "Action", "Gnìomh", ReviewStatus.Verified, "WK", "gnìomh, act or action"),
            G("controls.colKeys", "Keyboard / mouse", "Meur-chlàr / luch", ReviewStatus.Likely, "WK", "meur-chlàr, keyboard, and luch, mouse; the slash is kept from the English"),
            G("controls.colPad", "Gamepad", "Gèama-phàd", ReviewStatus.NeedsReview, "WK", "a coined compound of gèama, game, and the borrowed pàd, pad; gd has no settled word for gamepad"),
            G("controls.resetAll", "Reset to defaults", "Aisig na bun-roghainnean", ReviewStatus.Likely, "FF", "Firefox gd uses this for 'Restore Defaults'; whether it suits 'Reset to defaults' needs a speaker"),
            G("controls.listenPad", "press a button or move a stick…", "brùth putan no gluais bata…", ReviewStatus.Likely, "WK", "brùth, press; putan, button; gluais, move (as the movement actions); bata, stick; composed"),
            G("controls.listenKey", "press a key or click…", "brùth iuchair no briog…", ReviewStatus.Likely, "WK", "brùth, press; iuchair, key; briog, click; composed"),
            G("controls.binding", "Binding \"{0}\". Esc cancels.", "Ceangal \"{0}\". Sguiridh Esc dheth.", ReviewStatus.NeedsReview, "WK", "ceangal, a binding or tie; sgir dheth, stop or cancel; the sentence is composed"),
            G("controls.mouse", "mouse", "luch", ReviewStatus.Verified, "WK", "luch, mouse, including the computing sense"),
            G("controls.now", "{0} is now {1}.", "'S e {1} a th' ann an {0} a-nis.", ReviewStatus.Likely, "WK", "'s e … a th' ann, it is; a-nis, now; composed"),
            G("controls.swapUnbound", "{0} was used by {1}, which is now unbound.", "Bha {0} aig {1}; tha {1} gun cheangal a-nis.", ReviewStatus.Likely, "WK", "gun cheangal, without a binding; composed"),
            G("controls.swapped", "{0} was used by {1}; the two swapped.", "Bha {0} aig {1}; chaidh an dà chur an malairt.", ReviewStatus.Likely, "WK", "cuir an malairt, exchange or swap; chaidh … , was …; composed"),
            G("controls.inUse", "{0} is used by {1}, which cannot take another input. Nothing changed.", "Tha {0} aig {1}. Cha do dh'atharraich dad.", ReviewStatus.Likely, "WK", "atharraich, change; cha do dh'atharraich dad, nothing changed"),
            G("controls.reserved", "{0} cannot be used for {1}.", "Cha ghabh {0} a chleachdadh airson {1}.", ReviewStatus.Likely, "WK", "cha ghabh … a chleachdadh, cannot be used; airson, for; composed"),
            G("controls.wrongKind", "{0} cannot use {1}.", "Cha ghabh {0} {1} a chleachdadh.", ReviewStatus.Likely, "WK", "as above, with the binding as the object; composed"),
            G("controls.same", "That binding is already set.", "Tha an ceangal seo ann mu thràth.", ReviewStatus.Likely, "WK", "mu thràth, already; composed"),
            G("controls.resetDone", "Bindings reset to defaults.", "Chaidh na ceanglaichean air ais dhan bun-roghainnean", ReviewStatus.NeedsReview, "WK", "chaidh … air ais, was put back; bun-roghainnean as controls.resetAll; composed, a speaker should check it"),
            G(GroupKey(ActionGroup.Movement), "MOVEMENT", "GLUASAD", ReviewStatus.Likely, "WK", "gluasad is the verbal noun of gluais, move"),
            G(GroupKey(ActionGroup.Look), "LOOK", "SEALLADH", ReviewStatus.Verified, "WK", "sealladh, a look or glance (noun)"),
            G(GroupKey(ActionGroup.Combat), "COMBAT", "COGADH", ReviewStatus.Likely, "WK", "cogadh, war or battle; the heading sense needs a speaker"),
            G(GroupKey(ActionGroup.Portals), "PORTALS", "PORTAILEAN", ReviewStatus.NeedsReview, "WK", "portail is the naturalised borrowed word for portal (portailean, plural); the loan needs a speaker's blessing"),
            G(GroupKey(ActionGroup.System), "SYSTEM", "SIOSTAM", ReviewStatus.Verified, "WK", "siostam, system"),

            // ---- action labels ----
            G(ActionKey(GameAction.MoveForward), "Move forward", "Gluais air adhart", ReviewStatus.Likely, "WK", "gluais (move) + air adhart (forward)"),
            G(ActionKey(GameAction.MoveBack), "Move back", "Gluais air ais", ReviewStatus.Likely, "WK", "gluais (move) + air ais (back)"),
            G(ActionKey(GameAction.MoveLeft), "Move left", "Gluais air chlì", ReviewStatus.Likely, "WK", "gluais (move) + air chlì (on the left); clì is left. The phrase is composed, so a speaker should check it"),
            G(ActionKey(GameAction.MoveRight), "Move right", "Gluais air dheis", ReviewStatus.Likely, "WK", "gluais (move) + air dheis (on the right); deas is right, lenited dheis. The phrase is composed"),
            G(ActionKey(GameAction.LookUp), "Look up", "Seall suas", ReviewStatus.Likely, "WK", "seall (look, the imperative) + suas (up)"),
            G(ActionKey(GameAction.LookDown), "Look down", "Seall sìos", ReviewStatus.Likely, "WK", "seall (look, the imperative) + sìos (down)"),
            G(ActionKey(GameAction.LookLeft), "Look left", "Seall air chlì", ReviewStatus.Likely, "WK", "seall (look) + air chlì (on the left); composed, so a speaker should check it"),
            G(ActionKey(GameAction.LookRight), "Look right", "Seall air dheis", ReviewStatus.Likely, "WK", "seall (look) + air dheis (on the right); composed, so a speaker should check it"),
            G(ActionKey(GameAction.Jump), "Jump", "Leum", ReviewStatus.Likely, "WK", "leum, to jump; the bare verb is the imperative"),
            G(ActionKey(GameAction.FirePrimary), "Fire revolver", "Losg a' ghunna-thionndaidh", ReviewStatus.NeedsReview, "WK", "losg, fire or shoot; gunna-thionndaidh is a coined compound, gun plus tionndaidh, turning, for the revolver; needs a speaker"),
            G(ActionKey(GameAction.FireSecondary), "Fire shotgun", "Losg a' ghunna-sgaoilidh", ReviewStatus.NeedsReview, "WK", "losg, fire or shoot; the coined gunna-sgaoilidh as hint.shotgun; composed"),
            G(ActionKey(GameAction.Parry), "Parry", "Dìon", ReviewStatus.Likely, "WK", "dìon as hint.parry; the bare noun is the imperative"),
            G(ActionKey(GameAction.PortalTeal), "Teal portal", "Portail uaine-gorm", ReviewStatus.NeedsReview, "WK", "portail as group.portals; uaine-gorm, turquoise; composed"),
            G(ActionKey(GameAction.PortalViolet), "Violet portal", "Portail purpaidh", ReviewStatus.Likely, "WK", "as hint.violetPortal; composed"),
            G(ActionKey(GameAction.Respawn), "Respawn", "Tilleadh", ReviewStatus.Likely, "WK", "as pause.respawn; composed"),
            G(ActionKey(GameAction.FlyToggle), "Free-fly", "Eitealachd shaor", ReviewStatus.NeedsReview, "WK", "itealachd, flying (itealaich, to fly); saor, free; a coined compound for the debug free-fly; needs a speaker"),
            G(ActionKey(GameAction.FlyDown), "Fly down", "Itealaich sìos", ReviewStatus.Likely, "WK", "itealaich, fly (the imperative); sìos, down (as Seall sìos); composed"),
            G(ActionKey(GameAction.DebugInfo), "Debug overlay", "Dì-bhugachadh", ReviewStatus.NeedsReview, "WK", "a borrowed-word coinage modelled on English debugging; whether gd software uses it needs checking"),
            G(ActionKey(GameAction.Interact), "Open and use", "Fosgail agus cleachd", ReviewStatus.Likely, "WK", "fosgail (open) + agus (and) + cleachd (use), both bare verbs used as imperatives"),
            G(ActionKey(GameAction.ClosePortals), "Close portals (sever)", "Dùin na portailichean (gèarr)", ReviewStatus.NeedsReview, "WK", "dùin, close; portailichean, the loan's plural; gèarr as hint.sever; composed"),
            G(ActionKey(GameAction.Pause), "Pause menu", "Clàr-taice stad", ReviewStatus.Likely, "WK", "clàr-taice as hint.menu; stad as pause.title; composed"),
            G(ActionKey(GameAction.Slam), "Slam", "Bualadh", ReviewStatus.Likely, "WK", "bualadh, a striking (buail, to strike); the slamming sense needs a speaker"),
            G(ActionKey(GameAction.FireMortar), "Fire block mortar", "Losg am moirtiar-bloca", ReviewStatus.NeedsReview, "WK", "moirtiar is the borrowed word for a mortar (the weapon is not in the dictionaries); bloca, block; coined; needs a speaker"),
            G(ActionKey(GameAction.UseItem), "Use quick item", "Cleachd an nì grad", ReviewStatus.Likely, "WK", "cleachd, use (as Interact); nì, item or thing; grad, quick; composed"),

            // ---- settings ----
            G("settings.title", "SETTINGS", "ROGHAINNEAN", ReviewStatus.Verified, "FF", "capitals of Firefox gd's 'Roghainnean'"),
            G("settings.language", "Language", "Cànan", ReviewStatus.Verified, "FF WK", "Firefox gd's 'Language' header is Cànan; cànan, a language"),
            G("settings.mouseSensitivity", "Mouse sensitivity", "Mothachalachd na luch", ReviewStatus.Likely, "WK", "mothachalachd, sensitivity (technical gd); luch, mouse as controls.mouse; composed"),
            G("settings.padLookSpeed", "Gamepad look speed", "Luaths seallaidh a' gheama-phàid", ReviewStatus.NeedsReview, "WK", "luaths, speed; sealladh, look (as the look actions); the coined gèama-phàd; composed"),
            G("settings.deadzone", "Stick dead zone", "Raon marbh a' bhata", ReviewStatus.NeedsReview, "WK", "raon, zone or field; marbh, dead; bata, stick; a word-for-word coinage"),
            G("settings.fov", "Field of view", "Raon an t-seallaidh", ReviewStatus.Likely, "WK", "raon, field; sealladh, view; composed"),
            G("settings.uiSize", "Interface size", "Meud an eadar-aghaidh", ReviewStatus.Likely, "WK", "meud, size; eadar-aghaidh, interface (the usual gd software word); composed"),
            G("settings.autoScale", "Scale the interface with the window", "Sgèil an eadar-aghaidh leis an uinneag", ReviewStatus.NeedsReview, "WK", "sgèil, scale; uinneag, window; the sentence is composed"),
            G("settings.master", "Master volume", "Prìomh-fhuaim", ReviewStatus.Likely, "WK", "prìomh, main or master (as the main menu); fuaim, sound; composed"),
            G("settings.effects", "Effects volume", "Fuaim nan èifeachd", ReviewStatus.Likely, "WK", "èifeachd, effect (èifeachd, plural); composed"),
            G("settings.ambience", "Ambience volume", "Fuaim na h-àrainne", ReviewStatus.Likely, "WK", "àrainn, surroundings or environment (àrainne in the genitive); composed"),
            G("settings.music", "Music volume", "Fuaim na ciùil", ReviewStatus.Likely, "WK", "ceòl, music (ciùil in the genitive); composed"),
            G("settings.viewDistance", "Render distance", "Astar-taisbeanaidh", ReviewStatus.Likely, "WK", "astar, distance; taisbean, show or display; a compound for render distance"),
            G("settings.chunks", "{0} chunks", "{0} blocaichean", ReviewStatus.Likely, "WK", "bloca, block (blocaichean, plural) standing in for chunk, for which gd has no word"),
            G("settings.ghost", "Show the ghost of your best run", "Seall taibhse an ruith as fheàrr agad", ReviewStatus.Likely, "WK", "taibhse, ghost or spectre; ruith, run; as fheàrr, best; composed"),
            G("settings.prompts", "Controller prompts", "Comharran an t-smachdair", ReviewStatus.NeedsReview, "WK", "comharra, sign or mark (the button labels); smachdar, a coined controller after smachd; needs a speaker"),
            G("settings.promptsAuto", "Auto (follow controller)", "Fèin (a-rèir an t-smachdair)", ReviewStatus.Likely, "WK", "fèin, self, the usual gd prefix for automatic; a-rèir, according to; needs a speaker"),
            G("settings.invertMouse", "Invert mouse Y", "Tionndaidh Y na luch", ReviewStatus.Likely, "WK", "tionndaidh, turn about (imperative); the technical gd for Invert; composed"),
            G("settings.invertPad", "Invert gamepad Y", "Tionndaidh Y a' gheama-phàid", ReviewStatus.NeedsReview, "WK", "as above with the coined gèama-phàd"),

            // ---- HUD ----
            new("hud.floor", "Floor {0} / {1}", "Làr {0} / {1}", ReviewStatus.NeedsReview, "WK", "làr is the floor of a room or the ground; whether it fits a level of a dungeon (or lobhta, a storey) needs a speaker, so it is held back"),
            G("hud.keys", "Keys {0}", "Iuchraichean {0}", ReviewStatus.Likely, "WK", "iuchraichean is the plural of iuchair, key; the number sits beside it, so no count agreement is needed"),
            G("hud.result.title", "FLOOR {0} COMPLETE", "LÀR {0} DEISEIL", ReviewStatus.Likely, "WK", "làr as held back for hud.floor; deiseil as quest.done, finished"),
            G("hud.result.rank", "RANK  {0}", "INBHE  {0}", ReviewStatus.Verified, "WK", "inbhe, rank or status; capitals for the banner and two spaces kept before the number"),
            G("hud.result.stats", "Time {0}    Kills {1}    Style {2}", "Ùine {0}  Marbhadh {1}  Stoidhle {2}", ReviewStatus.Likely, "WK", "ùine, time; marbhadh, killing; stoidhle, style; the spacing is kept"),
            G("hud.result.restarts", "Restarts {0}  (−{0} points)", "Ath-thòiseachaidh {0}  (-{0} puing)", ReviewStatus.Likely, "WK", "ath-thòiseachadh, starting again; puing, point"),
            G("toast.omen", "A washerwoman wrings out her cloth nearby. Something is coming.", "Tha bean-nighe faisg air làimh. Tha rudeigin a' tighinn", ReviewStatus.NeedsReview, "WK", "bean-nighe, the washerwoman of folklore; faisg air làimh, nearby; Tha rudeigin a' tighinn, something is coming; the wringing is dropped, a speaker should check the line"),
            G("toast.boon", "The brownie warms the hearth for you (+{0} health)", "Tha am brùnaidh a' blàthachadh an làr-teine dhut (+{0} slàinte)", ReviewStatus.NeedsReview, "WK", "brùnaidh, brownie (as docs/mythology.md); blàth, warm (a' blàthachadh, warming); làr-teine, hearth; composed, a speaker should check the idiom"),
            G("standing.better", "{0} think better of you: {1}", "Tha {0} measail ort tuilleadh: {1}", ReviewStatus.Likely, "WK", "measail ort, fond of you; tuilleadh, more; composed"),
            G("standing.worse", "{0} now regard you as {1}", "Tha {0} a-nis 'gad mheas mar {1}", ReviewStatus.Likely, "WK", "meas, regard (agad mheas, regarding you); mar, as; composed"),
            G("standing.title", "STANDING", "SEASAMH", ReviewStatus.Likely, "WK", "capitals of seasamh, position or standing; the screen-name sense needs a speaker"),
            G("standing.subtitle", "How the dungeon's peoples regard you. Hurt a creature in front of its kin and they will remember.", "Mar a tha muinntir an dùin 'gad fhaicinn. Ma leòn thu creutair air beulaibh a chinneadh, chuimhnich iad", ReviewStatus.NeedsReview, "WK", "muinntir an dùin, the fortress's people; 'gad fhaicinn, seeing you; leòn, wound or hurt; creutair, creature; beulaibh, in front of; cinneadh, kin; chuimhnich, will remember; composed"),
            G("standing.nobody", "Nobody here has an opinion of you yet.", "Chan eil beachd aig neach sam bith ort fhathast.", ReviewStatus.Likely, "WK", "beachd, opinion; neach sam bith, anyone; composed"),
            G("standing.recent", "RECENT", "ÙR", ReviewStatus.NeedsReview, "WK", "ùr, new; a short heading for the recent changes, and whether a speaker would use it here is untested"),
            G("standing.nothingYet", "Nothing yet.", "Chan eil dad.", ReviewStatus.Likely, "WK", "chan eil dad, there is nothing"),

            // ---- gaps and falls ----
            G("gap.edge", "A gap. Fire a portal across it ({0}), then another beside you ({1}), and step through.", "Beàrn. Losg portail tarsainn air ({0}), agus fear eile ri do thaobh ({1}), agus coisich troimhe.", ReviewStatus.NeedsReview, "WK", "beàrn, gap; losg, fire or shoot (imperative); tarsainn air, across; fear eile, another one; ri do thaobh, beside you; coisich, walk; composed, a speaker should check it"),
            G("gap.alcove", "A high alcove. Fire a portal up into it ({0}), then another beside you ({1}), and step through.", "Àite àrd. Losg portail suas ann ({0}), agus fear eile ri do thaobh ({1}), agus coisich troimhe.", ReviewStatus.NeedsReview, "WK", "suas ann, up into it; the alcove is described as a high place, not named; composed"),
            G("gap.afterFall", "You are set back at the edge. Fire a portal across the gap instead ({0}, then {1}).", "Chaidh do chur air ais dhan oir. Losg portail tarsainn air a' bheàrn ({0}, an uairsin {1}).", ReviewStatus.NeedsReview, "WK", "chaidh do chur air ais, you were put back; oir, edge; an uairsin, then; composed"),
            G("gap.fellBare", "FELL  -{0}", "THUIT  -{0}", ReviewStatus.Verified, "WK", "thuit is the past tense of tuit, to fall; two spaces kept before the number"),
            G("gap.spikes", "SPIKES  -{0}", "BIORAN  -{0}", ReviewStatus.Likely, "WK", "bioran is the plural of bior, spike or prong"),
            G("gap.splash", "SPLASH  climbing out", "PLESG  a' dìreadh a-mach", ReviewStatus.NeedsReview, "WK", "plesg is coined for splash (nothing in the dictionaries); a' dìreadh a-mach, climbing out"),

            // ---- hints under the HUD ----
            G("hint.revolver", "revolver", "gunna-chuartach", ReviewStatus.NeedsReview, "WK", "a coined compound, gunna, gun, plus cuairt, a turn, for the revolving gun; needs a speaker"),
            G("hint.shotgun", "shotgun", "gunna-sgaoilidh", ReviewStatus.NeedsReview, "WK", "a coined compound, gunna plus sgaoil, scatter, for the shotgun; needs a speaker"),
            G("hint.parry", "parry", "dìon", ReviewStatus.Likely, "WK", "dìon, defence; the fencing sense of parry needs a speaker"),
            G("hint.tealPortal", "teal portal", "portail uaine-gorm", ReviewStatus.NeedsReview, "WK", "portail as group.portals; uaine-gorm, turquoise (uaine, green, + gorm, blue); composed"),
            G("hint.violetPortal", "violet portal", "portail purpaidh", ReviewStatus.Likely, "WK", "purpaidh, violet or purple; composed with portail"),

            // The portal pair's non-colour marks (docs/features.md 2.6): teal frames carry chevrons, violet frames diamonds,
            // and the HUD shows the words so the pair never depends on telling the two colours apart.
            T("portal.mark.chevron", "chevron"),
            T("portal.mark.diamond", "diamond"),
            G("hint.sever", "sever", "gèarr", ReviewStatus.Likely, "WK", "gèarr, cut (imperative); sever is read as cutting the link"),
            G("hint.open", "open", "fosgail", ReviewStatus.Likely, "WK", "fosgail, open; the bare verb is the imperative"),
            G("hint.jump", "jump", "leum", ReviewStatus.Likely, "WK", "leum, to jump"),
            G("hint.menu", "menu", "clàr-taice", ReviewStatus.Likely, "DRU", "a software translation of 'Menu'; not in the dictionaries checked"),

            // ---- prompts and panels ----
            G("prompt.read", "{0}  read: {1}", "{0}  leugh: {1}", ReviewStatus.Likely, "WK", "leugh, read (the imperative; Wiktionary quotes 'Leugh seo', Read this)"),
            G("prompt.speak", "{0}  speak", "{0}  bruidhinn", ReviewStatus.Likely, "WK", "bruidhinn, to speak or talk; the bare verb is the imperative"),
            G("cat.warmth", "The fire is warm, and you feel a little better.", "Tha an teine blàth, agus tha thu nas fheàrr beagan", ReviewStatus.NeedsReview, "WK", "blàth, warm; nas fheàrr beagan, a little better; composed"),
            G("prompt.closeHint", "{0} or {1} to close", "{0} no {1} airson dùineadh", ReviewStatus.Likely, "WK", "airson, for the purpose of; dùineadh, closing (dùin, close); composed"),
            G("prompt.leaveHint", "{0} or {1} to leave", "{0} no {1} airson fàgail", ReviewStatus.Likely, "WK", "fàgail, leaving (fàg as Quit); composed"),

            // ---- banners ----
            G("banner.died", "YOU DIED", "THA THU MARBH", ReviewStatus.Likely, "WK", "Tha thu (you are) + marbh, dead"),
            G("banner.respawn", "{0} to respawn", "{0} airson tilleadh", ReviewStatus.Likely, "WK", "tilleadh as pause.respawn; airson, for"),
            G("banner.enemies", "Enemies {0}: kill them to open the doors", "Nàimhdean {0}: marbh iad gus na dorsan fhosgladh", ReviewStatus.Likely, "WK", "nàimhdean, enemies; marbh, kill (imperative); gus … fhosgladh, to open; dorsan, doors (as An Doras Deireannach); composed"),
            G("banner.clear", "CLEAR", "FALAMH", ReviewStatus.Likely, "WK", "falamh, empty; capitals for the banner, the cleared sense needs a speaker"),
            G("banner.floorDone", "FLOOR {0} COMPLETE  {1}  {2}", "LÀR {0} DEISEIL  {1}  {2}", ReviewStatus.Likely, "WK", "as hud.result.title; the spacing is kept"),
            G("banner.floorStats", "time {0}  kills {1}  style {2}", "ùine {0}  marbhadh {1}  stoidhle {2}", ReviewStatus.Likely, "WK", "as hud.result.stats, lower case"),
            G("banner.firstClear", "first clear", "a' chiad fhalamhadh", ReviewStatus.NeedsReview, "WK", "a' chiad, the first; fhalamhadh, the clearing of (from falamh); a coinage for the banner"),
            G("banner.newBest", "NEW BEST  (was {0})", "AS FHEÀRR ÙR  (bha {0})", ReviewStatus.Likely, "WK", "as fheàrr, best; ùr, new; bha, it was; composed"),
            G("banner.best", "best {0}", "as fheàrr {0}", ReviewStatus.Likely, "WK", "as fheàrr, best; the number sits beside it"),
            G("banner.key", "KEY", "IUCHAIR", ReviewStatus.Likely, "WK", "iuchair, key"),
            G("banner.chest", "CHEST", "CISTE", ReviewStatus.Verified, "WK", "ciste, chest or box; capitals for the banner"),
            G("banner.lockedChest", "LOCKED CHEST", "CISTE GHLAISTE", ReviewStatus.Likely, "WK GW", "ciste (feminine) + glaiste, locked; the adjective is lenited after a feminine noun"),
            G("banner.chestHealed", "CHEST  +{0} health", "CISTE  +{0} slàinte", ReviewStatus.Likely, "WK", "ciste, chest, and slàinte, health; composed for the banner"),
            G("banner.lockedChestHealed", "LOCKED CHEST OPENED  +{0} health", "CISTE GHLAISTE AIR FHOSGLADH  +{0} slàinte", ReviewStatus.Likely, "WK", "as banner.lockedChest plus air fhosgladh, opened; slàinte as banner.chestHealed"),
            G("banner.item", "{0}  +{1} to your bag", "{0}  +{1} dhan phoca agad", ReviewStatus.Likely, "WK", "dhan phoca agad, into your bag (poca as pause.bag); composed"),
            G("banner.bagFull", "{0}  your bag is full", "{0}  tha do phoca làn", ReviewStatus.Likely, "WK", "tha … làn, is full; composed"),
            G("banner.locked", "locked: find the key on this floor", "glaiste: faigh an iuchair air an làr seo", ReviewStatus.Likely, "WK", "glaiste as banner.lockedChest; faigh, get or find (imperative); air an làr seo, on this floor; composed"),
            G("banner.spikes", "SPIKES  -{0}", "BIORAN  -{0}", ReviewStatus.Likely, "WK", "bioran is the plural of bior, spike or prong"),
            G("banner.noPortal", "can't place a portal there ({0})", "chan urrainn dhut portail a chur an sin ({0})", ReviewStatus.Likely, "WK", "cha(n) urrainn dhut, you cannot; a chur an sin, to put it there; composed"),
            G("banner.meteor", "METEOR", "MEATAN", ReviewStatus.Likely, "WK", "meatan, meteor; capitals for the banner"),
            G("banner.meteor2", "METEOR x2", "MEATAN x2", ReviewStatus.Likely, "WK", "as banner.meteor; the x2 is kept"),
            // ---- pacts (offered between floors; English only until a speaker checks the lines) ----
            T("pact.title", "THE DUNGEON OFFERS A PACT"),
            T("pact.strength", "its strength follows your style rank"),
            T("pact.boon", "Boon"),
            T("pact.burden", "Burden"),
            T("pact.decline", "Take none"),
            T("pact.hint", "{0} to take one, {1} to leave them"),
            T("pact.bloodsworn.name", "Bloodsworn"),
            T("pact.bloodsworn.boon", "Parries heal 20 more and the spent parry window's cooldown is refunded"),
            T("pact.bloodsworn.burden", "Hard damage takes half of damage taken, up from 35%"),
            T("pact.hollowHeart.name", "Hollow Heart"),
            T("pact.hollowHeart.boon", "Blood heals 1.5x"),
            T("pact.hollowHeart.burden", "Maximum health 30 lower"),
            T("pact.gravedigger.name", "Gravedigger"),
            T("pact.gravedigger.boon", "Slam shockwaves reach 1.5 blocks further and break floors in one hit"),
            T("pact.gravedigger.burden", "Style decays 20% faster"),
            T("pact.showman.name", "Showman"),
            T("pact.showman.boon", "All style points 25% higher"),
            T("pact.showman.burden", "Weapons go stale 40% faster"),
            T("pact.twinGate.name", "Twin Gate"),
            T("pact.twinGate.boon", "A third portal slot: the newest rotates in, the oldest rotates out"),
            T("pact.twinGate.burden", "Placing a portal costs 5 health while below Brutal"),
        };

        var rankGaelic = new Dictionary<StyleRank, (string Gaelic, ReviewStatus Status, string Note)>
        {
            [StyleRank.Fierce] = ("FIADHAICH", ReviewStatus.Verified, "fiadhaich, fierce or wild"),
            [StyleRank.Brutal] = ("BRÙIDEIL", ReviewStatus.Verified, "brùideil, brutal or cruel"),
            [StyleRank.Savage] = ("FIADHACH", ReviewStatus.Likely, "fiadhach, savage; kept apart from FIADHAICH above, which a speaker should confirm"),
            [StyleRank.Rowdy] = ("RUGDA", ReviewStatus.Likely, "rugda, rough or rude; the rowdy sense needs a speaker"),
            [StyleRank.Merciless] = ("MI-THROCAIREACH", ReviewStatus.Likely, "mi-throcaireach, merciless (trocair, mercy)"),
            [StyleRank.Relentless] = ("DIAN", ReviewStatus.Likely, "dian, intense, firm or relentless"),
            [StyleRank.Apocalyptic] = ("APACAILIPT", ReviewStatus.NeedsReview, "coined from apacailiops, an apocalypse; the form and the adjective's earlier h need a speaker"),
            [StyleRank.Legendary] = ("SGEULACH", ReviewStatus.NeedsReview, "sgeulach, of the tales (sgeul, a tale); whether it carries the English sense needs a speaker"),
        };
        foreach (var rank in Enum.GetValues<StyleRank>().Where(r => r != StyleRank.None))
            list.Add(rankGaelic.TryGetValue(rank, out var g)
                ? G(RankKey(rank), rank.ToString().ToUpperInvariant(), g.Gaelic, g.Status, "WK", g.Note)
                : T(RankKey(rank), rank.ToString().ToUpperInvariant()));
        return list;
    }
}
