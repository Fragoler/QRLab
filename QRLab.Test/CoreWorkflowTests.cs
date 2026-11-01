using QRLab.Core.Interfaces.Generation;
using QRLab.Core.Interfaces.Mutations;
using QRLab.Core.Interfaces.Recognition;
using QRLab.Core.Interfaces.Storage;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using QRLab.Core.Models.Storage;
using QRLab.Core.Services;
using Xunit;

namespace QRLab.Test;

public sealed class CoreWorkflowTests
{
    [Fact]
    public async Task FailedEditPreservesFramesResultAndRedoHistory()
    {
        var workspace = Workspace();
        var sample = workspace.Samples[0];
        var mutations = new MutationService(workspace, [new CopyMutation()]);
        mutations.Add(sample, new GaussianBlurOptions { Sigma = 1 });
        mutations.Add(sample, new GaussianBlurOptions { Sigma = 2 });
        var last = sample.Image;
        sample.Mutations.Undo();
        var frames = sample.Mutations.Frames;
        var revision = sample.Mutations.Revision;
        var recognizer = new PendingRecognizer();
        var reading = new RecognitionService(workspace, recognizer);
        var run = reading.RecognizeAsync(new(), TestContext.Current.CancellationToken);
        recognizer.Complete(0);
        Assert.Equal(1, await run);
        var result = sample.Result;

        Assert.Throws<ArgumentException>(() => mutations.Edit(sample, 0, new GaussianBlurOptions { Sigma = -1 }));
        Assert.Same(frames, sample.Mutations.Frames);
        Assert.Same(result, sample.Result);
        Assert.Equal(revision, sample.Mutations.Revision);
        Assert.True(sample.Mutations.CanRedo);
        sample.Mutations.Redo();
        Assert.Same(last, sample.Image);
        Assert.Null(sample.Result);
        sample.Mutations.Reset();
        Assert.Same(sample.Preview.Image, sample.Image);
        Assert.False(sample.Mutations.CanUndo);
        Assert.False(sample.Mutations.CanRedo);
    }

    [Fact]
    public async Task RecognitionIsInvalidatedByAnEditEvenWhenImageInstanceDoesNotChange()
    {
        var workspace = Workspace();
        var sample = workspace.Samples[0];
        var recognizer = new PendingRecognizer();
        var service = new RecognitionService(workspace, recognizer);
        var run = service.RecognizeAsync(new(), TestContext.Current.CancellationToken);
        var original = sample.Image;
        sample.Mutations.Reset();
        Assert.Same(original, sample.Image);
        recognizer.Complete(0);
        Assert.Equal(0, await run);
        Assert.Null(sample.Result);
    }

    [Fact]
    public async Task RecognitionIgnoresReplacedSamplesAndOlderRuns()
    {
        var workspace = Workspace();
        var recognizer = new PendingRecognizer();
        var service = new RecognitionService(workspace, recognizer);
        var removed = workspace.Samples[0];
        var oldSetRun = service.RecognizeAsync(new(), TestContext.Current.CancellationToken);
        workspace.Replace("replacement", [Preview(1)]);
        recognizer.Complete(0);
        Assert.Equal(0, await oldSetRun);
        Assert.Null(removed.Result);

        var older = service.RecognizeAsync(new(), TestContext.Current.CancellationToken);
        var newer = service.RecognizeAsync(new(), TestContext.Current.CancellationToken);
        recognizer.Complete(2, "newer");
        Assert.Equal(1, await newer);
        recognizer.Complete(1, "older");
        Assert.Equal(0, await older);
        Assert.Equal("newer", workspace.Samples[0].Result!.DecodedContent);
    }

    [Fact]
    public async Task CancellationPreventsCommitEvenIfRecognizerReturnsResults()
    {
        var workspace = Workspace();
        var recognizer = new PendingRecognizer();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = new RecognitionService(workspace, recognizer).RecognizeAsync(new(), cancellation.Token);
        cancellation.Cancel();
        recognizer.Complete(0);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Null(workspace.Samples[0].Result);
    }

    [Fact]
    public async Task BatchDoesNotOverwriteEditedOrReplacedTargets()
    {
        var workspace = Workspace(3);
        var source = workspace.Samples[0];
        var edited = workspace.Samples[1];
        var untouched = workspace.Samples[2];
        var edit = new MutationService(workspace, [new CopyMutation()]);
        edit.Add(source, new GaussianBlurOptions { Sigma = 2 });
        var processor = new GatedMutation();
        var batch = new MutationService(workspace, [processor]).ApplyToOthersAsync(source);
        try
        {
            await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            edit.Add(edited, new GaussianBlurOptions { Sigma = 4 });
        }
        finally { processor.Release.TrySetResult(); }
        Assert.Equal(1, await batch);
        Assert.Equal(4, Assert.IsType<GaussianBlurOptions>(edited.Mutations.Frames[0].Options).Sigma);
        Assert.Equal(2, Assert.IsType<GaussianBlurOptions>(untouched.Mutations.Frames[0].Options).Sigma);

        processor = new GatedMutation();
        batch = new MutationService(workspace, [processor]).ApplyToOthersAsync(source);
        try
        {
            await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            workspace.Replace("new set", [Preview(1), Preview(2)]);
        }
        finally { processor.Release.TrySetResult(); }
        Assert.Equal(0, await batch);
        Assert.All(workspace.Samples, sample => Assert.Empty(sample.Mutations.Frames));
    }

