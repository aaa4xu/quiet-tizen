using System;

namespace QuietHome.Core.Theme;

/// <summary>
/// Color calculations for placeholder app cards, which have only a background color and
/// the app's name on top. <see cref="DarkText"/> picks dark or white text by APCA contrast
/// (0.0.98G-4g) with the card; <see cref="Tone"/> gives the card's own color made a little
/// lighter or darker in OKLab, for the circle drawn on it. The TV host calls these while
/// it builds the Apps page and the dock.
/// </summary>
public static class ColorMath
{
    /// <summary>The dark text color used on light placeholder cards.</summary>
    public static Rgba PlaceholderInk { get; } = new(.11f, .11f, .12f, 1);

    /// <summary>
    /// Returns true when <see cref="PlaceholderInk"/> has more contrast with
    /// <paramref name="background"/> than white does, so the card's text should be dark.
    /// </summary>
    public static bool DarkText(Rgba background) => Apca(PlaceholderInk, background) > Apca(Rgba.White, background);

    /// <summary>
    /// Returns <paramref name="background"/> with its perceived lightness moved by
    /// <paramref name="delta"/> (about ±0.06 for a visible but quiet step); hue and
    /// saturation stay the same. Uses Ottosson's toe-corrected lightness, so near-black
    /// colors still change visibly.
    /// </summary>
    public static Rgba Tone(Rgba background, double delta)
    {
        double r = Linear(background.R), g = Linear(background.G), b = Linear(background.B);
        double l = Math.Cbrt(.4122214708 * r + .5363325363 * g + .0514459929 * b), m = Math.Cbrt(.2119034982 * r + .6806995451 * g + .1073969566 * b), s = Math.Cbrt(.0883024619 * r + .2817188376 * g + .6299787005 * b);
        double lightness = .2104542553 * l + .7936177850 * m - .0040720468 * s, a = 1.9779984951 * l - 2.4285922050 * m + .4505937099 * s, bb = .0259040371 * l + .7827717662 * m - .8086757660 * s;
        const double k1 = .206, k2 = .03, k3 = (1 + k1) / (1 + k2);
        double lr = (k3 * lightness - k1 + Math.Sqrt((k3 * lightness - k1) * (k3 * lightness - k1) + 4 * k2 * k3 * lightness)) / 2 + delta;
        lightness = (lr * lr + k1 * lr) / (k3 * (lr + k2));
        l = Math.Pow(lightness + .3963377774 * a + .2158037573 * bb, 3);
        m = Math.Pow(lightness - .1055613458 * a - .0638541728 * bb, 3);
        s = Math.Pow(lightness - .0894841775 * a - 1.2914855480 * bb, 3);
        return new Rgba(
            (float)Gamma(4.0767416621 * l - 3.3077115913 * m + .2309699292 * s),
            (float)Gamma(-1.2684380046 * l + 2.6097574011 * m - .3413193965 * s),
            (float)Gamma(-.0041960863 * l - .7034186147 * m + 1.7076147010 * s),
            1);
    }

    /// <summary>Converts one sRGB channel (0..1) to linear light.</summary>
    static double Linear(double c) => c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);

    /// <summary>Converts linear light back to an sRGB channel, clamped to 0..1.</summary>
    static double Gamma(double c)
    {
        c = Math.Clamp(c, 0, 1);
        return c <= .0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - .055;
    }

    /// <summary>APCA's screen luminance of a color, with its soft clamp for near-black.</summary>
    static double ApcaY(Rgba c)
    {
        double y = .2126729 * Math.Pow(c.R, 2.4) + .7151522 * Math.Pow(c.G, 2.4) + .0721750 * Math.Pow(c.B, 2.4);
        return y < .022 ? y + Math.Pow(.022 - y, 1.414) : y;
    }

    /// <summary>APCA lightness contrast of <paramref name="text"/> on <paramref name="background"/>, as a positive number (0 when too low to count).</summary>
    static double Apca(Rgba text, Rgba background)
    {
        double yt = ApcaY(text), yb = ApcaY(background);
        if (Math.Abs(yb - yt) < .0005)
            return 0;
        double s = yb > yt ? (Math.Pow(yb, .56) - Math.Pow(yt, .57)) * 1.14 : (Math.Pow(yb, .65) - Math.Pow(yt, .62)) * 1.14;
        return Math.Abs(s) < .1 ? 0 : Math.Abs(s) - .027;
    }
}
