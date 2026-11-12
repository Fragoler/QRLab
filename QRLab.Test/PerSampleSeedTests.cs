using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Mutations;
using QRLab.Core.Models.Samples;
using QRLab.Core.Services;
using QRLab.Image.Generation;
using QRLab.Image.Mutations;
using QRLab.Test.Fixtures;
using QRLab.View.ViewModels.Mutations;
using Xunit;

namespace QRLab.Test;

public sealed class PerSampleSeedTests
{
    [Theory]
    [InlineData(MutationKind.SaltAndPepper, false)]
    [InlineData(MutationKind.SaltAndPepper, true)]
    [InlineData(MutationKind.PrintDefects, false)]
    [InlineData(MutationKind.PrintDefects, true)]
    public async Task ApplyingChainUsesEachSamplesNumberAndKeepsBaseSeed(MutationKind kind, bool perSample)
    {
        var workspace = CreateWorkspace();
        var service = new MutationService(workspace, TestApplicationFactory.CreateImageMutations());
        var source = workspace.Samples[1];
        var options = Options(kind, perSample);
        service.Add(source, options);
        Assert.Equal(2, await service.ApplyToOthersAsync(source));
        foreach (var sample in workspace.Samples)
        {
            var frame = Assert.Single(sample.Mutations.Frames);
            Assert.Equal(options, frame.Options);
            var seed = 42 + (perSample ? sample.Preview.Number : 0);
            Assert.Equal(Expected(sample.Preview.Image, options, seed).Data.ToArray(), frame.Image.Data.ToArray());
        }
        var first = workspace.Samples[0].Image.Data.ToArray();
        var second = workspace.Samples[1].Image.Data.ToArray();
        Assert.Equal(!perSample, first.AsSpan().SequenceEqual(second));
        var images = workspace.Samples.Select(sample => sample.Image.Data.ToArray()).ToArray();
        service.Edit(source, 0, options);
        await service.ApplyToOthersAsync(source);
        for (var index = 0; index < workspace.Samples.Count; index++)
            Assert.Equal(images[index], workspace.Samples[index].Image.Data.ToArray());
    }

    [Fact]
    public void EditingEarlierMutationResolvesEachSeedOnceForTheWholeChain()
    {
        var workspace = CreateWorkspace();
        var service = new MutationService(workspace, TestApplicationFactory.CreateImageMutations());
        var sample = workspace.Samples[0];
        var noise = new SaltAndPepperOptions { Seed = 10, RandomForEachSample = true, DensityPercent = 15 };
        var defects = new PrintDefectsOptions { Seed = 100, RandomForEachSample = true, Count = 30 };
        service.Add(sample, noise); service.Add(sample, defects);
        noise = noise with { DensityPercent = 25 };
        service.Edit(sample, 0, noise);
        var intermediate = new SaltAndPepperMutation().Apply(sample.Preview.Image, noise with { Seed = 22, RandomForEachSample = false });
        var expected = new PrintDefectsMutation().Apply(intermediate, defects with { Seed = 112, RandomForEachSample = false });
        Assert.Equal(expected.Data.ToArray(), sample.Image.Data.ToArray());
        Assert.Equal(noise, sample.Mutations.Frames[0].Options);
        Assert.Equal(defects, sample.Mutations.Frames[1].Options);
    }

