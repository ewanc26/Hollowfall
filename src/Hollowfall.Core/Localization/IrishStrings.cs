using Hollowfall.Core.Combat;
using Hollowfall.Core.Input;

namespace Hollowfall.Core.Localization;

/// <summary>
/// Gaeilge for the interface, kept apart from the Gàidhlig in <see cref="InterfaceStrings"/> and attached to the same keys here:
/// the interface is the only area translated into Irish (the story prose and the cat's lines are covered by docs/gaeilge.md and the
/// string tables they describe). The same honesty rules apply: every entry carries a status and a cited source, a Likely entry is
/// put together from words a dictionary gives, and a NeedsReview entry (a coined technical term or a composed sentence) is held
/// back behind its English until a speaker checks it — docs/gaeilge.md lists the whole table.
/// </summary>
public static class IrishStrings
{
    /// <summary>Attaches the Gaeilge to the entries that carry the key it belongs to. A key with no Gaeilge keeps its null, so the English shows.</summary>
    public static IReadOnlyList<LocEntry> Merge(IReadOnlyList<LocEntry> entries) =>
        [.. entries.Select(e => Map.TryGetValue(e.Key, out var t)
            ? e with { Irish = t.Text, IrishStatus = t.Status, IrishSource = t.Source, IrishNote = t.Note }
            : e)];

    private static string Action(GameAction a) => "action." + char.ToLowerInvariant(a.ToString()[0]) + a.ToString()[1..];
    private static string Group(ActionGroup g) => "group." + char.ToLowerInvariant(g.ToString()[0]) + g.ToString()[1..];
    private static string Rank(StyleRank r) => "rank." + char.ToLowerInvariant(r.ToString()[0]) + r.ToString()[1..];

    private sealed record E(string Text, ReviewStatus Status = ReviewStatus.Likely, string Source = "WKI FGB", string Note = "");

    private static readonly IReadOnlyDictionary<string, E> Map = Build();

