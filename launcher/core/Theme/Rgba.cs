using System;

namespace QuietHome.Core.Theme;

/// <summary>
/// A color with red, green, blue and alpha channels from 0 to 1. The core describes every
/// color with this type so that it needs no Tizen reference; the TV host turns it into
/// a <c>Tizen.NUI.Color</c> with the same four floats right before drawing.
/// </summary>
public readonly record struct Rgba(float R, float G, float B, float A)
{
    /// <summary>Opaque white; also the fallback for a color that cannot be read.</summary>
    public static Rgba White => new(1, 1, 1, 1);

    /// <summary>Fully transparent black, used for views that must not paint anything.</summary>
    public static Rgba Transparent => new(0, 0, 0, 0);

    /// <summary>
    /// Reads a catalog color such as "#1A2B3C" into an opaque color. The '#' is optional.
    /// Text that is not a hexadecimal number gives <see cref="White"/>, so a bad color
    /// never stops the page from drawing.
    /// </summary>
    public static Rgba FromHex(string? hex)
    {
        try
        {
            int n = Convert.ToInt32(hex!.TrimStart('#'), 16);
            return new Rgba(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f, 1);
        }
        catch
        {
            return White;
        }
    }
}
