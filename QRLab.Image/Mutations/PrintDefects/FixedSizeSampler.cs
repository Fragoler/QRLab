namespace QRLab.Image.Mutations.PrintDefects;

internal sealed class FixedSizeSampler : PrintDefectSizeSampler
{
    public override double Sample(Random random, double diameter, double variationPercent) => diameter;
}
