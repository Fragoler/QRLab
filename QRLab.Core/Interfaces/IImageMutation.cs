using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;

namespace QRLab.Core.Interfaces.Mutations;

public interface IImageMutation
{
    MutationKind Kind { get; }
    RasterImage Apply(RasterImage image, MutationOptions options);
}