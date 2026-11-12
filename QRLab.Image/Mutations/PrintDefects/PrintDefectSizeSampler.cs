using QRLab.Core.Models.Mutations;

namespace QRLab.Image.Mutations.PrintDefects;

internal abstract class PrintDefectSizeSampler
{
    private static readonly IReadOnlyDictionary<PrintDefectSizeDistribution, PrintDefectSizeSampler> Samplers =
        new Dictionary<PrintDefectSizeDistribution, PrintDefectSizeSampler>
        {
            [PrintDefectSizeDistribution.Fixed] = new FixedSizeSampler(),
            [PrintDefectSizeDistribution.Uniform] = new UniformSizeSampler(),
            [PrintDefectSizeDistribution.Normal] = new NormalSizeSampler()
        };

    public static PrintDefectSizeSampler For(PrintDefectSizeDistribution distribution) => Samplers[distribution];
    public abstract double Sample(Random random, double diameter, double variationPercent);
    protected static (double Minimum, double Maximum) Bounds(double diameter, double variationPercent) =>
        (Math.Max(1, diameter * (1 - variationPercent / 100)), diameter * (1 + variationPercent / 100));
}
