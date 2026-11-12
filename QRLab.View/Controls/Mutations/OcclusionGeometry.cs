using Avalonia;
using QRLab.Core.Models.Mutations;

namespace QRLab.View.Controls.Mutations;

/// <summary>Rotated selection geometry, measured in image pixels rather than screen pixels.</summary>
public sealed class OcclusionGeometry(OcclusionOptions options, Size imageSize)
{
    private static readonly (int X, int Y)[] Signs = [(-1, -1), (1, -1), (1, 1), (-1, 1)];
    public Point Center => new(imageSize.Width * options.CenterX / 100, imageSize.Height * options.CenterY / 100);
    public double Width => imageSize.Width * options.Width / 100;
    public double Height => imageSize.Height * options.Height / 100;
    public Point[] Corners => Signs.Select(sign => Center + Rotate(new Vector(sign.X * Width / 2, sign.Y * Height / 2), options.Angle)).ToArray();

    public bool Contains(Point point)
    {
        var local = Rotate(point - Center, -options.Angle);
        return options.Shape == OcclusionShape.Ellipse
            ? Width > 0 && Height > 0 && Math.Pow(2 * local.X / Width, 2) + Math.Pow(2 * local.Y / Height, 2) <= 1
            : Math.Abs(local.X) <= Width / 2 && Math.Abs(local.Y) <= Height / 2;
    }

    public OcclusionOptions Move(Vector delta) => options with
    {
        CenterX = Math.Clamp((Center.X + delta.X) / imageSize.Width * 100, 0, 100),
        CenterY = Math.Clamp((Center.Y + delta.Y) / imageSize.Height * 100, 0, 100)
    };

    public OcclusionOptions Resize(int corner, Vector delta)
    {
        var sign = Signs[corner];
        var anchor = Corners[(corner + 2) % 4];
        var dragged = Corners[corner] + delta;
        var local = Rotate(dragged - anchor, -options.Angle);
        var width = Math.Clamp(sign.X * local.X, 1, imageSize.Width);
        var height = Math.Clamp(sign.Y * local.Y, 1, imageSize.Height);
        var center = anchor + Rotate(new Vector(sign.X * width / 2, sign.Y * height / 2), options.Angle);
        return options with
        {
            CenterX = Math.Clamp(center.X / imageSize.Width * 100, 0, 100),
            CenterY = Math.Clamp(center.Y / imageSize.Height * 100, 0, 100),
            Width = width / imageSize.Width * 100, Height = height / imageSize.Height * 100
        };
    }

    private static Vector Rotate(Vector vector, double angle)
    {
        var radians = angle * Math.PI / 180;
        return new(vector.X * Math.Cos(radians) - vector.Y * Math.Sin(radians),
            vector.X * Math.Sin(radians) + vector.Y * Math.Cos(radians));
    }
}
