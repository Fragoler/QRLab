using QRLab.Core.Interfaces.Generation;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Samples;

namespace QRLab.Core.Services;

public sealed class GenerationService(SampleWorkspace workspace, ICodeGenerator generator)
{
    public void Generate(GenerationOptions options)
    {
        var samples = generator.CreateSamples(options);
        workspace.Replace(GenerationDefaults.NewSetName, samples);
    }
}
