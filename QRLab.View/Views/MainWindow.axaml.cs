using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using QRLab.View.Controls;

namespace QRLab.View.Views;

public partial class MainWindow : Window
{
    private static readonly Lazy<WindowIcon> ApplicationIcon = new(CreateApplicationIcon);

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
        Icon = ApplicationIcon.Value;
        AddHandler(PointerPressedEvent, ResizeWindowPointerPressed, RoutingStrategies.Tunnel);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
                MaximizeWindowMenuItem.Header = WindowState == WindowState.Maximized ? "Восстановить" : "Развернуть";
        };
    }

    // Window lifecycle and native pointer gestures belong to the view, not the experiment view models.
    private void ExitWindowClick(object? sender, RoutedEventArgs e) => Close();
    private void MinimizeWindowClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindowClick(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Visual source && source.GetVisualAncestors().Prepend(source).OfType<Button>().Any()) return;
        if (e.ClickCount == 2) ToggleMaximized();
        else BeginMoveDrag(e);
        e.Handled = true;
    }

    private void ResizeWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanResize || WindowState != WindowState.Normal || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        const double grip = 5;
        var left = point.X < grip;
        var right = point.X >= Bounds.Width - grip;
        var top = point.Y < grip;
        var bottom = point.Y >= Bounds.Height - grip;
        WindowEdge? edge = (left, right, top, bottom) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            _ => null
        };
        if (edge is null) return;
        BeginResizeDrag(edge.Value, e);
        e.Handled = true;
    }

    private static WindowIcon CreateApplicationIcon()
    {
        // Native window icons require a bitmap; rasterize the same logo.svg used by the title bar.
        var logo = new SvgIcon { Source = "logo", Width = 64, Height = 64, Foreground = new SolidColorBrush(Color.Parse("#75D9BE")) };
        logo.Measure(new Size(64, 64));
        logo.Arrange(new Rect(0, 0, 64, 64));
        using var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
        bitmap.Render(logo);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}
