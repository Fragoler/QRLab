namespace QRLab.Core.Models.Mutations;

public sealed record DilationOptions : MorphologyOptions
{
    public override MutationKind Kind => MutationKind.Dilation;
}
