using QRLab.Core.Interfaces.Mutations;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Samples;

namespace QRLab.Core.Services;

public sealed class MutationService(SampleWorkspace workspace, IEnumerable<IImageMutation> mutations)
{
    private readonly IReadOnlyDictionary<MutationKind, IImageMutation> _mutations = mutations.ToDictionary(mutation => mutation.Kind);
    private long _batch;

    public IReadOnlyList<MutationFrame> Apply(RasterImage original, IReadOnlyList<MutationOptions> operations) =>
        Apply(original, operations, 0);

    public IReadOnlyList<MutationFrame> Apply(RasterImage original, IReadOnlyList<MutationOptions> operations, int sampleNumber)
    {
        var frames = new List<MutationFrame>(operations.Count);
        var image = original;
        foreach (var options in operations)
        {
            if (!_mutations.TryGetValue(options.Kind, out var mutation))
                throw new ArgumentException("Реализация мутации не зарегистрирована.");
            image = mutation.Apply(image, options.ForSample(sampleNumber));
            // Store base options so editing or reapplying never adds the sample number twice.
            frames.Add(new(options, image));
        }
        return frames;
    }

    public void Add(SampleState sample, MutationOptions options)
    {
        var frame = Apply(sample.Image, [options], sample.Preview.Number).Single();
        sample.Mutations.Replace(sample.Mutations.Frames.Append(frame).ToArray());
    }

    public void Edit(SampleState sample, int index, MutationOptions options)
    {
        var chain = sample.Mutations.Frames.Select(frame => frame.Options).ToArray();
        chain[index] = options;
        var frames = Apply(sample.Preview.Image, chain, sample.Preview.Number);
        sample.Mutations.Replace(frames);
    }

    public async Task<int> ApplyToOthersAsync(SampleState source)
    {
        var batch = Interlocked.Increment(ref _batch);
        var sourceRevision = source.Mutations.Revision;
        var chain = source.Mutations.Frames.Select(frame => frame.Options).ToArray();
        var targets = workspace.Samples.Where(sample => sample != source)
            .Select(sample => (Sample: sample, Revision: sample.Mutations.Revision, Original: sample.Preview.Image, Number: sample.Preview.Number)).ToArray();
        var results = await Task.Run(() => targets.Select(target =>
            (target.Sample, target.Revision, Frames: Apply(target.Original, chain, target.Number))).ToArray());
        if (batch != Volatile.Read(ref _batch) || !workspace.Samples.Contains(source)
            || source.Mutations.Revision != sourceRevision) return 0;
        var applied = 0;
        foreach (var result in results)
        {
            if (!workspace.Samples.Contains(result.Sample) || result.Sample.Mutations.Revision != result.Revision) continue;
            result.Sample.Mutations.Replace(result.Frames);
            applied++;
        }
        return applied;
    }
}
