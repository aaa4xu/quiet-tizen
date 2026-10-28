using System;

namespace QuietHome.TizenHost.Views;

/// <summary>A rectangle on the 1920 × 1080 screen, in pixels from the top left corner.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="W">Width.</param>
/// <param name="H">Height.</param>
readonly record struct Box(int X, int Y, int W, int H);

/// <summary>
/// Where everything goes on the 1920 × 1080 screen, and the font sizes. The page views and
/// <see cref="HomeView"/> take every coordinate from here. The numbers are the ones the
/// README screenshots were taken with: changing any of them changes the picture.
/// </summary>
static class Layout
{
    /// <summary>Screen width.</summary>
    public const int ScreenWidth = 1920;

    /// <summary>Screen height.</summary>
    public const int ScreenHeight = 1080;

    /// <summary>The page title.</summary>
    public static readonly Box Title = new(76, 55, 1350, 100);

    /// <summary>Font size of the page title.</summary>
    public const float TitleSize = 76;

    /// <summary>Left edges of the three tabs.</summary>
    public static readonly int[] TabX = { 54, 221, 366 };

    /// <summary>Widths of the three tabs.</summary>
    public static readonly int[] TabWidth = { 153, 131, 182 };

    /// <summary>Top edge of the tabs.</summary>
    public const int TabY = 158;

    /// <summary>Height of the tabs.</summary>
    public const int TabHeight = 68;

    /// <summary>Font size of the tabs.</summary>
    public const float TabSize = 30;

    /// <summary>Corner radius of the highlight behind the focused tab.</summary>
    public const float TabRadius = 34;

    /// <summary>The clock, right-aligned.</summary>
    public static readonly Box Clock = new(1540, 170, 300, 45);

    /// <summary>The focused toolbar button's name, in the clock's place.</summary>
    public static readonly Box ToolbarHint = new(1530, 170, 310, 45);

    /// <summary>Font size of the clock and the toolbar hint.</summary>
    public const float ClockSize = 24;

    /// <summary>The line at the bottom left.</summary>
    public static readonly Box Detail = new(80, 1030, 1320, 35);

    /// <summary>The line at the bottom right, right-aligned.</summary>
    public static readonly Box Hint = new(1310, 1030, 530, 35);

    /// <summary>Font size of the bottom lines.</summary>
    public const float BottomSize = 20;

    /// <summary>The notice above the bottom lines.</summary>
    public static readonly Box Notice = new(80, 1008, 1760, 34);

    /// <summary>Font size of the notice.</summary>
    public const float NoticeSize = 22;

    /// <summary>Left margin of the page content.</summary>
    public const int Margin = 80;

    /// <summary>
    /// Top edge of the page content under the title and the tabs: the input cards, the first
    /// row of app cards and of Settings tiles, and the messages shown in their place.
    /// </summary>
    public const int ContentTop = 262;

    /// <summary>"No connected inputs found." on the Home page.</summary>
    public static readonly Box NoInputs = new(80, ContentTop, 1500, 70);

    /// <summary>Font size of "No connected inputs found.".</summary>
    public const float NoInputsSize = 34;

    /// <summary>Top edge of the input cards.</summary>
    public const int InputY = ContentTop;

    /// <summary>Height of the input cards.</summary>
    public const int InputHeight = 442;

    /// <summary>Widest an input card gets.</summary>
    public const int InputMaxWidth = 540;

    /// <summary>Gap between input cards.</summary>
    public const int InputGap = 40;

    /// <summary>Width all input cards and gaps share.</summary>
    public const int InputRowWidth = 1760;

    /// <summary>Size of the input icon in the middle of the card.</summary>
    public const int InputIconSize = 156;

    /// <summary>Top edge and height of the input name under the card.</summary>
    public const int InputNameY = 716, InputNameHeight = 45;

    /// <summary>Top edge and height of the line under the input name.</summary>
    public const int InputTextY = 760, InputTextHeight = 36;

    /// <summary>Font sizes of the input name and the line under it.</summary>
    public const float InputNameSize = 31, InputTextSize = 24;

    /// <summary>Width of the focused input card: 16:9 at <see cref="InputHeight"/>, the shape of the input's picture.</summary>
    public const int InputWideWidth = 786;

