using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Samples;

namespace QRLab.Core.Interfaces.Generation;

public interface ICodeGenerator
{
    IReadOnlyList<SamplePreview> CreateSamples(GenerationOptions options);
}
