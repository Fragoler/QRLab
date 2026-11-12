using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using QRLab.Core.Models.Images;

namespace QRLab.View.Controls;

/// <summary>
/// Decodes existing PNG data for display
/// </summary>
public class RasterPreview : Control
{
    private const double DefaultZoom = 1;
    
    public static readonly StyledProperty<RasterImage?> ImageProperty = AvaloniaProperty.Register<RasterPreview, RasterImage?>(nameof(Image));
    public static readonly StyledProperty<bool> AllowZoomProperty = AvaloniaProperty.Register<RasterPreview, bool>(nameof(AllowZoom));
    
    public RasterImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    
    public bool AllowZoom { get => GetValue(AllowZoomProperty); set => SetValue(AllowZoomProperty, value); }
    public Rect ImageBounds => Image is { } image ? Destination(image) : default;
    public ICommand ResetViewCommand { get; }
    public double Zoom => _zoom;
    public Vector PanOffset => _pan;
    
    private Bitmap? _displayImage;
    private Point? _dragStart;
    private Vector _pan;
    private double _zoom = DefaultZoom;
    private int _decodedWidth;
    private bool _attached;

    static RasterPreview() => AffectsRender<RasterPreview>(ImageProperty);
    public RasterPreview()
    {
        ClipToBounds = true;
        ResetViewCommand = new RelayCommand(ResetView);
    }

    public void ResetView()
    {
        _zoom = DefaultZoom; _pan = default; _dragStart = null; InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageProperty)
        {
            ReleaseImage();
            if (change.OldValue is not RasterImage previous || Image is not { } current
                || previous.Width != current.Width || previous.Height != current.Height) ResetView();
            InvalidateVisual();
        }
        else if (change.Property == AllowZoomProperty) Cursor = AllowZoom ? new Cursor(StandardCursorType.Hand) : null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); _attached = true; InvalidateVisual();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false; ReleaseImage(); base.OnDetachedFromVisualTree(e);
    }
    private void ReleaseImage()
    {
        _displayImage?.Dispose(); _displayImage = null; _decodedWidth = 0;
    }
    private Rect Destination(RasterImage image)
    {
        var scale = Math.Min(Bounds.Width / image.Width, Bounds.Height / image.Height) * _zoom;
        var size = new Size(image.Width * scale, image.Height * scale);
        return new Rect((Bounds.Width - size.Width) / 2 + _pan.X, (Bounds.Height - size.Height) / 2 + _pan.Y, size.Width, size.Height);
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (!_attached || Image is not { } image || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var destination = Destination(image);
        var width = Math.Clamp((int)Math.Ceiling(destination.Width * 2), 1, image.Width);
        if (_displayImage is null || width > _decodedWidth)
        {
            ReleaseImage();
            using var stream = image.OpenRead();
            _displayImage = Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.HighQuality);
            _decodedWidth = width;
        }
        context.DrawImage(_displayImage, new Rect(_displayImage.Size), destination);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Handled)
            return;
        
        if (!AllowZoom || Image is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragStart = e.GetPosition(this); e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Handled) return;
        if (_dragStart is not { } previous) return;
        var position = e.GetPosition(this); _pan += position - previous; _dragStart = position; InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled) return;
        if (_dragStart is null) return;
        _dragStart = null; e.Pointer.Capture(null); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _dragStart = null; base.OnPointerCaptureLost(e);
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (!AllowZoom) { base.OnPointerWheelChanged(e); return; }
        _zoom = Math.Clamp(_zoom + e.Delta.Y * .1, .25, 8); InvalidateVisual(); e.Handled = true;
    }
}
