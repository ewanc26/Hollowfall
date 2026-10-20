using Godot;
using Hollowfall.Core.Input;
using Hollowfall.Core.Story;

namespace Hollowfall.Game;

/// <summary>
/// Everything the story puts on screen: the floor title card that fades in on entering a floor, a caption for one-off lines
/// (boss, death, floor complete), a prompt over a note you can read, the reading panel for a note, and the full-screen pages
/// for the opening crawl and the ending. Reading and the pages pause the game and close with Interact or the menu button.
/// It holds no story logic: <see cref="StoryDirector"/> decides what to show and when.
/// </summary>
public partial class StoryUi : CanvasLayer
{
    private const double CardIn = 0.9, CardHold = 3.2, CardOut = 1.4;
    private const double CrawlGap = 1.7, CrawlFade = 1.2, CrawlStart = 0.5;

    private enum Page
    {
        None,
        Note,
        Crawl,
        Dialogue,
    }

    private static readonly Color Gold = new(1f, 0.8f, 0.3f);
    private static readonly Color Paper = new(0.93f, 0.89f, 0.8f);
    private static readonly Color Dim = new(0.68f, 0.64f, 0.56f);

    private Control _ambient = null!;
    private Control _cardBox = null!, _captionBox = null!, _promptBox = null!;
    private Label _cardName = null!, _cardEpigraph = null!, _caption = null!, _prompt = null!, _speaker = null!;
    private Control _reader = null!;
    private Label _readerFloor = null!, _readerTitle = null!, _readerText = null!, _readerHint = null!;
    private Control _dialogue = null!;
    private Label _dialogueSpeaker = null!, _dialogueText = null!, _dialogueHint = null!;
    private VBoxContainer _dialogueChoices = null!;
    private Action<int>? _onChoose;
    private Control _crawl = null!;
    private ColorRect _crawlBack = null!;
    private Label _crawlHeading = null!, _crawlHint = null!;
    private VBoxContainer _crawlLines = null!;

    private readonly List<Label> _paragraphs = [];
    private FloorCard? _pendingCard;
    private double _cardDelay, _cardTime = -1, _captionTime = -1, _captionTotal, _crawlTime;
    private Page _page = Page.None;
    private bool _menuOpen;
    private ulong _closedFrame = ulong.MaxValue;

    /// <summary>True while a note, the opening or the ending is up. The game tree is paused for as long as it is.</summary>
    public bool Modal => _page != Page.None;

    public string? CurrentNoteId { get; private set; }

    /// <summary>The text shown in the reading panel, for tests.</summary>
    public string ReaderText => _readerText.Text;

    public string CaptionText => _caption.Text;

    /// <summary>The name shown above a spoken line (empty for an ordinary caption).</summary>
    public string SpeakerText => _speaker.Text;

    /// <summary>True while someone's spoken line is on screen.</summary>
    public bool Speaking => _speaker.Visible && _captionTime >= 0;

    public string CardTitle => _cardName.Text;

    public bool CardVisible => _cardTime >= 0 && _cardBox.Modulate.A > 0.01f;

    public bool CrawlOpen => _page == Page.Crawl;

    /// <summary>Raised when a note, the opening or the ending is dismissed.</summary>
    public event Action? Closed;

