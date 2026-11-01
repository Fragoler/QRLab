using QRLab.Core.Interfaces.Recognition;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;

namespace QRLab.Core.Services;

public sealed class RecognitionService(SampleWorkspace workspace, ICodeRecognizer recognizer)
{
    private long _run;

    public async Task<int> RecognizeAsync(RecognitionOptions options, CancellationToken cancellationToken = default)
    {
        var run = Interlocked.Increment(ref _run);
        var snapshot = workspace.Samples.Select(sample =>
            (Sample: sample, Revision: sample.Mutations.Revision, Preview: sample.Preview)).ToArray();
        var results = await recognizer.RecognizeAsync(snapshot.Select(item => item.Preview).ToArray(), options, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (run != Volatile.Read(ref _run)) return 0;
        var byNumber = snapshot.ToDictionary(item => item.Preview.Number);
        var applied = 0;
        foreach (var result in results)
        {
            if (byNumber.TryGetValue(result.Number, out var item) && workspace.Samples.Contains(item.Sample)
                && item.Sample.TrySetResult(result, item.Revision)) applied++;
        }
        return applied;
    }
}