    [Fact]
    public async Task BatchDiscardsResultsWhenSourceChanges()
    {
        var workspace = Workspace(2);
        var source = workspace.Samples[0];
        new MutationService(workspace, [new CopyMutation()]).Add(source, new GaussianBlurOptions());
        var processor = new GatedMutation();
        var batch = new MutationService(workspace, [processor]).ApplyToOthersAsync(source);
        try
        {
            await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            source.Mutations.Reset();
        }
        finally { processor.Release.TrySetResult(); }
        Assert.Equal(0, await batch);
        Assert.Empty(workspace.Samples[1].Mutations.Frames);
    }

    [Fact]
    public async Task BatchFailureDoesNotPartiallyReplaceTargets()
    {
        var workspace = Workspace(3);
        var source = workspace.Samples[0];
        new MutationService(workspace, [new CopyMutation()]).Add(source, new GaussianBlurOptions());
        var revisions = workspace.Samples.Select(sample => sample.Mutations.Revision).ToArray();
        var service = new MutationService(workspace, [new FailingMutation()]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ApplyToOthersAsync(source));
        Assert.Equal(revisions, workspace.Samples.Select(sample => sample.Mutations.Revision));
    }

    [Fact]
    public async Task SavingAnOldSnapshotDoesNotRenameAReplacementSet()
    {
        var workspace = Workspace();
        var storage = new GatedStorage();
        var save = new SampleStorageService(workspace, storage).SaveAsync(new("unused"));
        try
        {
            await storage.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            workspace.Replace("current", [Preview(2)]);
        }
        finally { storage.Release.TrySetResult(); }
        Assert.Equal("saved snapshot", (await save).Name);
        Assert.Equal("current", workspace.Name);
        Assert.Equal(2, workspace.Samples[0].Preview.Number);
    }

    [Fact]
    public void GenerationFailureKeepsWorkspaceIntact()
    {
        var workspace = Workspace();
        var samples = workspace.Samples;
        var revision = workspace.Revision;
        Assert.Throws<ArgumentException>(() => new GenerationService(workspace, new FailingGenerator()).Generate(new QrGenerationOptions()));
        Assert.Same(samples, workspace.Samples);
        Assert.Equal(revision, workspace.Revision);
        Assert.Equal("initial", workspace.Name);
    }

    private static SampleWorkspace Workspace(int count = 1)
    {
        var workspace = new SampleWorkspace();
        workspace.Replace("initial", Enumerable.Range(1, count).Select(Preview).ToArray());
        return workspace;
    }
    private static SamplePreview Preview(int number) => new(number, $"sample {number}", "payload",
        new RasterImage(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9XkAAAAASUVORK5CYII=")), [], new QrGenerationOptions());

    private class CopyMutation : IImageMutation
    {
        public MutationKind Kind => MutationKind.GaussianBlur;
        public virtual RasterImage Apply(RasterImage original, MutationOptions options)
        {
            options.Validate();
            return new RasterImage(original.Data.Span);
        }
    }
    private sealed class FailingMutation : CopyMutation
    {
        private int _calls;
        public override RasterImage Apply(RasterImage original, MutationOptions options) =>
            ++_calls == 2 ? throw new ArgumentException("processing failed") : base.Apply(original, options);
    }
    private sealed class GatedMutation : CopyMutation
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override RasterImage Apply(RasterImage original, MutationOptions options)
        {
            Started.TrySetResult();
            Release.Task.GetAwaiter().GetResult();
            return base.Apply(original, options);
        }
    }
    private sealed class PendingRecognizer : ICodeRecognizer
    {
        private readonly List<(IReadOnlyList<SamplePreview> Samples, TaskCompletionSource<IReadOnlyList<RecognitionPreview>> Completion)> _requests = [];
        public Task<IReadOnlyList<RecognitionPreview>> RecognizeAsync(IReadOnlyList<SamplePreview> samples, RecognitionOptions options, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<RecognitionPreview>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add((samples, completion));
            return completion.Task;
        }
        public void Complete(int index, string decoded = "payload")
        {
            var request = _requests[index];
            request.Completion.SetResult(request.Samples.Select(sample => new RecognitionPreview(sample.Number, Status.Success, 1, decoded)).ToArray());
        }
    }
    private sealed class GatedStorage : ISampleSetStorage
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SampleSet Save(IReadOnlyList<SamplePreview> samples, ExportOptions options)
        {
            Started.SetResult();
            Release.Task.GetAwaiter().GetResult();
            return new("saved snapshot", samples);
        }
        public SampleSet Load(string directory) => throw new NotSupportedException();
    }
    private sealed class FailingGenerator : ICodeGenerator
    {
        public IReadOnlyList<SamplePreview> CreateSamples(GenerationOptions options) => throw new ArgumentException("generation failed");
    }
}
