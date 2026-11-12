namespace QRLab.Core.Models.Mutations;

public enum PrintDefectSizeDistribution { Fixed, Uniform, Normal }

public sealed record PrintDefectsOptions : MutationOptions
{
    public const int MaximumCount = 10000;
    public const double MaximumDiameter = 256;
    public override MutationKind Kind => MutationKind.PrintDefects;
    public int Count { get; init; } = 50;
    public double Diameter { get; init; } = 3;
    public PrintDefectSizeDistribution Distribution { get; init; } = PrintDefectSizeDistribution.Uniform;
    public double SizeVariationPercent { get; init; } = 50;
    public int Seed { get; init; } = MutationDefaults.Seed;
    public bool RandomForEachSample { get; init; }

    public override MutationOptions ForSample(int sampleNumber) => RandomForEachSample
        ? this with { Seed = unchecked(Seed + sampleNumber), RandomForEachSample = false }
        : this;

    public override void Validate()
    {
        if (Count is < 0 or > MaximumCount)
            throw new ArgumentException($"Количество дефектов должно быть от 0 до {MaximumCount}.");
        if (!double.IsFinite(Diameter) || Diameter is < 1 or > MaximumDiameter)
            throw new ArgumentException($"Диаметр дефектов должен быть от 1 до {MaximumDiameter} пикселей.");
        if (!Enum.IsDefined(Distribution)) throw new ArgumentException("Неизвестное распределение размеров дефектов.");
        if (!double.IsFinite(SizeVariationPercent) || SizeVariationPercent is < 0 or > 100)
            throw new ArgumentException("Разброс размеров должен быть от 0 до 100%.");
    }
}
