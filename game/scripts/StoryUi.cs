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
    }

    private static readonly Color Gold = new(1f, 0.8f, 0.3f);
    private static readonly Color Paper = new(0.93f, 0.89f, 0.8f);
    private static readonly Color Dim = new(0.68f, 0.64f, 0.56f);

    private Control _ambient = null!;
    private Control _cardBox = null!, _captionBox = null!, _promptBox = null!;
    private Label _cardName = null!, _cardEpigraph = null!, _caption = null!, _prompt = null!, _speaker = null!;
    private Control _reader = null!;
    private Label _readerFloor = null!, _readerTitle = null!, _readerText = null!, _readerHint = null!;
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
