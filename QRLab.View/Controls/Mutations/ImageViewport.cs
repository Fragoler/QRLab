using Avalonia;

namespace QRLab.View.Controls.Mutations;

/// <summary>
/// Maps image pixels to the panned and zoomed preview rectangle
/// </summary>
public readonly record struct ImageViewport(Size ImageSize, Rect Bounds)
{
    public double Scale => Bounds.Width / ImageSize.Width;
    public Point ToImage(Point point) => new((point.X - Bounds.X) / Scale, (point.Y - Bounds.Y) / Scale);
    public Point ToScreen(Point point) => new(Bounds.X + point.X * Scale, Bounds.Y + point.Y * Scale);
    public Point FromPercent(double x, double y) => new(x * ImageSize.Width / 100, y * ImageSize.Height / 100);
    public Point ToPercent(Point point) => new(Math.Clamp(point.X / ImageSize.Width * 100, 0, 100),
        Math.Clamp(point.Y / ImageSize.Height * 100, 0, 100));
}
