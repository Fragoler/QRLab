namespace QRLab.Core.Models.Mutations;

public sealed record ErosionOptions : MorphologyOptions
{
    public override MutationKind Kind => MutationKind.Erosion;
}
