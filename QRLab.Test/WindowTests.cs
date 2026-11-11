using Avalonia.Input;
using QRLab.Core.Models.Mutations;
using QRLab.Test.Fixtures;
using QRLab.Core.Models.Generation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using QRLab.View.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using QRLab.Core.Models.Generation.Barcode;
using QRLab.View;
using QRLab.View.ViewModels;
using QRLab.View.ViewModels.Reading;
using QRLab.View.Views;
using Xunit;
[assembly: AvaloniaTestApplication(typeof(QRLab.Test.TestAppBuilder))]
namespace QRLab.Test;
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().WithInterFont().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public sealed class WindowTests
{
    [AvaloniaFact]
    public async Task RecognitionSummaryOpensFirstAndMismatchRowsAreGold()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 2;
        vm.Generation.GenerateCommand.Execute(null);
        var samples = vm.Workspace.Samples.Select(sample => sample.Preview).ToArray();
        samples[0] = samples[0] with { Content = "OTHER" };
        vm.Workspace.State.Replace("Проверка несовпадений", samples);
        vm.Reading.Settings.Timeout = 10000;
        await vm.Reading.RunCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("03"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single(control => control.Name == "RecognitionPanels");
            Assert.Equal(0, tabs.SelectedIndex);
            var summary = Assert.IsType<ScrollViewer>(Assert.IsType<TabItem>(tabs.SelectedItem).Content);
            Assert.True(summary.Extent.Height <= summary.Viewport.Height, $"Сводка не помещается: {summary.Extent.Height} > {summary.Viewport.Height}");
            var list = window.GetVisualDescendants().OfType<ListBox>().Single(control => control.Name == "RecognitionResultsList");
            var rows = list.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
            var mismatch = rows.Single(row => ReferenceEquals(row.DataContext, vm.Workspace.Samples[0]));
            Assert.Contains("mismatch", mismatch.Classes);
            Assert.Equal(Color.Parse("#493A19"), Assert.IsAssignableFrom<ISolidColorBrush>(mismatch.Background).Color);
            var filter = window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "RecognitionStatusFilter");
            filter.SelectedItem = vm.Reading.Results.Filters.Single(option => option.Id == RecognitionFilter.Mismatch);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(RecognitionFilter.Mismatch, vm.Reading.Results.Filter);
            Assert.Single(vm.Reading.Results.Items);
            if (Environment.GetEnvironmentVariable("QRLAB_CAPTURE") is { } directory)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new RenderTargetBitmap(new PixelSize(1440, 940), new Vector(96, 96));
                bitmap.Render(window); using var output = File.Create(Path.Combine(directory, "reading-statuses.png"));
                bitmap.Save(output, PngBitmapEncoderOptions.Default);
            }
            tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Assert.Contains(window.GetVisualDescendants().OfType<NumericUpDown>(), control => control.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ComparisonDisplaysBinaryPixelsAndInspectsTheSharedSample()
    {
        using var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 2;
        vm.Generation.Settings.ImageWidth = vm.Generation.Settings.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        var samples = vm.Workspace.Samples.Select(sample => sample.Preview).ToArray();
        samples[0] = samples[0] with { Frames = [new(new GaussianBlurOptions(), BitmapComparisonTests.ChangedModuleImage(samples[0], 1))] };
        vm.Workspace.State.Replace("Матрицы QR", samples);
        vm.Reading.Settings.Timeout = 10000;
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("04"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var originalPlaceholder = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "OriginalMatrixPlaceholder");
            var finalPlaceholder = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "FinalMatrixPlaceholder");
            Assert.False(originalPlaceholder.IsVisible);
            Assert.True(finalPlaceholder.IsVisible);
            Assert.Empty(window.GetVisualDescendants().OfType<Slider>());
            await vm.Reading.RunCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.False(originalPlaceholder.IsVisible);
            Assert.False(finalPlaceholder.IsVisible);
            var previews = window.GetVisualDescendants().OfType<BitmapPreview>().ToArray();
            Assert.Equal(2, previews.Length);
            Assert.All(previews, preview => Assert.NotNull(preview.Image));
            Assert.True(vm.Comparison.Comparison!.Changed > 0);
            vm.Comparison.HighlightChanges = true;
            Dispatcher.UIThread.RunJobs();
            var final = previews.Single(preview => preview.Name == "FinalBitmapPreview");
            Assert.True(final.HighlightChanges);
            var center = final.TranslatePoint(new Point(final.Bounds.Width / 2, final.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left); window.MouseUp(center, MouseButton.Left);
            Assert.Equal(default, final.PanOffset);
            window.MouseWheel(center, new Vector(0, 2));
            Dispatcher.UIThread.RunJobs();
            Assert.All(previews, preview => Assert.Equal(1.2, preview.Zoom));
            var moved = center + new Vector(35, 20);
            window.MouseDown(center, MouseButton.Left); window.MouseMove(moved); window.MouseUp(moved, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.All(previews, preview => Assert.Equal(new Vector(35, 20), preview.PanOffset));
            var original = previews.Single(preview => preview.Name == "OriginalBitmapPreview");
            var originalCenter = original.TranslatePoint(new Point(original.Bounds.Width / 2, original.Bounds.Height / 2), window)!.Value;
            window.MouseWheel(originalCenter, new Vector(0, -1)); Dispatcher.UIThread.RunJobs();
            Assert.All(previews, preview => Assert.Equal(1.1, preview.Zoom, 6));
            vm.Comparison.ResetZoomCommand.Execute(null); Dispatcher.UIThread.RunJobs();
            Assert.All(previews, preview => { Assert.Equal(1, preview.Zoom); Assert.Equal(default, preview.PanOffset); });
            vm.Comparison.HighlightZones = true; vm.Comparison.HighlightCodewords = true;
            Dispatcher.UIThread.RunJobs();
            Assert.All(previews, preview => { Assert.True(preview.HighlightZones); Assert.True(preview.HighlightCodewords); Assert.NotNull(preview.Layout); });
            if (Environment.GetEnvironmentVariable("QRLAB_CAPTURE") is { } directory)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new RenderTargetBitmap(new PixelSize(1440, 940), new Vector(96, 96));
                bitmap.Render(window);
                using var output = File.Create(Path.Combine(directory, "comparison-populated.png"));
                bitmap.Save(output, PngBitmapEncoderOptions.Default);
            }
            Assert.Equal(5, window.GetVisualDescendants().OfType<CheckBox>().Count());
            var wordsCheckBox = window.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "HighlightCodewordsCheckBox");
            var blocksCheckBox = window.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "HighlightBlocksCheckBox");
            Assert.True(blocksCheckBox.IsEnabled);
            blocksCheckBox.IsChecked = true; Dispatcher.UIThread.RunJobs();
            Assert.True(vm.Comparison.HighlightBlocks);
            Assert.False(vm.Comparison.HighlightCodewords);
            Assert.False(wordsCheckBox.IsChecked);
            Assert.All(previews, preview => Assert.True(preview.HighlightBlocks));
            Assert.All(previews, preview => Assert.False(preview.HighlightCodewords));
            wordsCheckBox.IsChecked = true; Dispatcher.UIThread.RunJobs();
            Assert.False(blocksCheckBox.IsChecked);
            Assert.False(vm.Comparison.HighlightBlocks);
            Assert.All(previews, preview => Assert.False(preview.HighlightBlocks));
            Assert.All(previews, preview => Assert.True(preview.HighlightCodewords));
            var showCodeCheckBox = window.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "ShowResultCodeCheckBox");
            Assert.True(showCodeCheckBox.IsChecked);
            Assert.True(vm.Comparison.ShowResultCode);
            Assert.True(final.ShowModules);
            showCodeCheckBox.IsChecked = false; Dispatcher.UIThread.RunJobs();
            Assert.False(vm.Comparison.ShowResultCode);
            Assert.False(final.ShowModules);
            Assert.True(original.ShowModules);
            Assert.True(final.HighlightCodewords);
            Assert.True(final.HighlightChanges);
            vm.Comparison.ShowResultCode = true; Dispatcher.UIThread.RunJobs();
            Assert.True(showCodeCheckBox.IsChecked);
            Assert.True(final.ShowModules);
            var legendButton = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "QrLegendButton");
            Assert.Contains(legendButton.GetVisualDescendants().OfType<SvgIcon>(), icon => icon.Source == "info");
            var legend = Assert.IsType<Flyout>(legendButton.Flyout);
            legend.ShowAt(legendButton); Dispatcher.UIThread.RunJobs();
            Assert.True(legend.IsOpen); legend.Hide();
            var diagnostics = window.GetVisualDescendants().OfType<ItemsControl>().Single(control => control.Name == "DamageDiagnostics");
            Assert.NotEmpty(vm.Comparison.Sections);
            Assert.Same(vm.Workspace.SelectedSample!.Result!.Inspection!.Sections, vm.Comparison.Sections);
            Assert.Same(vm.Workspace.SelectedSample.Result.Inspection.Modules, vm.Comparison.Comparison);
            Assert.Contains(vm.Comparison.Sections.SelectMany(section => section.Parameters), value => value.Name == "Предел коррекции ошибок");
            var dataCard = diagnostics.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("card"));
            Assert.Contains(dataCard.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Данные распознавания");
            vm.Workspace.NextCommand.Execute(null); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, vm.Comparison.Comparison!.Changed);
            Assert.Same(vm.Workspace.SelectedSample!.State.FinalBitmap, final.Image);
            vm.Mutations.AddCommand.Execute(MutationKind.GaussianBlur); Dispatcher.UIThread.RunJobs();
            Assert.False(originalPlaceholder.IsVisible);
            Assert.True(finalPlaceholder.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LogoMenuControlsBorderlessWindow()
    {
        var window = new MainWindow { DataContext = TestApplicationFactory.CreateMainWindow() };
        try
        {
            window.Show();
            Assert.Equal(WindowDecorations.None, window.WindowDecorations);
            Assert.NotNull(window.Icon);
            var button = window.FindControl<Button>("WindowMenuButton")!;
            var menu = Assert.IsType<MenuFlyout>(button.Flyout);
            menu.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            var items = menu.Items.OfType<MenuItem>().ToArray();
            Assert.Equal(new[] { "Выход", "Свернуть", "Развернуть" }, items.Select(item => item.Header));
            items[2].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal("Восстановить", items[2].Header);
            items[2].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal("Развернуть", items[2].Header);
            items[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(WindowState.Minimized, window.WindowState);
            window.WindowState = WindowState.Normal;
            items[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AllThreeScreensLoadAndNavigationChangesVisibleContent()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show();
            foreach (var screen in vm.Screens)
            {
                var navigation = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Classes.Contains("nav"));
                navigation.SelectedItem = screen;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Same(screen, vm.SelectedScreen);
                var navIcons = navigation.GetVisualDescendants().OfType<SvgIcon>().ToArray();
                Assert.Equal(4, navIcons.Length);
                Assert.All(navIcons, icon =>
                {
                    var item = icon.GetVisualAncestors().OfType<ListBoxItem>().First();
                    var expected = Color.Parse(item.IsSelected ? "#75D9BE" : "#86928F");
                    Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(icon.Foreground).Color);
                    var caption = item.GetVisualDescendants().OfType<TextBlock>().Single();
                    Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(caption.Foreground).Color);
                });
                var text = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains(screen.Title, text);
                Assert.DoesNotContain(text, t => t?.Contains("Функциональность будет добавлена") == true);
                if (screen == vm.Comparison) Assert.Equal(2, window.GetVisualDescendants().OfType<BitmapPreview>().Count());
                else if (screen != vm.Reading) Assert.NotEmpty(window.GetVisualDescendants().OfType<QRLab.View.Controls.RasterPreview>());
                if (Environment.GetEnvironmentVariable("QRLAB_CAPTURE") is { } directory)
                {
                    Directory.CreateDirectory(directory);
                    using var bitmap = new RenderTargetBitmap(new PixelSize(1440, 940), new Vector(96, 96));
                    bitmap.Render(window); using var output = File.Create(Path.Combine(directory, $"screen-{screen.Number}.png")); bitmap.Save(output, PngBitmapEncoderOptions.Default);
                }
            }
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public void GenerationSelectionsBindEnumsInBothDirections()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var formatList = window.GetVisualDescendants().OfType<ListBox>()
                .Single(list => ReferenceEquals(list.ItemsSource, vm.Generation.Settings.Formats));
            formatList.SelectedItem = vm.Generation.Settings.Formats.Single(option => option.Id == CodeKind.Barcode);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(CodeKind.Barcode, vm.Generation.Settings.Format);

            var barcodeCombo = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => ReferenceEquals(combo.ItemsSource, vm.Generation.Settings.Barcode.Types));
            barcodeCombo.SelectedItem = vm.Generation.Settings.Barcode.Types.Single(option => option.Id == BarcodeType.Ean13);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(BarcodeType.Ean13, vm.Generation.Settings.Barcode.Type);
            Assert.True(vm.Generation.Settings.Barcode.IsEan13);

            vm.Generation.Settings.Format = CodeKind.QrCode;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(CodeKind.QrCode, formatList.SelectedValue);

            var sourceCombo = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => ReferenceEquals(combo.ItemsSource, vm.Generation.Settings.Sources));
            sourceCombo.SelectedItem = vm.Generation.Settings.Sources.Single(option => option.Id == ContentSource.Text);
            Dispatcher.UIThread.RunJobs();
            vm.Generation.Settings.Mode = GenerationMode.Single;
            vm.Generation.Settings.Template = "Literal-{num}";
            vm.Generation.GenerateCommand.Execute(null);
            Assert.Equal(ContentSource.Text, vm.Generation.Settings.Source);
            Assert.Equal("Literal-{num}", Assert.Single(vm.Workspace.Samples).Content);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EveryMainPreviewCanPanZoomAndResetWithoutEditingTheImage()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 3; vm.Generation.Settings.ImageWidth = 512; vm.Generation.Settings.ImageHeight = 384;
        vm.Generation.GenerateCommand.Execute(null);
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show();
            foreach (var (screen, name) in new[] { ("01", "GenerationPreview"), ("02", "MutationPreview"), ("03", "ReadingPreview") })
            {
                vm.NavigateCommand.Execute(screen); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var preview = window.GetVisualDescendants().OfType<RasterPreview>().Single(control => control.Name == name);
                var start = preview.TranslatePoint(new Point(preview.Bounds.Width / 2, preview.Bounds.Height / 2), window)!.Value;
                var end = start + new Vector(35, 20);
                window.MouseDown(start, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left);
                Assert.Equal(new Vector(35, 20), preview.PanOffset);
                window.MouseWheel(end, new Vector(0, 2));
                Assert.True(preview.Zoom > 1);
                var reset = window.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.Command, preview.ResetViewCommand));
                reset.Command!.Execute(null);
                Assert.Equal(default(Vector), preview.PanOffset); Assert.Equal(1, preview.Zoom);
                Assert.Empty(vm.Mutations.Operations);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MutationSampleNumberAndArrowsStayInSync()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 3; vm.Generation.Settings.ImageWidth = 256; vm.Generation.Settings.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        var window = new MainWindow { DataContext = vm };
        try
        {
            window.Show(); vm.NavigateCommand.Execute("02"); Dispatcher.UIThread.RunJobs();
            var position = window.GetVisualDescendants().OfType<TextBlock>().Single(control => control.Text == "1 / 3");
            var next = window.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.Command, vm.Workspace.NextCommand));
            var previous = window.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.Command, vm.Workspace.PreviousCommand));
            Assert.False(previous.IsEffectivelyEnabled); Assert.True(next.IsEffectivelyEnabled);
            next.Command!.Execute(null); Dispatcher.UIThread.RunJobs();
            Assert.Same(vm.Workspace.Samples[1], vm.Workspace.SelectedSample);
            Assert.Equal("2 / 3", position.Text);
            next.Command.Execute(null); Dispatcher.UIThread.RunJobs();
            Assert.Same(vm.Workspace.Samples[2], vm.Workspace.SelectedSample);
            Assert.Equal("3 / 3", position.Text); Assert.False(next.IsEffectivelyEnabled);
            previous.Command!.Execute(null); Dispatcher.UIThread.RunJobs();
            Assert.Equal("2 / 3", position.Text); Assert.True(next.IsEffectivelyEnabled);
            vm.Workspace.SampleNumber = 100;
            Assert.Same(vm.Workspace.Samples[1], vm.Workspace.SelectedSample);
            Assert.Equal(3, vm.Workspace.SampleCount);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task NavigationButtonsAndFrameSelectionKeepSharedSample()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        var window = new MainWindow { DataContext = vm };
        var savedSet = Path.Combine(Path.GetTempPath(), $"QRLab.Tests-{Guid.NewGuid():N}");
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Command == vm.NavigateCommand && Equals(b.CommandParameter, "02"));
            Assert.NotNull(button.Command); button.Command.Execute(button.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(vm.Mutations, vm.SelectedScreen);
            Assert.NotNull(vm.Workspace.SelectedSample);
            vm.Mutations.AddCommand.Execute(MutationKind.SaltAndPepper);
            vm.Mutations.Noise.DensityPercent = 19;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(19, Assert.IsType<SaltAndPepperOptions>(vm.Mutations.Operations.Last().Options).DensityPercent);
            Assert.NotSame(vm.Workspace.SelectedSample!.OriginalImage, vm.Mutations.PreviewImage);
            vm.Mutations.AddCommand.Execute(MutationKind.GaussianBlur);
            var blurred = vm.Mutations.PreviewImage;
            vm.Mutations.GaussianBlur.Sigma = 3;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, Assert.IsType<GaussianBlurOptions>(vm.Mutations.SelectedOperation!.Options).Sigma);
            Assert.NotSame(blurred, vm.Mutations.PreviewImage);
            Assert.NotNull(vm.Mutations.SelectedOperation);
            vm.Mutations.SelectedFrame = vm.Mutations.Frames[0];
            Assert.Null(vm.Mutations.SelectedOperation);
            Assert.Same(vm.Workspace.SelectedSample.OriginalImage, vm.Mutations.PreviewImage);
            vm.Workspace.NextCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(vm.Workspace.SelectedSample);
            vm.NavigateCommand.Execute("03");
            await vm.Reading.RunCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(120, vm.Reading.Results.Checked);
            var selected = vm.Workspace.SelectedSample;
            vm.Reading.Results.Filter = RecognitionFilter.NotFound; vm.Reading.Results.Search = "no-match";
            Dispatcher.UIThread.RunJobs();
            Assert.Same(selected, vm.Workspace.SelectedSample);
            vm.NavigateCommand.Execute("01"); Dispatcher.UIThread.RunJobs();
            vm.Generation.OpenLibraryCommand.Execute(null); Dispatcher.UIThread.RunJobs();
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Command == vm.Generation.LoadSetCommand);
            vm.Generation.LoadSetCommand.Execute(vm.Generation.LibrarySets[0]); Dispatcher.UIThread.RunJobs();
            Assert.Equal(12, vm.Workspace.Samples.Count);
            vm.Generation.ExportCommand.Execute(null); Dispatcher.UIThread.RunJobs();
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Command == vm.Generation.ConfirmExportCommand);
            vm.Generation.ExportDestination = savedSet;
            await vm.Generation.ConfirmExportCommand.ExecuteAsync(null); Dispatcher.UIThread.RunJobs();
            Assert.False(vm.Generation.IsExportOpen);
            Assert.Contains("Сохранён набор", vm.Workspace.Notice);
            Assert.True(File.Exists(Path.Combine(savedSet, "qrlab-set.json")));
        }
        finally
        {
            window.Close();
            if (Directory.Exists(savedSet)) Directory.Delete(savedSet, recursive: true);
        }
    }
}
