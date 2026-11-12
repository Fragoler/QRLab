using QRLab.Core.Models.Mutations;

namespace QRLab.Image.Mutations;

public sealed class DilationMutation : MorphologyMutation<DilationOptions>
{
    public override MutationKind Kind => MutationKind.Dilation;
    protected override bool Prefer(byte candidate, byte current) => candidate <= current;
}