    /// <summary>Returns the width of each of <paramref name="count"/> input cards while none has the focus.</summary>
    public static int InputWidth(int count) => Math.Min(InputMaxWidth, (InputRowWidth - InputGap * (count - 1)) / count);

    /// <summary>
    /// Returns where input card <paramref name="index"/> of <paramref name="count"/> goes.
    /// The card at <paramref name="focused"/> is <see cref="InputWideWidth"/> wide and the
    /// others share the rest of the row; with <paramref name="focused"/> -1 all are equal.
    /// </summary>
    public static Box InputBox(int index, int count, int focused)
    {
        if (focused < 0)
        {
            int w = InputWidth(count);
            return new Box(Margin + index * (w + InputGap), InputY, w, InputHeight);
        }
        int narrow = count > 1 ? Math.Min(InputMaxWidth, (InputRowWidth - InputGap * (count - 1) - InputWideWidth) / (count - 1)) : 0;
        int x = Margin;
        for (int i = 0; i < index; i++)
            x += (i == focused ? InputWideWidth : narrow) + InputGap;
        return new Box(x, InputY, index == focused ? InputWideWidth : narrow, InputHeight);
    }

    /// <summary>The glass shelf under the dock.</summary>
    public static readonly Box Shelf = new(0, 784, 1920, 280);

    /// <summary>Size of a dock card.</summary>
    public const int DockWidth = 253, DockHeight = 126;

    /// <summary>Top edge of the dock cards.</summary>
    public const int DockY = 854;

    /// <summary>Returns the left edge of dock card <paramref name="index"/>.</summary>
    public static int DockX(int index) => (int)Math.Round(80 + index * (253.333 + 48));

    /// <summary>Size of an app card on the Apps page.</summary>
    public const int AppWidth = 328, AppHeight = 184;

    /// <summary>Distance between the left edges of neighbouring app cards, and between rows.</summary>
    public const int AppColumnStep = 358, AppRowStep = 253;

    /// <summary>Top edge of the first row of app cards.</summary>
    public const int AppTop = ContentTop;

    /// <summary>Gap between an app card and its name, height of the name and its font size.</summary>
    public const int AppNameGap = 13, AppNameHeight = 38;

    /// <summary>Font size of the app name under its card.</summary>
    public const float AppNameSize = 27;

    /// <summary>"1 / 2" above the grid, right-aligned.</summary>
    public static readonly Box AppPages = new(1650, 218, 190, 30);

    /// <summary>Font size of the page number above the grid.</summary>
    public const float AppPagesSize = 21;

    /// <summary>"Apps could not be loaded." on the Apps page.</summary>
    public static readonly Box AppsLoadFailed = new(80, ContentTop, 1500, 70);

    /// <summary>Font size of "Apps could not be loaded.".</summary>
    public const float AppsLoadFailedSize = 32;

    /// <summary>Size of a Settings tile.</summary>
    public const int TileWidth = 410, TileHeight = 286;

    /// <summary>Distance between the left edges of neighbouring tiles, and between rows.</summary>
    public const int TileColumnStep = 450, TileRowStep = 332;

    /// <summary>Top edge of the first row of tiles.</summary>
    public const int TileTop = ContentTop;

    /// <summary>The tile's title, value and hint, relative to the tile.</summary>
    public static readonly Box TileTitle = new(26, 25, 348, 42), TileValue = new(26, 151, 358, 56), TileHint = new(26, 226, 370, 34);

    /// <summary>Font sizes of the tile's title, value and hint.</summary>
    public const float TileTitleSize = 29, TileValueSize = 38, TileHintSize = 20;

    /// <summary>
    /// Corner radius of every rectangular card (input, app, dock and Settings cards) and of
    /// app artwork that fills its card. The Settings tile and dock artwork files are drawn
    /// with the same radius. The focus ring's corners follow it (see <see cref="FocusStyle.AddRing"/>).
    /// </summary>
    public const int CardRadius = 24;

    /// <summary>Size of a toolbar button.</summary>
    public const int ToolbarSize = 68;

    /// <summary>Top edge of the toolbar buttons.</summary>
    public const int ToolbarY = 69;

    /// <summary>Returns the left edge of the toolbar button at <paramref name="position"/> (-1 for "Edit inputs").</summary>
    public static int ToolbarX(int position) => 1596 + position * 88;
}
