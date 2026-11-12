using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Mutations;
using QRLab.Test.Fixtures;
using QRLab.View.Controls;
using QRLab.View.Controls.Mutations;
using QRLab.View.ViewModels.Mutations;
using QRLab.View.Views;
using Xunit;

namespace QRLab.Test;

public sealed class MutationCanvasTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(35)]
    [InlineData(90)]
    [InlineData(-45)]
    [InlineData(180)]
    public void AllCornersResizeAroundOppositeAnchorAtAnyRotation(double angle)
    {
        var size = new Size(400, 240);
        var options = new OcclusionOptions { Width = 30, Height = 30, Angle = angle };
        var geometry = new OcclusionGeometry(options, size);
        var signs = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) };
        for (var corner = 0; corner < 4; corner++)
        {
            var (x, y) = signs[corner];
            var radians = angle * Math.PI / 180;
            var delta = new Vector(x * 24 * Math.Cos(radians) - y * 16 * Math.Sin(radians),
                x * 24 * Math.Sin(radians) + y * 16 * Math.Cos(radians));
            var resized = geometry.Resize(corner, delta);
            resized.Validate();
            var after = new OcclusionGeometry(resized, size);
            Assert.Equal(144, after.Width, 5);
            Assert.Equal(88, after.Height, 5);
            Assert.Equal(geometry.Corners[(corner + 2) % 4].X, after.Corners[(corner + 2) % 4].X, 5);
            Assert.Equal(geometry.Corners[(corner + 2) % 4].Y, after.Corners[(corner + 2) % 4].Y, 5);
        }
    }

    [AvaloniaFact]
    public void DraggingOcclusionAndItsCornerUpdatesSettingsAndFollowingFrame()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Mode = GenerationMode.Single;
        vm.Generation.Settings.ImageWidth = 400;
        vm.Generation.Settings.ImageHeight = 240;
        vm.Generation.GenerateCommand.Execute(null);
        vm.Mutations.AddCommand.Execute(MutationKind.Occlusion);
        var first = vm.Mutations.SelectedOperation;
        vm.Mutations.AddCommand.Execute(MutationKind.GaussianBlur);
        var oldTail = vm.Mutations.Operations[1].Image;
        vm.Mutations.SelectedOperation = first;
        var area = Assert.IsType<OcclusionSettingsViewModel>(vm.Mutations.SelectedSettings);
        area.Shape = OcclusionShape.Ellipse;
        area.Angle = 30;
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("02"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var preview = window.GetVisualDescendants().OfType<MutationCanvas>().Single();
            var viewport = new ImageViewport(new Size(400, 240), preview.ImageBounds);
            Point Screen(Point local) => preview.TranslatePoint(local, window)!.Value;
            var center = Screen(viewport.ToScreen(new Point(200, 120)));
            var delta = new Vector(20, 12);
            window.MouseDown(center, MouseButton.Left); window.MouseMove(center + delta); window.MouseUp(center + delta, MouseButton.Left);
            Assert.Equal(50 + 20 / viewport.Scale / 400 * 100, area.CenterX, 4);
            Assert.Equal(50 + 12 / viewport.Scale / 240 * 100, area.CenterY, 4);
            Assert.Equal(default(Vector), preview.PanOffset);
            Assert.NotSame(oldTail, vm.Mutations.Operations[1].Image);
            var before = (OcclusionOptions)area.ToOptions();
            var geometry = new OcclusionGeometry(before, new Size(400, 240));
            var corner = Screen(viewport.ToScreen(geometry.Corners[2]));
            window.MouseDown(corner, MouseButton.Left); window.MouseMove(corner + new Vector(15, 20)); window.MouseUp(corner + new Vector(15, 20), MouseButton.Left);
            Assert.True(area.Width > before.Width);
            Assert.True(area.Height > before.Height);
            Assert.Equal(30, area.Angle);
            var after = new OcclusionGeometry((OcclusionOptions)area.ToOptions(), new Size(400, 240));
            Assert.Equal(geometry.Corners[0].X, after.Corners[0].X, 4);
            Assert.Equal(geometry.Corners[0].Y, after.Corners[0].Y, 4);
            Assert.False(vm.Workspace.HasError);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MarkerDrawsInImageCoordinatesAfterRightButtonPanAndZoom()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Mode = GenerationMode.Single;
        vm.Generation.GenerateCommand.Execute(null);
        vm.Mutations.AddCommand.Execute(MutationKind.Marker);
        var marker = Assert.IsType<MarkerSettingsViewModel>(vm.Mutations.SelectedSettings);
        var source = vm.Workspace.SelectedSample!.OriginalImage;
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("02"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var preview = window.GetVisualDescendants().OfType<MutationCanvas>().Single();
            var margin = new[]
            {
                new Point(2, preview.Bounds.Height / 2), new Point(preview.Bounds.Width - 2, preview.Bounds.Height / 2),
                new Point(preview.Bounds.Width / 2, 2), new Point(preview.Bounds.Width / 2, preview.Bounds.Height - 2)
            }.First(point => !preview.ImageBounds.Contains(point));
            var middle = preview.TranslatePoint(margin, window)!.Value;
            var pan = new Vector(25, 15);
            window.MouseDown(middle, MouseButton.Left); window.MouseMove(middle + pan); window.MouseUp(middle + pan, MouseButton.Left);
            Assert.Equal(pan, preview.PanOffset);
            Assert.Empty(marker.Strokes);
            window.MouseWheel(middle, new Vector(0, 2));
            Assert.True(preview.Zoom > 1);
            var bounds = preview.ImageBounds;
            Point Position(double x, double y) => preview.TranslatePoint(new Point(bounds.X + x * bounds.Width, bounds.Y + y * bounds.Height), window)!.Value;
            var start = Position(.3, .3);
            var bend = Position(.6, .3);
            var end = Position(.6, .6);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(bend);
            Assert.Empty(marker.Strokes);
            window.MouseMove(end);
            window.MouseUp(end, MouseButton.Left);
            var points = Assert.Single(marker.Strokes).Points;
            Assert.Equal(30, points[0].X, 4); Assert.Equal(30, points[0].Y, 4);
            Assert.Equal(60, points[^1].X, 4); Assert.Equal(60, points[^1].Y, 4);
            Assert.NotSame(source, vm.Mutations.PreviewImage);
            Assert.Equal(pan, preview.PanOffset);
            window.MouseDown(Position(.4, .4), MouseButton.Left);
            window.MouseMove(Position(.5, .5));
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.MouseUp(Position(.5, .5), MouseButton.Left);
            Assert.Single(marker.Strokes);
        }
        finally { window.Close(); }
    }
}
