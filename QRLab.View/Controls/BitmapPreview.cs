using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using QRLab.Core.Models.Images;

namespace QRLab.View.Controls;

/// <summary>Displays code modules directly through a raw framebuffer, with no PNG encoding.</summary>
public sealed class BitmapPreview : Control
{
    public static readonly StyledProperty<BinaryImage?> ImageProperty = AvaloniaProperty.Register<BitmapPreview, BinaryImage?>(nameof(Image));
    public static readonly StyledProperty<BinaryImage?> ReferenceProperty = AvaloniaProperty.Register<BitmapPreview, BinaryImage?>(nameof(Reference));
    public static readonly StyledProperty<bool> HighlightChangesProperty = AvaloniaProperty.Register<BitmapPreview, bool>(nameof(HighlightChanges));
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<BitmapPreview, double>(nameof(Zoom), 1);
    public static readonly StyledProperty<Vector> PanOffsetProperty = AvaloniaProperty.Register<BitmapPreview, Vector>(nameof(PanOffset));
    public BinaryImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    public BinaryImage? Reference { get => GetValue(ReferenceProperty); set => SetValue(ReferenceProperty, value); }
    public bool HighlightChanges { get => GetValue(HighlightChangesProperty); set => SetValue(HighlightChangesProperty, value); }
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public Vector PanOffset { get => GetValue(PanOffsetProperty); set => SetValue(PanOffsetProperty, value); }
    private WriteableBitmap? _bitmap;
    private Point? _drag;
    static BitmapPreview() => AffectsRender<BitmapPreview>(ImageProperty, ReferenceProperty, HighlightChangesProperty, ZoomProperty, PanOffsetProperty);
    public BitmapPreview()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageProperty || change.Property == ReferenceProperty || change.Property == HighlightChangesProperty)
        {
            _bitmap?.Dispose(); _bitmap = null;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bitmap?.Dispose(); _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    private Rect Destination(BinaryImage image)
    {
        var scale = Math.Min(Bounds.Width / image.Width, Bounds.Height / image.Height) * Zoom;
        return new((Bounds.Width - image.Width * scale) / 2 + PanOffset.X, (Bounds.Height - image.Height * scale) / 2 + PanOffset.Y,
            image.Width * scale, image.Height * scale);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Image is not { } image || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        if (_bitmap is null)
        {
            _bitmap = new(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using var buffer = _bitmap.Lock();
            var row = new byte[image.Width * 4];
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var black = image[x, y];
                    var value = black ? (byte)0 : (byte)255;
                    var i = x * 4;
                    row[i] = row[i + 1] = row[i + 2] = value; row[i + 3] = 255;
                    if (HighlightChanges && Reference is { } original && original.Width == image.Width && original.Height == image.Height && original[x, y] != black)
                    {
                        // Cyan: added black. Coral: removed black.
                        row[i] = black ? (byte)225 : (byte)115;
                        row[i + 1] = black ? (byte)195 : (byte)115;
                        row[i + 2] = black ? (byte)55 : (byte)245;
                    }
                }
                Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
            }
        }
        context.DrawImage(_bitmap, new Rect(_bitmap.Size), Destination(image));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Image is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _drag = e.GetPosition(this);
        e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is not { } previous) return;
        var point = e.GetPosition(this);
        SetCurrentValue(PanOffsetProperty, PanOffset + (point - previous));
        _drag = point; InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is null) return;
        _drag = null; e.Pointer.Capture(null); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { _drag = null; base.OnPointerCaptureLost(e); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (Image is null) { base.OnPointerWheelChanged(e); return; }
        SetCurrentValue(ZoomProperty, Math.Clamp(Zoom + e.Delta.Y * .1, .25, 8));
        e.Handled = true;
    }
}
