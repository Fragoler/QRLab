using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Recognition;


namespace QRLab.Core.Models.Samples;

public sealed class SampleState
{
    private readonly SamplePreview _source;
    public MutationChain Mutations { get; }
    public RasterImage Image => Mutations.CurrentImage(_source.Image);
    public RecognitionPreview? Result { get; private set; }
    public SamplePreview Preview => _source with { Frames = Mutations.Frames };
    public event EventHandler? Changed;

    public SampleState(SamplePreview source)
    {
        _source = source;
        Mutations = new(source.Frames);
        Mutations.Changed += (_, _) =>
        {
            Result = null;
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    internal bool TrySetResult(RecognitionPreview result, long revision)
    {
        if (revision != Mutations.Revision) return false;
        Result = result;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
