using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using QRLab.Core.Models.Mutations;
using QRLab.View.ViewModels.Mutations;

namespace QRLab.View.Controls.Mutations;

public sealed class OcclusionTool(OcclusionSettingsViewModel settings) : IMutationTool
{
    private static readonly IBrush Outline = new SolidColorBrush(Color.Parse("#75D9BE"));
    private OcclusionOptions? _startOptions;
    private OcclusionGeometry? _startGeometry;
    private Point _startPosition;
    private int _corner;

    private OcclusionOptions? Options()
    {
        var options = (OcclusionOptions)settings.ToOptions();
        try { options.Validate(); return options; }
        catch (ArgumentException) { return null; }
    }

    private static int HitCorner(Point position, OcclusionGeometry geometry, ImageViewport viewport) =>
        Array.FindIndex(geometry.Corners, corner =>
        {
            var point = viewport.ToScreen(corner);
            return Math.Pow(point.X - position.X, 2) + Math.Pow(point.Y - position.Y, 2) <= 100;
        });

    public bool Begin(Point position, ImageViewport viewport)
    {
        if (Options() is not { } options) return false;
        var geometry = new OcclusionGeometry(options, viewport.ImageSize);
        _corner = HitCorner(position, geometry, viewport);
        if (_corner < 0 && !geometry.Contains(viewport.ToImage(position))) return false;
        _startOptions = options;
        _startGeometry = geometry;
        _startPosition = viewport.ToImage(position);
        return true;
    }

    public void Move(Point position, ImageViewport viewport)
    {
        if (_startGeometry is null) return;
        Vector delta = viewport.ToImage(position) - _startPosition;
        var options = _corner < 0 ? _startGeometry.Move(delta) : _startGeometry.Resize(_corner, delta);
        Apply(options);
    }

    private void Apply(OcclusionOptions options) => settings.SetGeometry(options.CenterX, options.CenterY, options.Width, options.Height);

    public void Complete(Point position, ImageViewport viewport)
    {
        Move(position, viewport);
        Cancel(false);
    }

    public void Cancel(bool restore)
    {
        if (restore && _startOptions is { } options) Apply(options);
        _startOptions = null;
        _startGeometry = null;
    }

    public StandardCursorType GetCursor(Point position, ImageViewport viewport)
    {
        if (Options() is not { } options) return StandardCursorType.Arrow;
        
        var geometry = new OcclusionGeometry(options, viewport.ImageSize);
        var corner = HitCorner(position, geometry, viewport);
        if (corner >= 0)
        {
            var vector = geometry.Corners[corner] - geometry.Center;
            return vector.X * vector.Y >= 0 ? StandardCursorType.TopLeftCorner : StandardCursorType.TopRightCorner;
        }
        return geometry.Contains(viewport.ToImage(position)) ? StandardCursorType.SizeAll : StandardCursorType.Arrow;
    }

    public void Render(DrawingContext context, ImageViewport viewport)
    {
        if (Options() is not { } options) return;
        
        var corners = new OcclusionGeometry(options, viewport.ImageSize).Corners.Select(viewport.ToScreen).ToArray();
        var geometry = new StreamGeometry();
        using (var builder = geometry.Open())
        {
            builder.BeginFigure(corners[0], false);
            foreach (var corner in corners.Skip(1)) builder.LineTo(corner);
            builder.EndFigure(true);
        }

        foreach (var point in corners)
            context.DrawRectangle(Brushes.White, new Pen(Outline, 2), new Rect(point.X - 4, point.Y - 4, 8, 8));
    }
}
