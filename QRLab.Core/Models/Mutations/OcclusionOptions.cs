namespace QRLab.Core.Models.Mutations;

public enum OcclusionShape { Rectangle, Ellipse }

public sealed record OcclusionOptions : MutationOptions
{
    public override MutationKind Kind => MutationKind.Occlusion;
    public OcclusionShape Shape { get; init; } = OcclusionShape.Rectangle;
    public string Color { get; init; } = "#808080";
    public double CenterX { get; init; } = 50;
    public double CenterY { get; init; } = 50;
    public double Width { get; init; } = 25;
    public double Height { get; init; } = 25;
    public double Angle { get; init; }

    public override void Validate()
    {
        if (!Enum.IsDefined(Shape) || !ValidPercent(CenterX) || !ValidPercent(CenterY)
            || !ValidPercent(Width) || !ValidPercent(Height) || !double.IsFinite(Angle) || Math.Abs(Angle) > 180)
            throw new ArgumentException("Положение и размеры области должны быть от 0 до 100%, угол — от −180 до 180°.");
        var hex = Color?.StartsWith('#') == true ? Color[1..] : Color;
        if (hex is not { Length: 6 } || !hex.All(char.IsAsciiHexDigit))
            throw new ArgumentException("Укажите цвет области в формате #RRGGBB, например #808080.");
    }

    private static bool ValidPercent(double value) => double.IsFinite(value) && value is >= 0 and <= 100;
}
