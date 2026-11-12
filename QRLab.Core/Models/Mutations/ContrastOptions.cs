namespace QRLab.Core.Models.Mutations;

public sealed record ContrastOptions : MutationOptions
{
    public override MutationKind Kind => MutationKind.Contrast;
    public double Reduction { get; init; } = MutationDefaults.ContrastReduction;
    public double Center { get; init; } = MutationDefaults.ContrastCenter;

    public override void Validate()
    {
        if (!double.IsFinite(Reduction) || Reduction is < 0 or > 1)
            throw new ArgumentException("Коэффициент снижения контраста должны быть от 0 до 1.");
        
        if (!double.IsFinite(Center) || Center is < 0 or > 1)
            throw new ArgumentException("Центр должны быть от 0 до 1.");
    }
}
