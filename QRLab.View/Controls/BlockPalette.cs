using Avalonia.Media;

namespace QRLab.View.Controls;

/// <summary>Stable block colors with contrasting module shades, excluding violet alignment colors.</summary>
internal static class BlockPalette
{
    public static Color ColorFor(int block, bool dark)
    {
        // Space adjacent block hues far apart. Restrict the range to red–green–blue (0–235°).
        var hue = block * 137.50776405003785 % 235;
        var saturation = dark ? 1.0 : .7;
        var value = dark ? .4 : 1.0;
        var chroma = value * saturation;
        var secondary = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, secondary, 0.0),
            < 120 => (secondary, chroma, 0.0),
            < 180 => (0.0, chroma, secondary),
            _ => (0.0, secondary, chroma)
        };
        byte Channel(double component) => (byte)Math.Round((component + value - chroma) * 255);
        return Color.FromRgb(Channel(red), Channel(green), Channel(blue));
    }
}
