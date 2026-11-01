using QRLab.Core.Interfaces.Storage;
using QRLab.Core.Models.Samples;
using QRLab.Core.Models.Storage;


namespace QRLab.Core.Services;

public sealed class SampleStorageService(SampleWorkspace workspace, ISampleSetStorage storage)
{
    public SampleSet Load(string directory)
    {
        var set = storage.Load(directory);
        workspace.Replace(set.Name, set.Samples);
        return set;
    }

    public async Task<SampleSet> SaveAsync(ExportOptions options)
    {
        var revision = workspace.Revision;
        var samples = workspace.Samples.Select(sample => sample.Preview).ToArray();
        var set = await Task.Run(() => storage.Save(samples, options));
        if (workspace.Revision == revision) workspace.Rename(set.Name);
        return set;
    }
}
