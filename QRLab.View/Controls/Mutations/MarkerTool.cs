using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using QRLab.Core.Models.Mutations;
using QRLab.View.ViewModels.Mutations;

namespace QRLab.View.Controls.Mutations;

public interface IMutationTool
{
    bool Begin(Point position, ImageViewport viewport);
    void Move(Point position, ImageViewport viewport);
    void Complete(Point position, ImageViewport viewport);
    void Cancel(bool restore);
    StandardCursorType GetCursor(Point position, ImageViewport viewport);
    void Render(DrawingContext context, ImageViewport viewport);
}

public sealed class MarkerTool(MarkerSettingsViewModel settings) : IMutationTool
{
    private readonly List<MarkerPoint> _points = [];

    public bool Begin(Point position, ImageViewport viewport)
    {
        if (!viewport.Bounds.Contains(position)) return false;
        try { settings.ToOptions().Validate(); }
        catch (ArgumentException) { return false; }
        _points.Clear();
        Move(position, viewport);
        return true;
    }

    public void Move(Point position, ImageViewport viewport)
    {
        var percent = viewport.ToPercent(viewport.ToImage(position));
        var point = new MarkerPoint(percent.X, percent.Y);
        if (_points.Count == 0 || _points[^1] != point) _points.Add(point);
    }

    public void Complete(Point position, ImageViewport viewport)
    {
        Move(position, viewport);
        settings.AddStroke(_points);
        _points.Clear();
    }

    public void Cancel(bool restore) => _points.Clear();

    public StandardCursorType GetCursor(Point position, ImageViewport viewport) => StandardCursorType.Cross;

    public void Render(DrawingContext context, ImageViewport viewport)
    {
        if (_points.Count == 0 || !Color.TryParse("#" + settings.Color.TrimStart('#'), out var color)) return;
        var points = _points.Select(point => viewport.ToScreen(viewport.FromPercent(point.X, point.Y))).ToArray();
        var brush = new SolidColorBrush(color);
        var thickness = settings.Thickness * viewport.Scale;
        using var clip = context.PushClip(viewport.Bounds);
        if (points.Length == 1) context.DrawEllipse(brush, null, points[0], thickness / 2, thickness / 2);
        else
        {
            var geometry = new StreamGeometry();
            using (var builder = geometry.Open())
            {
                builder.BeginFigure(points[0], false);
                foreach (var point in points.Skip(1)) builder.LineTo(point);
                builder.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
        }
    }
}
