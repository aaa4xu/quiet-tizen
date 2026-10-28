using System;
using System.Collections.Generic;
using System.Diagnostics;
using QuietHome.Core.Catalog;
using QuietHome.Core.Screens;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// The NUI implementation of the core's <see cref="IHomeView"/>: draws a
/// <see cref="HomeScreen"/> on the app's window. It draws the chrome (background, title,
/// tabs, clock, toolbar, bottom lines) itself and hands the page content to
/// <see cref="HomePageView"/>, <see cref="AppsPageView"/> or <see cref="SettingsPageView"/>.
/// The Quiet Home app creates one at start and passes it to the core's HomeController.
/// </summary>
sealed class HomeView : IHomeView
{
    const int FocusAnimationMs = 180;

    readonly string root;
    readonly bool fontLoaded;
    readonly Scene scene = new();
    readonly ArtworkCache artwork;
    readonly List<SlotView> slots = new();
    ViewPalette palette = new(true);
    Animation? focusAnimation;
    View? hole;
    int holeSlot = -1;
    TextLabel[] tabs = Array.Empty<TextLabel>();
    TextLabel? clock, toolbarHint, detail, notice;
    long buildMilliseconds;
    int pageArtworkCold;

    /// <summary>
    /// Creates the view for artwork files under <paramref name="root"/> (ending in "/"),
    /// registers the bundled fonts from its "fonts" folder and makes the window transparent.
    /// </summary>
    public HomeView(string root)
    {
        this.root = root;
        // The input preview shows through a hole in the window (see PreviewHole). The pages
        // cover the whole window, so this changes nothing else. It is set once here: switching
        // it later makes the TV reset the preview picture to full screen.
        Window.Default.SetTransparency(true);
        try
        {
            fontLoaded = FontClient.Instance.AddCustomFontDirectory(root + "fonts");
        }
        catch (Exception)
        {
            fontLoaded = false;
        }
        artwork = new ArtworkCache(root);
        artwork.Loaded += () => ArtworkLoaded?.Invoke();
    }

    /// <summary>Raised when an artwork image finished loading or failed.</summary>
    public event Action? ArtworkLoaded;

    /// <summary>Fonts, the last build time and the artwork counts.</summary>
    public ViewStats Stats => new(fontLoaded, buildMilliseconds, artwork.Count, artwork.Ready, artwork.Pending, artwork.Created, pageArtworkCold);

    /// <summary>Starts loading the catalog's artwork in the sizes the pages use.</summary>
    public void Preload(IReadOnlyList<LibraryApp> library) => artwork.Preload(library);

    /// <summary>Draws the page from scratch, or updates focus and values of the page on screen.</summary>
    public void Render(HomeScreen screen, RenderMode mode)
    {
        if (mode == RenderMode.Rebuild)
            Rebuild(screen);
        else
            ApplyFocus(screen, mode == RenderMode.AnimateFocus);
    }

    /// <summary>Sets the notice text; a rebuilt page starts without one.</summary>
    public void ShowNotice(string text)
    {
        if (notice != null)
            notice.Text = text;
    }

    /// <summary>Sets the clock text.</summary>
    public void ShowClock(string text)
    {
        if (clock != null)
            clock.Text = text;
    }

    /// <summary>
    /// Returns the screen rectangle for the live picture of input card <paramref name="slot"/>:
    /// the card where the focus puts it, grown like a focused card and then 4 pixels more,
    /// so the picture's square edges stay under the window around the hole. Returns null when
    /// the page on screen has no input card there. Used by the TV preview port.
    /// </summary>
    public Box? PreviewBox(int slot)
    {
        if (slot < 0 || slot >= slots.Count || slots[slot].Title == null)
            return null;
        var box = slots[slot].Box;
        float scale = FocusStyle.Card.Scale(box.H) * (1 + 4f / box.H);
        float w = box.W * scale, h = box.H * scale;
        float centerX = box.X + box.W / 2f, centerY = box.Y + box.H / 2f;
        return new Box((int)(centerX - w / 2), (int)(centerY - h / 2), (int)(w + .5f), (int)(h + .5f));
    }

