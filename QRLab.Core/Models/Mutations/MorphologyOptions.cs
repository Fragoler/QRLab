namespace QRLab.Core.Models.Mutations;

/// <summary>Rectangular morphology applied to dark foreground, independently to each color channel.</summary>
public abstract record MorphologyOptions : MutationOptions
{
    public const int MaximumRadius = 50;
    public const int MaximumIterations = 10;
    public int RadiusX { get; init; } = 1;
    public int RadiusY { get; init; } = 1;
    public int Iterations { get; init; } = 1;

    public sealed override void Validate()
    {
        if (RadiusX is < 0 or > MaximumRadius || RadiusY is < 0 or > MaximumRadius)
            throw new ArgumentException($"Радиусы морфологии должны быть от 0 до {MaximumRadius} пикселей.");
        if (Iterations is < 1 or > MaximumIterations)
            throw new ArgumentException($"Число повторений должно быть от 1 до {MaximumIterations}.");
    }
}