    private static Dictionary<string, E> Build()
    {
        var m = new Dictionary<string, E>
        {
            // ---- shared menu words ----
            ["menu.play"] = new("Imir", ReviewStatus.Verified, "FGB", "FGB imir: imir cluiche, to play a game. The imperative used as the button word"),
            ["menu.settings"] = new("Socruithe", ReviewStatus.Verified, "FFI FGB", "Firefox as Gaeilge's word for Settings; FGB socruithe, arrangements or settings"),
            ["menu.controls"] = new("Rialú", ReviewStatus.Likely, "FGB", "FGB rialú, the act of controlling; whether the plural rialtáin suits a heading needs a speaker"),
            ["menu.quit"] = new("Scoir", ReviewStatus.Likely, "FGB", "FGB scoir, to quit or leave; the imperative on a button is not confirmed"),
            ["menu.back"] = new("Ar ais", ReviewStatus.Likely, "FGB", "FGB ar ais, back or again; the use on a Back button is composition"),
            ["menu.reset"] = new("Athshocraigh", ReviewStatus.NeedsReview, "FGB", "coined: ath- plus socraigh (FGB socruithe), re-set; a speaker should confirm the term"),

            // ---- title ----
            ["title.tagline"] = new("Fáil ar a dhún a ghintear le huimhir.\nBí gasta. Faigh sláinte trí ghortú.", ReviewStatus.NeedsReview, "FGB WKI", "composed from sourced words (dún, fortress; gin, generate; uimhir, number; gasta, quick; sláinte, health; gortú, hurting); a speaker should rewrite the line"),
            ["title.seed"] = new("Síol", ReviewStatus.Verified, "WKI", "síol, seed (first sense)"),
            ["title.seedHint"] = new("aon téacs nó uimhir", ReviewStatus.Likely, "FGB WKI", "aon, any; téacs, text; uimhir, number; composed"),
            ["title.random"] = new("Fánach", ReviewStatus.NeedsReview, "FGB", "FGB fánach, wild or random; go fánach is 'at random'; whether the adjective alone suits a button needs a speaker"),
            ["title.daily"] = new("Laethúil", ReviewStatus.Likely, "FGB", "FGB laethúil, daily; whether the bare adjective suits a button needs a speaker"),
            ["title.board"] = new("Deimhnithe inniu", ReviewStatus.Likely, "FGB", "deimhnithe, verified or confirmed; inniu, today; composed"),
            ["title.boardNone"] = new("Níl aon rith deimhnithe fós inniu.", ReviewStatus.Likely, "FGB", "rith, a run; fós, yet; composed"),
            ["title.boardRow"] = new("Urlár {0}: grád {1}, {2} pointí, {3} s", ReviewStatus.Likely, "FGB WKI", "urlár, floor or storey — the word FGB gives for a floor of a building; grád, grade; pointí, points"),

            // ---- pause ----
            ["pause.title"] = new("AR SOS", ReviewStatus.Likely, "FGB", "FGB sos, a pause; ar sos, on pause, from learner usage rather than a quoted heading — a speaker should check ar shos"),
            ["pause.resume"] = new("Lean ar aghaidh", ReviewStatus.Likely, "FGB", "FGB lean ar aghaidh, to continue; composed as a button word"),
            ["pause.respawn"] = new("Athbheochan", ReviewStatus.NeedsReview, "FGB", "FGB athbheochan, a reviving or resurrection; the gaming respawn sense is coined and needs a speaker"),
            ["pause.standing"] = new("Stádas", ReviewStatus.Likely, "FGB", "FGB stádas, status or standing; the screen-name use is composition"),
            ["pause.character"] = new("Carachtar", ReviewStatus.Verified, "FGB", "FGB carachtar, character (a person in a book or play)"),
            ["pause.bag"] = new("Mála", ReviewStatus.Verified, "FGB", "FGB mála, a bag or sack"),
            ["bag.title"] = new("MÁLA", ReviewStatus.Verified, "FGB", "capitals of mála above"),
            ["bag.slots"] = new("Áiteanna in úsáid: {0} as {1}", ReviewStatus.NeedsReview, "FGB", "áit, a place, standing in for slot, for which Irish games have no settled word; in úsáid, in use; as, out of"),
            ["bag.coin"] = new("Bonn {0}", ReviewStatus.Verified, "FGB", "FGB bonn, a coin (one of its senses); the number sits beside it"),
            ["bag.none"] = new("Níl aon rud á iompar fós.", ReviewStatus.Likely, "FGB", "iompar, carrying; fós, yet; composed"),
            ["bag.wear"] = new("Caith", ReviewStatus.Likely, "FGB", "FGB caith, to wear (clothes); the bare verb used as the imperative"),
            ["bag.takeOff"] = new("Bain", ReviewStatus.Likely, "FGB", "FGB bain de, to take off (clothes); the bare bain as a button is composition"),
            ["bag.worn"] = new("caite", ReviewStatus.Likely, "FGB", "FGB caite, worn; the past participle of caith above"),
            ["bag.ready"] = new("Réidh", ReviewStatus.Verified, "FGB", "FGB réidh, ready or prepared"),
            ["bag.readyMark"] = new("réidh", ReviewStatus.Verified, "FGB", "same word as bag.ready, lower case for the mark beside an item"),
            ["bag.readyHint"] = new("Is é an mhír réidh an ceann a chaitheann an cnaipe úsáide tapaidh le linn rith; ní thagann ach ceann amháin leat.", ReviewStatus.NeedsReview, "FGB", "mír, an item; cnaipe, a button; tapa, quick; composed, a speaker should check it"),
            ["bag.wearHint"] = new("Tiocfaidh éifeacht na n-arraí chaite i bhfeidhm ó do rith eile.", ReviewStatus.NeedsReview, "FGB", "éifeacht, an effect; arraí, gear; caite as bag.worn; composed, a speaker should check it"),

            // ---- the character (progression) ----
            ["character.title"] = new("CARACHTAR", ReviewStatus.Verified, "FGB", "capitals of carachtar above"),
            ["character.none"] = new("Níl carachtar ann fós. Cliste rud éigin le tosú.", ReviewStatus.Likely, "FGB", "FGB clise, to defeat; composed sentence, a speaker should check it"),
            ["character.level"] = new("Leibhéal {0}", ReviewStatus.Verified, "FGB", "FGB leibhéal, a level or degree; the number sits beside it"),
            ["character.nextLevel"] = new("{0} as {1} taithí go dtí an chéad leibhéal eile", ReviewStatus.Likely, "FGB", "FGB taithí, experience; go dtí an chéad leibhéal eile, to the next level; composed"),
            ["character.maxLevel"] = new("An leibhéal is airde a shroich tú", ReviewStatus.Likely, "FGB", "FGB sroich, to reach; is airde, highest; composed"),
            ["character.points"] = new("Pointí le caitheamh: {0}", ReviewStatus.Likely, "FGB", "pointí, points; caitheamh, spending (from caith as bag.wear); composed"),
            ["character.vigour"] = new("Brí {0}", ReviewStatus.Verified, "FGB", "FGB brí, strength or vigour; the number sits beside it"),
            ["character.vigourHint"] = new("+10 sláinte uasta do gach pointe", ReviewStatus.Likely, "FGB", "sláinte, health; uasta, at most; gach, each; composed"),
            ["character.might"] = new("Neart {0}", ReviewStatus.Verified, "FGB", "FGB neart, might or strength; the number sits beside it"),
            ["character.mightHint"] = new("+5 faoin gcéad ó ghunnaí agus cosaint do gach pointe", ReviewStatus.NeedsReview, "FGB", "faoin gcéad, per cent; cosaint, defence, standing in for parry; composed, a speaker should check it"),
            ["character.nextRun"] = new("Tiocfaidh pointí i bhfeidhm ó do rith eile. Coinníonn rith na staitisticí a raibh aige nuair a thosaigh sé.", ReviewStatus.NeedsReview, "FGB", "tig i bhfeidhm, takes effect; coinnigh, keeps; composed, a speaker should check it"),

            // ---- quests (progression) ----
            ["quest.title"] = new("TASCANNA", ReviewStatus.Likely, "FGB", "FGB tasc, a task (tascanna, plural); the quest sense is from games and needs a speaker"),
            ["quest.none"] = new("Níl tasc ar bith fós. Iarr ar dhaoine an dúin.", ReviewStatus.NeedsReview, "FGB", "tasc as quest.title; dún, fortress; composed, a speaker should check it"),
            ["quest.husks"] = new("Glan na Sligíní", ReviewStatus.NeedsReview, "FGB", "glan, clean or clear out (the imperative); Sligín coined from FGB slige, a shell or husk, for the Husks; needs a speaker"),
            ["quest.spitters"] = new("Glan na n-Plathadóirí", ReviewStatus.NeedsReview, "FGB", "Plathadóir coined from FGB plath, to spurt or spit on, for the Spitters; needs a speaker"),
            ["quest.giver"] = new("Ó {0}", ReviewStatus.Likely, "FGB", "ó, from; 'given by' reads as 'from' in Gaeilge"),
            ["quest.progress"] = new("{0} as {1} cloíte", ReviewStatus.Likely, "FGB", "FGB buaigh: cloíte, defeated; FGB out of a number; composed"),
            ["quest.reward"] = new("Duais: {0} taithí, {1} bonn agus {2}", ReviewStatus.Likely, "FGB", "FGB duais, a reward or prize; taithí as character.nextLevel; bonn as bag.coin; composed"),
            ["quest.take"] = new("Tóg", ReviewStatus.Likely, "FGB", "FGB tóg, to take; the bare imperative on a button is composition"),
            ["quest.taken"] = new("Glactha", ReviewStatus.Likely, "FGB", "FGB glac, to take; glactha, taken"),
            ["quest.done"] = new("Críochnaithe", ReviewStatus.Verified, "FGB", "FGB críochnaithe, finished or completed"),
            ["quest.refused"] = new("Diúltaithe", ReviewStatus.Likely, "FGB", "FGB diúltaigh, to refuse; diúltaithe, refused"),
            ["quest.nextRun"] = new("Áirítear na maruithe a dhéanann tú ó seo amach, i ngach rith.", ReviewStatus.NeedsReview, "FGB", "áirigh, to count; marú, killing; composed, a speaker should check it"),
            ["pause.quests"] = new("Tascanna", ReviewStatus.Likely, "FGB", "plural of tasc as quest.title"),
            ["pause.journal"] = new("Dialann", ReviewStatus.Verified, "FGB", "FGB dialann, a diary or journal"),
            ["pause.mainMenu"] = new("An príomh-roghchlár", ReviewStatus.Likely, "FFI FGB", "príomh, main; roghchlár as hint.menu; composed"),

            // ---- journal ----
            ["journal.title"] = new("DIALANN", ReviewStatus.Verified, "FGB", "capitals of dialann above"),
            ["journal.notesRead"] = new("{0}   -   Nótaí léite: {1}", ReviewStatus.Likely, "FGB", "nóta, a note; léite, read; the shape of the line is kept"),
            ["journal.empty"] = new("Níl aon rud léite fós. Tá nótaí in aghaidh na mballaí sa dún: siúil go dtí an scroll agus bain úsáid as.", ReviewStatus.NeedsReview, "FGB", "balla, a wall; siúil, walk; scroll, a scroll (the borrowed word); composed, a speaker should check it"),

            // ---- controls ----
            ["controls.title"] = new("RIALÚ", ReviewStatus.Likely, "FGB", "capitals of rialú as menu.controls; the heading use needs a speaker"),
            ["controls.help"] = new("Cliceáil ar cheangal, agus ansin brúigh an cnaipe, an eochair nó taobh an bhaia nua. Cuirfidh Esc ar ceal.", ReviewStatus.NeedsReview, "FGB", "cliceáil, click (the borrowed word); brúigh, press; composed sentence, a speaker should check it"),
            ["controls.colAction"] = new("Gníomh", ReviewStatus.Verified, "FGB", "FGB gníomh, an act or action"),
            ["controls.colKeys"] = new("Méarchlár / luch", ReviewStatus.Likely, "FFI FGB", "méarchlár, keyboard, and luch, mouse (Firefox ga's words); the slash is kept from the English"),
            ["controls.colPad"] = new("Rialtóir", ReviewStatus.NeedsReview, "FGB", "coined from FGB rialtaire, a controller or governor; gd has no settled word for a gamepad and neither has ga; needs a speaker"),
            ["controls.resetAll"] = new("Fill na réamhshocruithe", ReviewStatus.NeedsReview, "FFI FGB", "réamhshocruithe, pre-settings or defaults; fill, to restore or fill back; composed after Firefox ga's wording; needs a speaker"),
            ["controls.listenPad"] = new("brúigh cnaipe nó bog an bat…", ReviewStatus.Likely, "FGB", "bog, to move; bat is the borrowed word for a stick; composed"),
            ["controls.listenKey"] = new("brúigh eochair nó cliceáil…", ReviewStatus.Likely, "FGB", "brúigh, press; eochair, a key; composed"),
            ["controls.binding"] = new("Ceangal \"{0}\". Cuirfidh Esc ar ceal.", ReviewStatus.NeedsReview, "FGB", "ceangal, a tie or binding; cur ar ceal, to cancel; composed"),
            ["controls.mouse"] = new("luch", ReviewStatus.Verified, "FGB", "FGB luch, a mouse"),
            ["controls.now"] = new("'S é {1} atá ag {0} anois.", ReviewStatus.Likely, "FGB", "is é atá ag, is what belongs to; anois, now; composed"),
            ["controls.swapUnbound"] = new("Bhí {0} in úsáid ag {1}; tá {1} dícheangailte anois.", ReviewStatus.NeedsReview, "FGB", "dícheangail, unbound; composed"),
            ["controls.swapped"] = new("Bhí {0} in úsáid ag {1}; malartaíodh an bheirt.", ReviewStatus.NeedsReview, "FGB", "malartaigh, to exchange; composed"),
            ["controls.inUse"] = new("Tá {0} in úsáid ag {1}. Níor athraigh rud ar bith.", ReviewStatus.Likely, "FGB", "in úsáid, in use; athraigh, to change; composed"),
            ["controls.reserved"] = new("Ní féidir {0} a úsáid do {1}.", ReviewStatus.Likely, "FGB", "is féidir, can; úsáid, use; do, for; composed"),
            ["controls.wrongKind"] = new("Ní féidir le {0} {1} a úsáid.", ReviewStatus.Likely, "FGB", "as above with the binding as the object; composed"),
            ["controls.same"] = new("Tá an ceangal seo socraithe cheana.", ReviewStatus.Likely, "FGB", "socraithe, set (from socruithe); cheana, already; composed"),
            ["controls.resetDone"] = new("Cuireadh na ceangail ar ais chuig na réamhshocruithe.", ReviewStatus.NeedsReview, "FGB", "cuir ar ais, to put back; composed"),

            // ---- settings ----
            ["settings.title"] = new("SOCRUITHE", ReviewStatus.Verified, "FFI FGB", "capitals of Firefox as Gaeilge's Socruithe"),
            ["settings.language"] = new("Teanga", ReviewStatus.Verified, "FFI FGB", "Firefox ga's Language header is Teanga; FGB teanga, a language or tongue"),
            ["settings.mouseSensitivity"] = new("Íogaireacht na luch", ReviewStatus.Likely, "FGB", "FGB íogaireacht, sensitivity; luch as controls.mouse; composed"),
            ["settings.padLookSpeed"] = new("Luas amhairc an rialtóra", ReviewStatus.NeedsReview, "FGB", "luas, speed; amharc, a look; rialtóir as controls.colPad; composed"),
            ["settings.deadzone"] = new("Crios marbh an bhaia", ReviewStatus.NeedsReview, "FGB", "crios, a zone; marbh, dead; bat as controls.listenPad; a word-for-word coinage"),
            ["settings.fov"] = new("Réimse radhairc", ReviewStatus.Likely, "FGB", "réimse, a field; radharc, a view; composed, a speaker should check the phrase"),
            ["settings.uiSize"] = new("Méid an chomhéadain", ReviewStatus.Likely, "FGB", "méid, size; comhéadan, an interface (the usual ga software word); composed"),
            ["settings.autoScale"] = new("Scálaigh an comhéadan leis an bhfuinneog", ReviewStatus.NeedsReview, "FGB", "scálaigh, to scale (the borrowed word); fuinneog, a window; composed"),
            ["settings.master"] = new("Príomhfhuaim", ReviewStatus.Likely, "FGB", "príomh, main; fuaim, a sound; composed"),
            ["settings.effects"] = new("Fuaim na n-éifeachtaí", ReviewStatus.Likely, "FGB", "éifeacht, an effect (éifeachtaí, plural); composed"),
            ["settings.ambience"] = new("Fuaim an chúlra", ReviewStatus.Likely, "FGB", "cúlra, a background or backdrop; composed"),
            ["settings.music"] = new("Fuaim na ceoil", ReviewStatus.Likely, "FGB", "ceol, music (ceoil in the genitive); composed"),
            ["settings.viewDistance"] = new("Fad taispeána", ReviewStatus.Likely, "FGB", "fad, a distance; taispeáin, to show; a compound for render distance"),
            ["settings.chunks"] = new("{0} bloic", ReviewStatus.Likely, "FGB WKI", "bloc, a block (bloic, plural) standing in for chunk, for which ga has no word"),
            ["settings.ghost"] = new("Taispeáin taibhse do rith is fearr", ReviewStatus.Likely, "FGB", "taispeáin, show (the imperative); taibhse, a ghost or spectre; rith, a run; composed"),
            ["settings.prompts"] = new("Leideanna cnaipe an rialtóra", ReviewStatus.NeedsReview, "FGB", "leid, a hint; cnaipe, a button; composed, a speaker should check it"),
            ["settings.promptsAuto"] = new("Uath (lean an rialtóir)", ReviewStatus.Likely, "FGB", "uath-, the usual ga prefix for automatic (uathoibríoch); lean, to follow; composed"),
            ["settings.invertMouse"] = new("Inbhéartaigh Y na luch", ReviewStatus.Likely, "FGB", "inbhéartaigh, to invert (the technical ga word); composed"),
            ["settings.invertPad"] = new("Inbhéartaigh Y an rialtóra", ReviewStatus.NeedsReview, "FGB", "as above with the coined rialtóir"),

            // ---- HUD ----
            ["hud.floor"] = new("Urlár {0} / {1}", ReviewStatus.Likely, "FGB", "FGB urlár, a floor or storey of a building, which suits a dungeon floor better than the gd làr held back for the same reason"),
            ["hud.keys"] = new("Eochracha {0}", ReviewStatus.Likely, "FGB", "FGB eochair, a key (eochracha, plural); the number sits beside it, so no count agreement is needed"),
            ["hud.result.title"] = new("URLÁR {0} CRÍOCHNAITHE", ReviewStatus.Likely, "FGB", "urlár as hud.floor; críochnaithe as quest.done; capitals for the banner"),
            ["hud.result.rank"] = new("CÉIM  {0}", ReviewStatus.Likely, "FGB", "FGB céim, a rank or grade; two spaces kept before the number"),
            ["hud.result.stats"] = new("Am {0}  MARUITHE {1}  STÍL {2}", ReviewStatus.Likely, "FGB", "am, time; maraithe, killed (marú, killing); stíl, style; the spacing is kept"),
            ["hud.result.restarts"] = new("Atosuithe {0}  (-{0} pointí)", ReviewStatus.NeedsReview, "FGB", "atosú, a restarting (FGB tos); pointí, points; coined for the line"),
            ["toast.omen"] = new("Tá bean ní in aice láimhe. Tá rud éigin ag teacht.", ReviewStatus.NeedsReview, "FGB", "bean ní, the washerwoman of folklore; in aice láimhe, nearby; composed, a speaker should check it"),
            ["toast.boon"] = new("Déanann an duine beag an tine te duit (+{0} sláinte)", ReviewStatus.NeedsReview, "FGB", "the brownie rendered as duine beag, the little person of folklore; te, warm; sláinte, health; composed"),
            ["standing.better"] = new("Measann {0} níos mó ort: {1}", ReviewStatus.Likely, "FGB", "meas, to think or regard; níos mó ort, more of you; composed, a speaker should check it"),
            ["standing.worse"] = new("Breathnaíonn {0} ort mar {1} anois", ReviewStatus.Likely, "FGB", "breathnaigh, to look at or regard; composed, a speaker should check it"),
            ["standing.title"] = new("STÁDAS", ReviewStatus.Likely, "FGB", "capitals of stádas as pause.standing"),
            ["standing.subtitle"] = new("Conas a fheiceann muintir an dúin thú. Má ghortaíonn tú creatúir chun toigh a chine, cuimhneoidh siad.", ReviewStatus.NeedsReview, "FGB", "feic, to see; gortaigh, to hurt; cine, kin; cuimhnigh, to remember; composed, a speaker should check it"),
            ["standing.nobody"] = new("Níl tuairim ag duine ar bith ort fós.", ReviewStatus.Likely, "FGB", "tuairim, an opinion; duine ar bith, anyone; fós, yet; composed"),
            ["standing.recent"] = new("LE DEIREANNAÍ", ReviewStatus.NeedsReview, "FGB", "FGB le deireanaí, recently; whether the short form suits a column heading needs a speaker"),
            ["standing.nothingYet"] = new("Níl aon rud go fóill.", ReviewStatus.Likely, "FGB", "go fóill, yet; composed"),
            ["banner.respawn"] = new("{0} chun athbheochain", ReviewStatus.Likely, "FGB", "chun, for the purpose of; athbheochan as pause.respawn"),

            // ---- gaps and falls ----
            ["gap.edge"] = new("Bearna. Lámhach portail trasna ({0}), ceann eile in aice leat ({1}), agus siúil tríd.", ReviewStatus.NeedsReview, "FGB", "bearna, a gap; lámhach, fire or shoot; trasna, across; in aice leat, beside you; composed, a speaker should check it"),
            ["gap.alcove"] = new("Áit ard. Lámhach portail suas ann ({0}), ceann eile in aice leat ({1}), agus siúil tríd.", ReviewStatus.NeedsReview, "FGB", "the alcove described as a high place, not named; composed"),
            ["gap.afterFall"] = new("Cuireadh tú ar ais go dtí an imeall. Lámhach portail trasna na bearna in ionad ({0}, ansin {1}).", ReviewStatus.NeedsReview, "FGB", "imeall, an edge; in ionad, instead of; composed"),
            ["gap.splash"] = new("PLEISG  ag dreapadóireacht amach", ReviewStatus.NeedsReview, "FGB", "pleisc is coined for splash (FGB has it for a shell and a burst); dreapadóireacht, climbing; needs a speaker"),
            ["gap.fellBare"] = new("THIT  -{0}", ReviewStatus.Likely, "FGB", "FGB thit, fell (from tit, to fall); two spaces kept before the number"),
            ["gap.spikes"] = new("BIORANNA  -{0}", ReviewStatus.Likely, "FGB", "bior, a spike or prong (bioranna, plural)"),

            // ---- hints under the HUD ----
            ["hint.open"] = new("oscail", ReviewStatus.Likely, "FGB", "FGB oscail, to open; the bare verb is the imperative"),
            ["hint.jump"] = new("léim", ReviewStatus.Verified, "FGB", "FGB léim, to jump"),
            ["hint.menu"] = new("roghchlár", ReviewStatus.Verified, "FFI FGB", "Firefox ga's word for a menu; FGB roghchlár, a menu or bill of fare"),
            ["hint.revolver"] = new("gunna rothach", ReviewStatus.NeedsReview, "FGB", "a coined compound, gunna, gun, plus rothach, wheeled, for the revolver; needs a speaker"),
            ["hint.shotgun"] = new("gunna scaoilte", ReviewStatus.NeedsReview, "FGB", "a coined compound, gunna plus scaoilte, scattering, for the shotgun; needs a speaker"),
            ["hint.parry"] = new("cosaint", ReviewStatus.Likely, "FGB", "FGB cosaint, a defence; the fencing sense of parry needs a speaker"),
            ["hint.tealPortal"] = new("portail turcaide", ReviewStatus.NeedsReview, "FGB", "portail is the naturalised borrowed word for portal; turcaide coined from turcaid, turquoise; composed"),
            ["hint.violetPortal"] = new("portail corcra", ReviewStatus.Likely, "FGB WKI", "corcra, purple or violet; composed with portail"),
            ["hint.sever"] = new("gearr", ReviewStatus.Likely, "FGB", "FGB gearr, to cut (the imperative); sever is read as cutting the link"),

            // ---- prompts and panels ----
            ["prompt.read"] = new("{0}  léigh: {1}", ReviewStatus.Likely, "FGB", "FGB léigh, to read; the bare verb is the imperative"),
            ["prompt.speak"] = new("{0}  labhair", ReviewStatus.Likely, "FGB", "FGB labhair, to speak; the bare verb is the imperative"),
            ["prompt.closeHint"] = new("{0} nó {1} lena dhúnadh", ReviewStatus.Likely, "FGB", "nó, or; lena dhúnadh, to close it; composed"),
            ["prompt.leaveHint"] = new("{0} nó {1} lena fhágáil", ReviewStatus.Likely, "FGB", "fágáil, leaving; composed"),
            ["cat.warmth"] = new("Tá an tine teolaí, agus tá tú beagáinín níos fearr.", ReviewStatus.NeedsReview, "FGB", "teolaí, warm and cosy (checked for the cat's lines); beagáinín, a little; composed"),

            // ---- banners ----
            ["banner.died"] = new("TÁ TÚ MARBH", ReviewStatus.Likely, "FGB", "FGB marbh, dead; composed as the banner"),
            ["banner.enemies"] = new("Naimhde {0}: maraigh iad leis na doirse a oscailt", ReviewStatus.Likely, "FGB", "naimhde, enemies; maraigh, kill (the imperative); composed"),
            ["banner.clear"] = new("GLANTA", ReviewStatus.Likely, "FGB", "FGB glan, to clean or clear; glanta, cleared; capitals for the banner"),
            ["banner.floorDone"] = new("URLÁR {0} CRÍOCHNAITHE  {1}  {2}", ReviewStatus.Likely, "FGB", "as hud.result.title; the spacing is kept"),
            ["banner.floorStats"] = new("am {0}  maraithe {1}  stíl {2}", ReviewStatus.Likely, "FGB", "as hud.result.stats, lower case"),
            ["banner.firstClear"] = new("an chéad ghlanadh", ReviewStatus.NeedsReview, "FGB", "an chéad, the first; glanadh, the clearing of (from glan as banner.clear); a coinage for the banner"),
            ["banner.newBest"] = new("IS FEARR NUA  (bhí {0})", ReviewStatus.Likely, "FGB", "is fearr, best; nua, new; bhí, it was; composed, a speaker should check it"),
            ["banner.best"] = new("is fearr {0}", ReviewStatus.Likely, "FGB", "is fearr, best; the number sits beside it"),
            ["banner.key"] = new("EOCHAIR", ReviewStatus.Verified, "FGB", "FGB eochair, a key; capitals for the banner"),
            ["banner.chest"] = new("CISTE", ReviewStatus.Verified, "FGB", "FGB ciste, a chest or box; capitals for the banner"),
            ["banner.lockedChest"] = new("CISTE GHLASAITHE", ReviewStatus.Likely, "FGB", "glasaithe, locked (from glasáil, to lock); lenition after the feminine ciste"),
            ["banner.chestHealed"] = new("CISTE  +{0} sláinte", ReviewStatus.Likely, "FGB", "as banner.chest plus sláinte, health; composed for the banner"),
            ["banner.lockedChestHealed"] = new("CISTE GHLASAITHE OSCAILTE  +{0} sláinte", ReviewStatus.Likely, "FGB", "as banner.lockedChest plus oscailte, opened"),
            ["banner.item"] = new("{0}  +{1} i do mhála", ReviewStatus.Likely, "FGB", "i do mhála, into your bag (mála as pause.bag); composed"),
            ["banner.bagFull"] = new("{0}  tá do mhála lán", ReviewStatus.Likely, "FGB", "lán, full; composed"),
            ["banner.locked"] = new("glasaithe: faigh an eochair ar an urlár seo", ReviewStatus.Likely, "FGB", "glasaithe as banner.lockedChest; faigh, get or find (the imperative); urlár as hud.floor; composed"),
            ["banner.noPortal"] = new("Ní féidir portail a chur ansin ({0})", ReviewStatus.Likely, "FGB", "ní féidir, cannot; ansin, there; composed"),
            ["banner.meteor"] = new("MEATAN", ReviewStatus.Likely, "FGB", "meatán, meteor; capitals for the banner"),
            ["banner.meteor2"] = new("MEATAN x2", ReviewStatus.Likely, "FGB", "as banner.meteor; the x2 is kept"),
            ["banner.spikes"] = new("BIORANNA  -{0}", ReviewStatus.Likely, "FGB", "as gap.spikes"),

            // ---- action labels ----
            [Action(GameAction.MoveForward)] = new("Bog ar aghaidh", ReviewStatus.Likely, "FGB", "bog, to move (the imperative); ar aghaidh, forward; composed"),
            [Action(GameAction.MoveBack)] = new("Bog siar", ReviewStatus.Likely, "FGB", "bog as above; siar, back; composed"),
            [Action(GameAction.MoveLeft)] = new("Bog ar chlé", ReviewStatus.Likely, "FGB", "ar chlé, to the left; composed, a speaker should check the phrase"),
            [Action(GameAction.MoveRight)] = new("Bog ar dheis", ReviewStatus.Likely, "FGB", "ar dheis, to the right; composed, a speaker should check the phrase"),
            [Action(GameAction.LookUp)] = new("Féach suas", ReviewStatus.Likely, "FGB", "féach, look (the imperative); suas, up; composed"),
            [Action(GameAction.LookDown)] = new("Féach síos", ReviewStatus.Likely, "FGB", "síos, down; composed"),
            [Action(GameAction.LookLeft)] = new("Féach ar chlé", ReviewStatus.Likely, "FGB", "féach as above; composed, a speaker should check the phrase"),
            [Action(GameAction.LookRight)] = new("Féach ar dheis", ReviewStatus.Likely, "FGB", "as above to the right"),
            [Action(GameAction.Jump)] = new("Léim", ReviewStatus.Verified, "FGB", "FGB léim, to jump; the bare verb is the imperative"),
            [Action(GameAction.FirePrimary)] = new("Lámhach an gunna rothaigh", ReviewStatus.NeedsReview, "FGB", "lámhach, fire or shoot; gunna rothaigh is the coined revolver (hint.revolver); composed"),
            [Action(GameAction.FireSecondary)] = new("Lámhach an gunna scaoilte", ReviewStatus.NeedsReview, "FGB", "lámhach as above; gunna scaoilte is the coined shotgun (hint.shotgun); composed"),
            [Action(GameAction.Parry)] = new("Cosaint", ReviewStatus.Likely, "FGB", "cosaint as hint.parry; the bare noun is the imperative"),
            [Action(GameAction.PortalTeal)] = new("Portail turcaide", ReviewStatus.NeedsReview, "FGB", "as hint.tealPortal; composed"),
            [Action(GameAction.PortalViolet)] = new("Portail corcra", ReviewStatus.Likely, "FGB", "as hint.violetPortal; composed"),
            [Action(GameAction.Respawn)] = new("Athbheochan", ReviewStatus.Likely, "FGB", "as pause.respawn"),
            [Action(GameAction.FlyToggle)] = new("Eitilt shaor", ReviewStatus.NeedsReview, "FGB WKI", "eitilt, flight; saor, free; a coined compound for the debug free-fly; needs a speaker"),
            [Action(GameAction.FlyDown)] = new("Eitil síos", ReviewStatus.Likely, "FGB", "eitil, fly (the imperative); síos, down; composed"),
            [Action(GameAction.DebugInfo)] = new("Dífhabhtú", ReviewStatus.NeedsReview, "FGB", "the loan-translation used in ga technical glossaries for debugging; whether it suits a button needs a speaker"),
            [Action(GameAction.Interact)] = new("Oscail agus bain úsáid as", ReviewStatus.Likely, "FGB", "oscail as hint.open; bain úsáid as, make use of; composed"),
            [Action(GameAction.ClosePortals)] = new("Dún na portail (gearr)", ReviewStatus.NeedsReview, "FGB", "dún, close; portail as hint.tealPortal; gearr as hint.sever; composed"),
            [Action(GameAction.Pause)] = new("Roghchlár sos", ReviewStatus.Likely, "FGB", "roghchlár as hint.menu; sos as pause.title; composed"),
            [Action(GameAction.Slam)] = new("Buille", ReviewStatus.Likely, "FGB", "FGB buille, a blow or stroke; the slamming sense needs a speaker"),
            [Action(GameAction.FireMortar)] = new("Lámhach an mhóirtéir", ReviewStatus.NeedsReview, "FGB", "móirtéar is the borrowed word for a mortar (the weapon is not in FGB); composed; needs a speaker"),
            [Action(GameAction.UseItem)] = new("Úsáid an mhír thapa", ReviewStatus.Likely, "FGB", "úsáid as controls.reserved; mír, an item; tapa, quick; composed"),

            // ---- group headings ----
            [Group(ActionGroup.Movement)] = new("GLUASACHT", ReviewStatus.Likely, "FGB", "FGB gluaiseacht, movement; capitals for the heading"),
            [Group(ActionGroup.Look)] = new("AMHARC", ReviewStatus.Likely, "FGB", "FGB amharc, a look or glance; capitals for the heading"),
            [Group(ActionGroup.Combat)] = new("COMHRAC", ReviewStatus.Likely, "FGB", "FGB comhrac, combat; capitals for the heading"),
            [Group(ActionGroup.Portals)] = new("PORTAIL", ReviewStatus.NeedsReview, "FGB", "the naturalised borrowed word for portal; the loan needs a speaker's blessing"),
            [Group(ActionGroup.System)] = new("CÓRAS", ReviewStatus.Verified, "FGB", "FGB córas, a system; capitals for the heading"),

            // ---- style ranks ----
            [Rank(StyleRank.Fierce)] = new("FIÁIN", ReviewStatus.NeedsReview, "FGB", "FGB fiáin, wild; whether it carries the fierce rank sense needs a speaker"),
            [Rank(StyleRank.Brutal)] = new("BRÚIDIÚIL", ReviewStatus.Likely, "FGB", "FGB brúidiúil, brutal or cruel"),
            [Rank(StyleRank.Savage)] = new("FÍOCHMHAR", ReviewStatus.Likely, "FGB", "FGB fíochmhar, fierce or savage"),
            [Rank(StyleRank.Rowdy)] = new("BORRACH", ReviewStatus.NeedsReview, "FGB", "FGB borrach, insolent or elated; the rowdy sense needs a speaker"),
            [Rank(StyleRank.Merciless)] = new("GAN TRÓCAIRE", ReviewStatus.NeedsReview, "FGB", "trócaire, mercy; the phrase 'without mercy' as a rank name needs a speaker"),
            [Rank(StyleRank.Relentless)] = new("GAN STAONADH", ReviewStatus.NeedsReview, "FGB", "staonadh, relenting; the phrase 'without relenting' as a rank name needs a speaker"),
            [Rank(StyleRank.Apocalyptic)] = new("APACAILIPT", ReviewStatus.NeedsReview, "WKI", "coined from the WKI Irish for an apocalypse; the adjective's form needs a speaker"),
            [Rank(StyleRank.Legendary)] = new("MIOTASACH", ReviewStatus.NeedsReview, "FGB", "coined from miotas, a myth, for legendary; needs a speaker"),
        };

        return m;
    }
}
