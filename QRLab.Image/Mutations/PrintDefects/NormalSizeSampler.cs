namespace QRLab.Image.Mutations.PrintDefects;

internal sealed class NormalSizeSampler : PrintDefectSizeSampler
{
    public override double Sample(Random random, double diameter, double variationPercent)
    {
        var (minimum, maximum) = Bounds(diameter, variationPercent);
        // Box–Muller; the configured range spans three standard deviations on either side.
        var normal = Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        return Math.Clamp(diameter + normal * diameter * variationPercent / 300, minimum, maximum);
    }
}
