using QRLab.Core.Models.Samples;
using QRLab.Core.Models.Storage;

namespace QRLab.Core.Interfaces.Storage;

public interface ISampleSetStorage
{
    SampleSet Save(IReadOnlyList<SamplePreview> samples, ExportOptions options);
    SampleSet Load(string directory);
}
