namespace QRLab.Core.Models.Mutations;

/// <summary>Coordinates in percent of the image, so strokes can be applied to other sample sizes.</summary>
public sealed record MarkerPoint(double X, double Y);

public sealed record MarkerStroke(IReadOnlyList<MarkerPoint> Points);

public sealed record MarkerOptions : MutationOptions
{
    public override MutationKind Kind => MutationKind.Marker;
    public string Color { get; init; } = "#FF0000";
    public double Thickness { get; init; } = 8;
    public IReadOnlyList<MarkerStroke> Strokes { get; init; } = [];

    public override void Validate()
    {
        var hex = Color?.StartsWith('#') == true ? Color[1..] : Color;
        if (hex is not { Length: 6 } || !hex.All(char.IsAsciiHexDigit))
            throw new ArgumentException("Укажите цвет маркера в формате #RRGGBB, например #FF0000.");
        if (!double.IsFinite(Thickness) || Thickness is < 0.1 or > 256)
            throw new ArgumentException("Толщина маркера должна быть от 0,1 до 256 пикселей.");
        if (Strokes is null || Strokes.Any(stroke => stroke?.Points is not { Count: > 0 }
            || stroke.Points.Any(point => point is null || !double.IsFinite(point.X) || !double.IsFinite(point.Y)
                || point.X is < 0 or > 100 || point.Y is < 0 or > 100)))
            throw new ArgumentException("Штрихи маркера должны содержать точки внутри изображения.");
    }
}
