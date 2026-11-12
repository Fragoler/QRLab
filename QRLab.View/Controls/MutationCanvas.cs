using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using QRLab.Core.Models.Images;
using QRLab.View.Controls.Mutations;
using QRLab.View.ViewModels.Mutations;

namespace QRLab.View.Controls;

/// <summary>
/// Hosts a graphical mutation tool and shares the raster preview's viewport.
/// </summary>
public sealed class MutationCanvas : RasterPreview
{
    public static readonly StyledProperty<MutationSettingsViewModel?> SettingsProperty =
        AvaloniaProperty.Register<MutationCanvas, MutationSettingsViewModel?>(nameof(Settings));
    public MutationSettingsViewModel? Settings { get => GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
    private IMutationTool? _tool;
    private IPointer? _pointer;
    private ImageViewport Viewport => new(new Size(Image!.Width, Image.Height), ImageBounds);
    private bool HasImage => Image is not null && ImageBounds.Width > 0 && ImageBounds.Height > 0;

    public MutationCanvas()
    {
        Focusable = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SettingsProperty)
        {
            CancelGesture();
            if (change.OldValue is MutationSettingsViewModel old) old.PropertyChanged -= SettingsChanged;
            if (Settings is { } current) current.PropertyChanged += SettingsChanged;
            _tool = MutationToolFactory.Create(Settings);
            InvalidateVisual();
        }
        else if (change.Property == ImageProperty && (Image is null || change.OldValue is not RasterImage oldImage
            || Image.Width != oldImage.Width || Image.Height != oldImage.Height)) CancelGesture();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Settings is { } settings) { settings.PropertyChanged -= SettingsChanged; settings.PropertyChanged += SettingsChanged; }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelGesture();
        if (Settings is { } settings) settings.PropertyChanged -= SettingsChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (HasImage) _tool?.Render(context, Viewport);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        if (_pointer is not null) { e.Handled = true; return; }
        if (HasImage && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && _tool?.Begin(e.GetPosition(this), Viewport) == true)
        {
            e.Handled = true;
            Focus();
            _pointer = e.Pointer;
            e.Pointer.Capture(this);
            InvalidateVisual();
        }
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (e.Handled) return;
        if (_pointer is not null)
        {
            e.Handled = true;
            if (HasImage) _tool?.Move(e.GetPosition(this), Viewport);
            InvalidateVisual();
        }
        base.OnPointerMoved(e);
        if (_pointer is null && HasImage)
            Cursor = new Cursor(_tool?.GetCursor(e.GetPosition(this), Viewport) ?? StandardCursorType.Hand);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.Handled) return;
        if (_pointer is not null)
        {
            e.Handled = true;
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (HasImage) _tool?.Complete(e.GetPosition(this), Viewport);
            CancelGesture();
        }
        base.OnPointerReleased(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        CancelGesture();
        base.OnPointerCaptureLost(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_pointer is not null) { e.Handled = true; return; }
        base.OnPointerWheelChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _pointer is not null)
        {
            CancelGesture(restore: true);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void CancelGesture(bool restore = false)
    {
        var pointer = _pointer;
        _pointer = null;
        _tool?.Cancel(restore);
        pointer?.Capture(null);
        InvalidateVisual();
    }
}
