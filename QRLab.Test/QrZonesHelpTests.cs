using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using QRLab.Core.Models.Generation;
using QRLab.Test.Fixtures;
using QRLab.View.Controls;
using QRLab.View.Views;
using QRLab.View.Views.Help;
using Xunit;

namespace QRLab.Test;

public sealed class QrZonesHelpTests
{
    [AvaloniaFact]
    public async Task HoldShowsProgressThenFormattedOverlayAndEscapeRestoresShortClick()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Mode = GenerationMode.Single;
        vm.Generation.GenerateCommand.Execute(null);
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("04"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var button = window.GetVisualDescendants().OfType<HoldButton>().Single();
            button.HoldDuration = TimeSpan.FromMilliseconds(500);
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            await Task.Delay(100); Dispatcher.UIThread.RunJobs();
            Assert.InRange(button.HoldProgress, .01, .99);
            Assert.False(vm.Comparison.IsQrHelpOpen);
            var fill = button.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "QrLegendHoldFill");
            Assert.Equal(button.HoldProgress, Assert.IsType<ScaleTransform>(fill.RenderTransform).ScaleY);
            Assert.Equal(new RelativePoint(.5, 1, RelativeUnit.Relative), fill.RenderTransformOrigin);
            await Task.Delay(550); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.True(vm.Comparison.IsQrHelpOpen);
            var overlay = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "QrZonesHelpOverlay");
            Assert.True(overlay.IsEffectivelyVisible);
            Assert.False(ToolTip.GetIsOpen(button));
            Assert.False(ToolTip.GetServiceEnabled(button));
            var help = Assert.Single(overlay.GetVisualDescendants().OfType<QrZonesHelpView>());
            var headings = help.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("zoneTitle")).ToArray();
            Assert.Equal(new[]
            {
                "Поисковые узоры", "Разделители", "Метки времени", "Информация формата",
                "Информация версии (только версии 7+)", "Выравнивающие узоры", "Фиксированный модуль", "Оставшиеся биты"
            }, headings.Select(block => block.Text));
            Assert.Contains(help.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.Inlines?.OfType<Run>().Any(run => run.FontWeight == FontWeight.SemiBold) == true);
            var scroll = overlay.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            Assert.False(button.IsEffectivelyEnabled);
            var flyout = Assert.IsType<Flyout>(button.Flyout);
            Assert.False(flyout.IsOpen);
            window.MouseUp(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Assert.True(vm.Comparison.IsQrHelpOpen); Assert.False(flyout.IsOpen);
            if (Environment.GetEnvironmentVariable("QRLAB_QR_HELP_PREVIEW") is { } directory)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new RenderTargetBitmap(new PixelSize(1440, 940), new Vector(96, 96));
                bitmap.Render(window);
                using var output = File.Create(Path.Combine(directory, "qr-zones-help.png"));
                bitmap.Save(output, PngBitmapEncoderOptions.Default);
            }
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.False(vm.Comparison.IsQrHelpOpen);
            Assert.Equal(0, button.HoldProgress);
            Assert.True(button.IsFocused);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(flyout.IsOpen); Assert.False(vm.Comparison.IsQrHelpOpen);
            flyout.Hide();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task LeavingButtonCancelsHoldAndRightButtonDoesNotOpenHelp()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("04"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var button = window.GetVisualDescendants().OfType<HoldButton>().Single();
            button.HoldDuration = TimeSpan.FromMilliseconds(250);
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            await Task.Delay(70); Dispatcher.UIThread.RunJobs();
            Assert.True(button.HoldProgress > 0);
            var outside = point + new Vector(-60, 0);
            window.MouseMove(outside);
            await Task.Delay(300); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, button.HoldProgress); Assert.False(vm.Comparison.IsQrHelpOpen);
            window.MouseUp(outside, MouseButton.Left);
            window.MouseMove(point); window.MouseDown(point, MouseButton.Right);
            await Task.Delay(300); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, button.HoldProgress); Assert.False(vm.Comparison.IsQrHelpOpen);
            window.MouseUp(point, MouseButton.Right);
            Assert.False(Assert.IsType<Flyout>(button.Flyout).IsOpen);
            window.MouseDown(point, MouseButton.Left);
            await Task.Delay(70); Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            await Task.Delay(300); Dispatcher.UIThread.RunJobs();
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(0, button.HoldProgress); Assert.False(vm.Comparison.IsQrHelpOpen);
            Assert.False(Assert.IsType<Flyout>(button.Flyout).IsOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task HelpClosesWithCloseButtonAndBackgroundAndPendingHoldStopsOnNavigation()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("04"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            vm.Comparison.OpenQrHelpCommand.Execute(null);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var close = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CloseQrHelpButton");
            var closePoint = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window)!.Value;
            window.MouseDown(closePoint, MouseButton.Left); window.MouseUp(closePoint, MouseButton.Left);
            Assert.False(vm.Comparison.IsQrHelpOpen);
            vm.Comparison.OpenQrHelpCommand.Execute(null);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var overlay = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "QrZonesHelpOverlay");
            var background = overlay.TranslatePoint(new Point(4, 4), window)!.Value;
            window.MouseDown(background, MouseButton.Left); window.MouseUp(background, MouseButton.Left);
            Assert.False(vm.Comparison.IsQrHelpOpen);
            var button = window.GetVisualDescendants().OfType<HoldButton>().Single();
            button.HoldDuration = TimeSpan.FromMilliseconds(200);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            vm.NavigateCommand.Execute("01"); Dispatcher.UIThread.RunJobs();
            await Task.Delay(300); Dispatcher.UIThread.RunJobs();
            window.MouseUp(point, MouseButton.Left);
            Assert.False(vm.Comparison.IsQrHelpOpen); Assert.Equal(0, button.HoldProgress);
            vm.Generation.Settings.Format = CodeKind.Barcode;
            vm.Generation.Settings.Mode = GenerationMode.Single;
            vm.Generation.GenerateCommand.Execute(null);
            vm.NavigateCommand.Execute("04"); Dispatcher.UIThread.RunJobs();
            vm.Comparison.OpenQrHelpCommand.Execute(null);
            Assert.False(vm.Comparison.IsQrHelpOpen);
            Assert.False(window.GetVisualDescendants().OfType<HoldButton>().Single().IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }
}