    [Theory]
    [InlineData(MutationKind.SaltAndPepper)]
    [InlineData(MutationKind.PrintDefects)]
    public async Task SavingAndReorderingSamplesRetainsBaseSeedAndRebuildsIdenticalImages(MutationKind kind)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"QRLab.PerSampleSeed-{Guid.NewGuid():N}");
        try
        {
            var workspace = CreateWorkspace();
            var service = new MutationService(workspace, TestApplicationFactory.CreateImageMutations());
            var options = Options(kind, true);
            service.Add(workspace.Samples[1], options);
            await service.ApplyToOthersAsync(workspace.Samples[1]);
            var expected = workspace.Samples.ToDictionary(sample => sample.Preview.Number, sample => sample.Image.Data.ToArray());
            var storage = TestApplicationFactory.CreateStorage();
            storage.Save(workspace.Samples.Select(sample => sample.Preview).ToArray(), new(folder));
            var loaded = storage.Load(folder);
            workspace.Replace("Переставленный набор", loaded.Samples.Reverse().ToArray());
            foreach (var sample in workspace.Samples)
            {
                var frame = Assert.Single(sample.Mutations.Frames);
                Assert.Equal(options, frame.Options);
                var editor = MutationSettingsViewModel.FromOptions(frame.Options);
                Assert.Equal(options, editor.ToOptions());
                service.Edit(sample, 0, editor.ToOptions());
                Assert.Equal(expected[sample.Preview.Number], sample.Image.Data.ToArray());
                sample.Mutations.Undo(); sample.Mutations.Redo();
                Assert.Equal(options, Assert.Single(sample.Mutations.Frames).Options);
            }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    [Theory]
    [InlineData(MutationKind.SaltAndPepper)]
    [InlineData(MutationKind.PrintDefects)]
    public void SeedResolutionDoesNotAccumulateAndHandlesIntegerBoundary(MutationKind kind)
    {
        MutationOptions options = kind == MutationKind.SaltAndPepper
            ? new SaltAndPepperOptions { Seed = int.MaxValue, RandomForEachSample = true }
            : new PrintDefectsOptions { Seed = int.MaxValue, RandomForEachSample = true };
        var resolved = options.ForSample(1);
        if (resolved is SaltAndPepperOptions noise)
        {
            Assert.Equal(int.MinValue, noise.Seed); Assert.False(noise.RandomForEachSample);
            Assert.Equal(int.MaxValue, Assert.IsType<SaltAndPepperOptions>(options).Seed);
        }
        else
        {
            var defects = Assert.IsType<PrintDefectsOptions>(resolved);
            Assert.Equal(int.MinValue, defects.Seed); Assert.False(defects.RandomForEachSample);
            Assert.Equal(int.MaxValue, Assert.IsType<PrintDefectsOptions>(options).Seed);
        }
        Assert.Same(resolved, resolved.ForSample(1));
        var blur = new GaussianBlurOptions();
        Assert.Same(blur, blur.ForSample(12));
    }

    [Fact]
    public void OlderSavedOptionsKeepFixedSeeds()
    {
        var noise = System.Text.Json.JsonSerializer.Deserialize<MutationOptions>("""{"$mutation":"salt-and-pepper","Seed":123}""");
        var defects = System.Text.Json.JsonSerializer.Deserialize<MutationOptions>("""{"$mutation":"print-defects","Seed":123}""");
        Assert.False(Assert.IsType<SaltAndPepperOptions>(noise).RandomForEachSample);
        Assert.False(Assert.IsType<PrintDefectsOptions>(defects).RandomForEachSample);
        Assert.Same(noise, noise.ForSample(12));
        Assert.Same(defects, defects.ForSample(12));
    }

    private static MutationOptions Options(MutationKind kind, bool perSample) => kind == MutationKind.SaltAndPepper
        ? new SaltAndPepperOptions { Seed = 42, RandomForEachSample = perSample, DensityPercent = 25 }
        : new PrintDefectsOptions { Seed = 42, RandomForEachSample = perSample, Count = 35, Diameter = 1 };

    private static RasterImage Expected(RasterImage image, MutationOptions options, int seed) => options switch
    {
        SaltAndPepperOptions noise => new SaltAndPepperMutation().Apply(image, noise with { Seed = seed, RandomForEachSample = false }),
        PrintDefectsOptions defects => new PrintDefectsMutation().Apply(image, defects with { Seed = seed, RandomForEachSample = false }),
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };

    private static SampleWorkspace CreateWorkspace()
    {
        var workspace = new SampleWorkspace();
        workspace.Replace("Одинаковые исходники", new CodeGenerator().CreateSamples(new QrGenerationOptions
        {
            Source = ContentSource.Text, Template = "SAME", FirstNumber = 12, Count = 3, Width = 128, Height = 128
        }));
        return workspace;
    }
}
