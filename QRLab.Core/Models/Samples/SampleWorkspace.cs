using QRLab.Core.Models.Generation;


namespace QRLab.Core.Models.Samples;

public sealed class SampleWorkspace
{
    public IReadOnlyList<SampleState> Samples { get; private set; } = Array.Empty<SampleState>();
    public string Name { get; private set; } = GenerationDefaults.NewSetName;
    public long Revision { get; private set; }
    public event EventHandler? Changed;

    public void Replace(string name, IReadOnlyList<SamplePreview> samples)
    {
        var replacement = Array.AsReadOnly(samples.Select(sample => new SampleState(sample)).ToArray());
        Name = name;
        Samples = replacement;
        Revision++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Rename(string name)
    {
        Name = name;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