    public override void _Ready()
    {
        Layer = 12;
        ProcessMode = ProcessModeEnum.Always; // fades run while the game is paused behind a page
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiFonts.BodyTheme(22) };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _ambient = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _ambient.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_ambient);

        // The floor title card hangs at the top of the screen, above the HUD's banner.
        var cardTop = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        cardTop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide, Control.LayoutPresetMode.Minsize, 0);
        cardTop.OffsetTop = 30;
        _ambient.AddChild(cardTop);
        var cardColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        cardColumn.AddThemeConstantOverride("separation", 0);
        cardTop.AddChild(cardColumn);
        _cardBox = cardColumn;
        _cardName = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore }, 44, UiFonts.Display, Gold, 10);
        _cardEpigraph = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore }, 22, UiFonts.Body, Paper, 7);
        cardColumn.AddChild(_cardName);
        cardColumn.AddChild(_cardEpigraph);

        // One-off lines sit low on the screen, clear of the crosshair and the HUD's banner.
        var captionBottom = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        captionBottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide, Control.LayoutPresetMode.Minsize, 0);
        captionBottom.OffsetBottom = -185;
        captionBottom.GrowVertical = Control.GrowDirection.Begin;
        _ambient.AddChild(captionBottom);
        _caption = Outlined(new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(820, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        }, 26, UiFonts.Body, Paper, 8);
        // A speaker's name sits above the line, in a display face. It is empty (and takes no room) for ordinary captions.
        var captionColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        captionColumn.AddThemeConstantOverride("separation", 2);
        captionBottom.AddChild(captionColumn);
        _speaker = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false }, 30, UiFonts.Display, Gold, 9);
        captionColumn.AddChild(_speaker);
        _caption.Modulate = Colors.White;
        captionColumn.AddChild(_caption);
        _captionBox = captionColumn;

        // The prompt over a note you can read.
        var promptCentre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        promptCentre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        promptCentre.OffsetTop = 150;
        _ambient.AddChild(promptCentre);
        _prompt = Outlined(new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false }, 24, UiFonts.DisplayMedium, Gold, 8);
        promptCentre.AddChild(_prompt);
        _promptBox = _prompt;

        BuildReader(root);
        BuildCrawl(root);
        BuildDialogue(root);
    }

    private static Label Outlined(Label label, int size, Font font, Color colour, int outline)
    {
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        label.AddThemeConstantOverride("outline_size", outline);
        return label;
    }

    private void BuildReader(Control root)
    {
        var back = new ColorRect { Color = new Color(0, 0, 0, 0.62f), MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(back);
        _reader = back;

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        back.AddChild(centre);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(780, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.05f, 0.97f),
            BorderColor = new Color(0.55f, 0.42f, 0.2f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 34,
            ContentMarginRight = 34,
            ContentMarginTop = 26,
            ContentMarginBottom = 22,
        });
        centre.AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);

        _readerFloor = new Label { HorizontalAlignment = HorizontalAlignment.Left };
        _readerFloor.AddThemeColorOverride("font_color", Dim);
        _readerFloor.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(_readerFloor);

        _readerTitle = new Label { HorizontalAlignment = HorizontalAlignment.Left, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _readerTitle.AddThemeFontOverride("font", UiFonts.Display);
        _readerTitle.AddThemeFontSizeOverride("font_size", 32);
        _readerTitle.AddThemeColorOverride("font_color", Gold);
        column.AddChild(_readerTitle);

        column.AddChild(new HSeparator());

        _readerText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(710, 0) };
        _readerText.AddThemeFontSizeOverride("font_size", 28);
        _readerText.AddThemeColorOverride("font_color", Paper);
        _readerText.AddThemeConstantOverride("line_spacing", 4);
        column.AddChild(_readerText);

        _readerHint = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _readerHint.AddThemeColorOverride("font_color", Dim);
        _readerHint.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(_readerHint);
    }

    private void BuildDialogue(Control root)
    {
        // A panel low on the screen, so the speaker stays in view above it. It takes the mouse and focus; the rest of the screen does not.
        var anchor = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        anchor.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide, Control.LayoutPresetMode.Minsize, 0);
        anchor.OffsetBottom = -62;
        anchor.GrowVertical = Control.GrowDirection.Begin;
        root.AddChild(anchor);
        _dialogue = anchor;

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(860, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.05f, 0.05f, 0.93f),
            BorderColor = new Color(0.55f, 0.42f, 0.2f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 28,
            ContentMarginRight = 28,
            ContentMarginTop = 18,
            ContentMarginBottom = 14,
        });
        anchor.AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);

        _dialogueSpeaker = new Label { HorizontalAlignment = HorizontalAlignment.Left };
        _dialogueSpeaker.AddThemeFontSizeOverride("font_size", 30);
        _dialogueSpeaker.AddThemeColorOverride("font_color", Gold);
        column.AddChild(_dialogueSpeaker);

        _dialogueText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(800, 0) };
        _dialogueText.AddThemeFontSizeOverride("font_size", 26);
        _dialogueText.AddThemeColorOverride("font_color", Paper);
        _dialogueText.AddThemeConstantOverride("line_spacing", 3);
        column.AddChild(_dialogueText);

        column.AddChild(new HSeparator());

        _dialogueChoices = new VBoxContainer();
        _dialogueChoices.AddThemeConstantOverride("separation", 4);
        column.AddChild(_dialogueChoices);

        _dialogueHint = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _dialogueHint.AddThemeColorOverride("font_color", Dim);
        _dialogueHint.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(_dialogueHint);
    }

    private static StyleBoxFlat ButtonBox(Color fill, Color border) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthBottom = 1,
        BorderWidthTop = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        ContentMarginLeft = 14,
        ContentMarginRight = 14,
        ContentMarginTop = 5,
        ContentMarginBottom = 5,
    };

    private void BuildCrawl(Control root)
    {
        _crawlBack = new ColorRect { Color = new Color(0, 0, 0, 0.94f), MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _crawlBack.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_crawlBack);
        _crawl = _crawlBack;

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _crawlBack.AddChild(centre);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(860, 0) };
        column.AddThemeConstantOverride("separation", 22);
        centre.AddChild(column);

        _crawlHeading = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _crawlHeading.AddThemeFontOverride("font", UiFonts.Display);
        _crawlHeading.AddThemeFontSizeOverride("font_size", 52);
        _crawlHeading.AddThemeColorOverride("font_color", Gold);
        column.AddChild(_crawlHeading);

        _crawlLines = new VBoxContainer();
        _crawlLines.AddThemeConstantOverride("separation", 20);
        column.AddChild(_crawlLines);

        _crawlHint = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _crawlHint.AddThemeColorOverride("font_color", Dim);
        _crawlHint.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(_crawlHint);
    }

    // ---- ambient pieces -----------------------------------------------------------------------

    /// <summary>Shows a floor title card after <paramref name="delay"/> seconds. A newer card replaces one that has not started.</summary>
    public void ShowCard(FloorCard card, double delay = 0)
    {
        _pendingCard = card;
        _cardDelay = delay;
    }

    /// <summary>Shows one line (or a few) low on the screen for <paramref name="seconds"/>, fading in and out.</summary>
    /// <summary>A line spoken by someone, with their name above it. The name is drawn in a face that has every letter in it.</summary>
    public void ShowSpeech(string speaker, string text, double seconds)
    {
        ShowCaption(text, seconds);
        _speaker.Text = speaker;
        _speaker.Visible = true;
        _speaker.AddThemeFontOverride("font", UiFonts.FaceWithGlyphs(speaker, UiFonts.Display));
    }

    public void ShowCaption(string text, double seconds)
    {
        _speaker.Text = "";
        _speaker.Visible = false;
        _caption.Text = text;
        _captionTotal = Math.Max(seconds, 0.1);
        _captionTime = 0;
        _captionBox.Modulate = new Color(1, 1, 1, 0);
    }

    public void ClearCaption()
    {
        _captionTime = -1;
        _caption.Text = "";
        _speaker.Text = "";
        _speaker.Visible = false;
        _captionBox.Modulate = new Color(1, 1, 1, 0);
    }

    /// <summary>The prompt over a readable note; null hides it.</summary>
    public void SetPrompt(string? text)
    {
        _prompt.Text = text ?? "";
        _promptBox.Visible = text != null && !Modal && !_menuOpen;
    }

    /// <summary>Hides the title card, caption and prompt while a menu covers the game.</summary>
    public void SetMenuOpen(bool open)
    {
        _menuOpen = open;
        _ambient.Visible = !open;
    }

    // ---- pages --------------------------------------------------------------------------------

    /// <summary>Opens the reading panel for a note and pauses the game.</summary>
    public void ShowNote(StoryNote note, string floorLabel, string closeHint)
    {
        CloseQuietly();
        _readerFloor.Text = floorLabel;
        _readerTitle.Text = note.Title;
        _readerText.Text = note.Text;
        _readerHint.Text = closeHint;
        CurrentNoteId = note.Id;
        _reader.Visible = true;
        Open(Page.Note);
    }

    /// <summary>Opens a full-screen page of paragraphs that fade in one after another (the opening crawl and the ending).</summary>
    public void ShowCrawl(string heading, IReadOnlyList<string> paragraphs, string closeHint, float darkness = 0.94f)
    {
        CloseQuietly();
        _crawlHeading.Text = heading;
        _crawlHint.Text = closeHint;
        _crawlBack.Color = new Color(0, 0, 0, darkness);
        foreach (var old in _paragraphs) old.QueueFree();
        _paragraphs.Clear();
        foreach (var text in paragraphs)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(860, 0),
                Modulate = new Color(1, 1, 1, 0),
            };
            label.AddThemeFontSizeOverride("font_size", 30);
            label.AddThemeColorOverride("font_color", Paper);
            _crawlLines.AddChild(label);
            _paragraphs.Add(label);
        }
        _crawlHint.Modulate = new Color(1, 1, 1, 0);
        _crawlTime = 0;
        _crawl.Visible = true;
        Open(Page.Crawl);
    }

    /// <summary>Whether every paragraph of the crawl is showing.</summary>
    public bool CrawlComplete => _page == Page.Crawl && _crawlTime >= CrawlStart + CrawlGap * Math.Max(_paragraphs.Count - 1, 0) + CrawlFade;

    /// <summary>The line the speaker says now (for tests).</summary>
    public string DialogueText => _dialogueText.Text;

    /// <summary>The labels of the choice buttons now showing (for tests).</summary>
    public IReadOnlyList<string> DialogueChoices => [.. _dialogueChoices.GetChildren().OfType<Button>().Where(b => !b.IsQueuedForDeletion()).Select(b => b.Text)];

    public bool DialogueOpen => _page == Page.Dialogue;

    /// <summary>
    /// Opens the choice panel and pauses the game: the speaker's name above their line, then a button for each thing the player can say.
    /// The first button has focus, so the keyboard and a gamepad work as in the menus. <paramref name="onChoose"/> gets the index of the
    /// button pressed; Interact or the menu button closes the panel.
    /// </summary>
    public void ShowDialogue(string speaker, string text, IReadOnlyList<string> choices, Action<int> onChoose, string closeHint)
    {
        CloseQuietly();
        _dialogueSpeaker.Text = speaker;
        _dialogueSpeaker.AddThemeFontOverride("font", UiFonts.FaceWithGlyphs(speaker, UiFonts.Display));
        _dialogueHint.Text = closeHint;
        _onChoose = onChoose;
        _dialogue.Visible = true;
        Open(Page.Dialogue);
        UpdateDialogue(text, choices);
    }

    /// <summary>Replaces the line and the buttons of the open panel.</summary>
    public void UpdateDialogue(string text, IReadOnlyList<string> choices)
    {
        _dialogueText.Text = text;
        foreach (var old in _dialogueChoices.GetChildren())
        {
            _dialogueChoices.RemoveChild(old);
            old.QueueFree();
        }
        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            var b = new Button { Text = choices[i], Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.All };
            b.AddThemeFontSizeOverride("font_size", 22);
            b.AddThemeColorOverride("font_color", Paper);
            b.AddThemeStyleboxOverride("normal", ButtonBox(new Color(0.12f, 0.11f, 0.11f), new Color(0.3f, 0.27f, 0.24f)));
            b.AddThemeStyleboxOverride("hover", ButtonBox(new Color(0.2f, 0.17f, 0.13f), new Color(0.7f, 0.55f, 0.3f)));
            b.AddThemeStyleboxOverride("pressed", ButtonBox(new Color(0.3f, 0.2f, 0.12f), new Color(1f, 0.7f, 0.2f)));
            b.AddThemeStyleboxOverride("focus", ButtonBox(new Color(0.2f, 0.17f, 0.13f), new Color(1f, 0.75f, 0.25f)));
            b.Pressed += () => _onChoose?.Invoke(index);
            _dialogueChoices.AddChild(b);
        }
        if (_dialogueChoices.GetChildCount() > 0 && IsInsideTree()) ((Control)_dialogueChoices.GetChild(0)).GrabFocus();
    }

    /// <summary>Presses a choice button as the player would (for tests).</summary>
    public void PressDialogueChoice(int index)
    {
        if (_dialogueChoices.GetChildOrNull<Button>(index) is { } b) b.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void Open(Page page)
    {
        _page = page;
        _promptBox.Visible = false;
        if (IsInsideTree()) GetTree().Paused = true;
    }

    private void CloseQuietly()
    {
        _reader.Visible = false;
        _crawl.Visible = false;
        _dialogue.Visible = false;
        _onChoose = null;
        _page = Page.None;
        CurrentNoteId = null;
    }

    /// <summary>Dismisses whatever page is open and gives the game back.</summary>
    public void Close()
    {
        if (_page == Page.None) return;
        CloseQuietly();
        _closedFrame = Engine.GetPhysicsFrames();
        if (IsInsideTree() && !_menuOpen) GetTree().Paused = false;
        Closed?.Invoke();
    }

    /// <summary>
    /// True for the physics step just after a page was closed with the Interact button. The same press is still "just
    /// pressed" then, and must not open the note again or a chest beside it.
    /// </summary>
    public bool SwallowInteract => _closedFrame != ulong.MaxValue && Engine.GetPhysicsFrames() - _closedFrame <= 1;

    public override void _Input(InputEvent e)
    {
        if (_page == Page.None || e.IsEcho()) return;
        bool interact = e.IsActionPressed(InputMapper.Name(GameAction.Interact));
        bool menu = e.IsActionPressed(InputMapper.Name(GameAction.Pause));
        if (!interact && !menu) return;

        GetViewport().SetInputAsHandled();
        if (_page == Page.Crawl && !CrawlComplete)
        {
            // The first press shows everything; the next dismisses it.
            _crawlTime = 1e6;
            return;
        }
        Close();
    }

    // ---- fades --------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (_page == Page.Crawl)
        {
            _crawlTime += delta;
            for (int i = 0; i < _paragraphs.Count; i++)
                _paragraphs[i].Modulate = new Color(1, 1, 1, (float)Math.Clamp((_crawlTime - CrawlStart - i * CrawlGap) / CrawlFade, 0, 1));
            _crawlHint.Modulate = new Color(1, 1, 1, CrawlComplete ? 1 : 0);
        }

        // Cards and captions wait while the game is paused behind a page or a menu.
        if (GetTree().Paused) return;

        if (_pendingCard is { } pending)
        {
            _cardDelay -= delta;
            if (_cardDelay <= 0)
            {
                _cardName.Text = pending.Title;
                _cardEpigraph.Text = pending.Epigraph;
                _cardTime = 0;
                _pendingCard = null;
            }
        }
        if (_cardTime >= 0)
        {
            _cardTime += delta;
            double total = CardIn + CardHold + CardOut;
            double a = _cardTime < CardIn ? _cardTime / CardIn : _cardTime < CardIn + CardHold ? 1 : 1 - (_cardTime - CardIn - CardHold) / CardOut;
            _cardBox.Modulate = new Color(1, 1, 1, (float)Math.Clamp(a, 0, 1));
            if (_cardTime >= total)
            {
                _cardTime = -1;
                _cardBox.Modulate = new Color(1, 1, 1, 0);
            }
        }
        if (_captionTime >= 0)
        {
            _captionTime += delta;
            double a = Math.Min(_captionTime / 0.4, _captionTotal > 100 ? 1 : (_captionTotal - _captionTime) / 0.8);
            _captionBox.Modulate = new Color(1, 1, 1, (float)Math.Clamp(a, 0, 1));
            if (_captionTime >= _captionTotal) ClearCaption();
        }
    }
}
