namespace QRLab.Image.Mutations.PrintDefects;

internal sealed class UniformSizeSampler : PrintDefectSizeSampler
{
    public override double Sample(Random random, double diameter, double variationPercent)
    {
        var (minimum, maximum) = Bounds(diameter, variationPercent);
        return minimum + random.NextDouble() * (maximum - minimum);
    }
}
