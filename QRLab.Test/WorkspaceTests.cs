using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Mutations;
using QRLab.Test.Fixtures;
using Xunit;

namespace QRLab.Test;

public sealed class WorkspaceTests
{
    [Fact]
    public void GenerationKeepsFormsAcrossNavigation()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 12; vm.Generation.Settings.FirstNumber = 20; vm.Generation.Settings.Template = "TEST-{num}";
        vm.Generation.GenerateCommand.Execute(null);
        Assert.Equal(12, vm.Workspace.Samples.Count);
        Assert.Equal("TEST-0020", vm.Workspace.SelectedSample!.Content);
        vm.NavigateCommand.Execute("02"); vm.NavigateCommand.Execute("01");
        Assert.Equal(12, vm.Generation.Settings.Count);
        vm.Generation.Settings.Mode = GenerationMode.Single; vm.Generation.GenerateCommand.Execute(null);
        Assert.Single(vm.Workspace.Samples);
    }

    [Fact]
    public async Task MutationHistorySupportsUndoRedoResetAndBatchApplication()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 2; vm.Generation.Settings.ImageWidth = 256; vm.Generation.Settings.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        var editor = vm.Mutations;
        var source = vm.Workspace.SelectedSample!.OriginalImage;
        editor.AddCommand.Execute(MutationKind.GaussianBlur);
        var changed = vm.Workspace.SelectedSample.Image;
        Assert.NotSame(source, changed);
        editor.UndoCommand.Execute(null); Assert.Same(source, vm.Workspace.SelectedSample.Image);
        editor.RedoCommand.Execute(null); Assert.Same(changed, vm.Workspace.SelectedSample.Image);
        await editor.ApplyCommand.ExecuteAsync(null);
        Assert.All(vm.Workspace.Samples, s => Assert.Single(s.Operations));
        editor.ResetCommand.Execute(null);
        Assert.Same(source, vm.Workspace.SelectedSample.Image);
        Assert.False(editor.CanUndo); Assert.False(editor.CanRedo);
        Assert.All(vm.Workspace.Samples.Where(s => s != vm.Workspace.SelectedSample), s => Assert.Single(s.Operations));
    }

}
