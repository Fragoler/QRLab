using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;

namespace QRLab.Core.Interfaces.Mutations;

public interface IMutationProcessor
{
    IReadOnlyList<MutationFrame> Apply(RasterImage original, IReadOnlyList<MutationOptions> operations);
}