    /// <summary>Returns whether input card <paramref name="slot"/> shows the picture below the window right now.</summary>
    public bool HoleOpen(int slot) => hole != null && holeSlot == slot;

    /// <summary>
    /// Makes input card <paramref name="slot"/> see-through, so the picture the TV placed at
    /// <see cref="PreviewBox"/> shows in it. The card's ring and texts stay. Does nothing when
    /// there is no input card there.
    /// </summary>
    public void OpenHole(int slot)
    {
        CloseHole();
        if (PreviewBox(slot) == null)
            return;
        var box = slots[slot].Box;
        hole = PreviewHole.Create(box.W, box.H, Layout.CardRadius);
        slots[slot].Card.Add(hole);
        holeSlot = slot;
    }

    /// <summary>
    /// Destroys the page on screen and draws <paramref name="screen"/> in the fixed order the
    /// screenshots depend on: background, title, tabs, clock, page content, toolbar, bottom
    /// lines; then applies the focus without animation. Measures the time for <c>build_ms</c>.
    /// </summary>
    void Rebuild(HomeScreen screen)
    {
        var stopwatch = Stopwatch.StartNew();
        StopAnimation();
        CloseHole();
        artwork.Park();
        scene.Clear();
        slots.Clear();
        palette = new ViewPalette(screen.Dark);
        Window.Default.BackgroundColor = palette.Window;
        var chrome = screen.Chrome;
        scene.Add(new ImageView { ResourceUrl = root + palette.Themed("free-background.svg"), Size2D = new Size2D(Layout.ScreenWidth, Layout.ScreenHeight) });
        scene.Text(chrome.Title, Layout.Title, Layout.TitleSize, palette.Ink, "Display");
        tabs = new TextLabel[ChromeModel.TabNames.Count];
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i] = scene.Text(ChromeModel.TabNames[i], new Box(Layout.TabX[i], Layout.TabY, Layout.TabWidth[i], Layout.TabHeight), Layout.TabSize, palette.Muted);
            tabs[i].HorizontalAlignment = HorizontalAlignment.Center;
        }
        clock = scene.TextEnd(chrome.Clock, Layout.Clock, Layout.ClockSize, palette.Muted);
        var canvas = new PageCanvas(scene, palette, root, artwork, slots);
        switch (screen.Content)
        {
            case HomePageModel home:
                HomePageView.Build(canvas, home);
                break;
            case AppsPageModel apps:
                AppsPageView.Build(canvas, apps);
                break;
            case SettingsPageModel settings:
                SettingsPageView.Build(canvas, settings);
                break;
        }
        pageArtworkCold = canvas.ColdArtwork;
        foreach (var button in chrome.Toolbar)
            BuildToolbarButton(button);
        toolbarHint = scene.TextEnd("", Layout.ToolbarHint, Layout.ClockSize, palette.Ink);
        detail = scene.Text("", Layout.Detail, Layout.BottomSize, palette.Muted);
        notice = scene.Text("", Layout.Notice, Layout.NoticeSize, palette.Accent);
        scene.TextEnd(chrome.Hint, Layout.Hint, Layout.BottomSize, palette.Muted);
        ApplyFocus(screen, false);
        buildMilliseconds = stopwatch.ElapsedMilliseconds;
        artwork.Recount();
    }

    /// <summary>Adds one round toolbar button with its glass background and icon.</summary>
    void BuildToolbarButton(ToolbarButtonModel button)
    {
        int x = Layout.ToolbarX(button.Position), size = Layout.ToolbarSize;
        var card = scene.Card(x + size / 2, Layout.ToolbarY + size / 2, size, size, ViewPalette.Clear(), 34);
        card.Add(new ImageView { ResourceUrl = root + palette.Themed("glass-button.svg"), Position2D = new Position2D(-16, -16), Size2D = new Size2D(100, 112) });
        card.Add(new ImageView { ResourceUrl = root + "symbol-" + button.Icon + ".png", Color = palette.Ink, Position2D = new Position2D(19, 19), Size2D = new Size2D(30, 30) });
        var ring = FocusStyle.AddRing(card, size, size, size / 2, palette.Ink);
        slots.Add(new SlotView(card, new Box(x, Layout.ToolbarY, size, size)) { Ring = ring });
    }

    /// <summary>
    /// Shows which element has the focus: the current tab, each card's size, border, shadow
    /// and ring (see <see cref="FocusStyle"/>), the focused card on top, the focused input
    /// card widened (see <see cref="HomePageView.Place"/>), and the clock, toolbar hint,
    /// detail line and Settings values. With <paramref name="animate"/> cards move there in a
    /// short animation. A preview hole closes when its card is no longer shown wide.
    /// </summary>
    void ApplyFocus(HomeScreen screen, bool animate)
    {
        StopAnimation();
        if (animate)
            focusAnimation = new Animation(FocusAnimationMs);
        var cards = screen.Cards;
        if (hole != null && (holeSlot >= cards.Count || cards[holeSlot] is not InputCardModel { Expanded: true }))
            CloseHole();
        if (screen.Content is HomePageModel home)
            HomePageView.Place(slots, home, focusAnimation);
        var chrome = screen.Chrome;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool current = i == (int)chrome.Page;
            tabs[i].TextColor = current ? palette.Ink : palette.Muted;
            tabs[i].FontFamily = Scene.Font(current ? "Semibold" : "Medium");
            tabs[i].BackgroundColor = chrome.TabsFocused && current ? palette.TabFocus : ViewPalette.Clear();
            tabs[i].CornerRadius = new Vector4(Layout.TabRadius, Layout.TabRadius, Layout.TabRadius, Layout.TabRadius);
        }
        for (int i = 0; i < slots.Count && i < cards.Count; i++)
            ApplyCardFocus(slots[i], cards[i], focusAnimation);
        focusAnimation?.Play();
        if (clock != null)
            clock.Opacity = chrome.ClockVisible ? 1 : 0;
        if (toolbarHint != null)
            toolbarHint.Text = chrome.ToolbarHint;
        if (detail != null)
            detail.Text = chrome.Detail;
        for (int i = 0; i < slots.Count && i < cards.Count; i++)
        {
            if (cards[i] is SettingsTileModel tile && slots[i].Value is TextLabel value)
                value.Text = tile.Value;
        }
    }

    /// <summary>Gives one card its focused or normal look, through <paramref name="animation"/> when there is one.</summary>
    void ApplyCardFocus(SlotView slot, CardModel card, Animation? animation)
    {
        bool focused = card.Focused;
        var style = FocusStyle.For(card);
        float scale = focused ? style.Scale(slot.Box.H) : 1;
        slot.Card.BorderlineWidth = style.Border;
        slot.Card.BorderlineColor = ViewPalette.CardBorder();
        if (animation != null)
            animation.AnimateTo(slot.Card, "Scale", new Vector3(scale, scale, 1));
        else
            slot.Card.Scale = new Vector3(scale, scale, 1);
        if (slot.Shadow != null)
            Fade(slot.Shadow, focused ? 1 : .55f, animation);
        if (slot.Ring != null)
            Fade(slot.Ring, focused ? 1 : 0, animation);
        if (focused)
            slot.Card.RaiseToTop();
    }

    /// <summary>Sets <paramref name="view"/>'s opacity, through <paramref name="animation"/> when there is one.</summary>
    static void Fade(View view, float opacity, Animation? animation)
    {
        if (animation != null)
            animation.AnimateTo(view, "Opacity", opacity);
        else
            view.Opacity = opacity;
    }

    /// <summary>Removes the see-through hole from its card, if there is one; the preview port calls it when the preview ends.</summary>
    public void CloseHole()
    {
        if (hole == null)
            return;
        hole.Unparent();
        hole.Dispose();
        hole = null;
        holeSlot = -1;
    }

    /// <summary>Stops and frees a focus animation still running.</summary>
    void StopAnimation()
    {
        if (focusAnimation == null)
            return;
        focusAnimation.Stop();
        focusAnimation.Dispose();
        focusAnimation = null;
    }
}
