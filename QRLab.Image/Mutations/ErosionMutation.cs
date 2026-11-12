using QRLab.Core.Models.Mutations;

namespace QRLab.Image.Mutations;

public sealed class ErosionMutation : MorphologyMutation<ErosionOptions>
{
    public override MutationKind Kind => MutationKind.Erosion;
    protected override bool Prefer(byte candidate, byte current) => candidate >= current;
}
