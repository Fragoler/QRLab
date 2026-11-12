using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace QRLab.View.Controls;

/// <summary>Keeps the ordinary click and invokes a separate command after an uninterrupted hold.</summary>
public sealed class HoldButton : Button
{
    public static readonly StyledProperty<ICommand?> HoldCommandProperty =
        AvaloniaProperty.Register<HoldButton, ICommand?>(nameof(HoldCommand));
    public static readonly StyledProperty<TimeSpan> HoldDurationProperty =
        AvaloniaProperty.Register<HoldButton, TimeSpan>(nameof(HoldDuration), TimeSpan.FromMilliseconds(650),
            validate: duration => duration > TimeSpan.Zero);
    public static readonly DirectProperty<HoldButton, double> HoldProgressProperty =
        AvaloniaProperty.RegisterDirect<HoldButton, double>(nameof(HoldProgress), button => button.HoldProgress);

    public ICommand? HoldCommand { get => GetValue(HoldCommandProperty); set => SetValue(HoldCommandProperty, value); }
    public TimeSpan HoldDuration { get => GetValue(HoldDurationProperty); set => SetValue(HoldDurationProperty, value); }
    public double HoldProgress { get => _holdProgress; private set => SetAndRaise(HoldProgressProperty, ref _holdProgress, value); }
    protected override Type StyleKeyOverride => typeof(Button);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private double _holdProgress;
    private IPointer? _pointer;
    private long _started;
    private bool _suppressClick;

    public HoldButton() => _timer.Tick += Tick;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _suppressClick = false;
        _pointer = e.Pointer;
        _started = Stopwatch.GetTimestamp();
        HoldProgress = 0;
        _timer.Start();
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (_pointer is null || !IsEffectivelyEnabled || !IsEffectivelyVisible) { CancelHold(); return; }
        HoldProgress = Math.Clamp(Stopwatch.GetElapsedTime(_started).TotalMilliseconds / HoldDuration.TotalMilliseconds, 0, 1);
        if (HoldProgress < 1) return;
        _timer.Stop();
        _suppressClick = true;
        if (HoldCommand?.CanExecute(CommandParameter) == true) HoldCommand.Execute(CommandParameter);
    }

    protected override void OnClick()
    {
        if (_suppressClick) { _suppressClick = false; return; }
        base.OnClick();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _pointer is not null)
        {
            CancelHold();
            _suppressClick = true;
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Space or Key.Enter) _suppressClick = false;
        base.OnKeyDown(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        CancelHold();
        base.OnPointerReleased(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pointer is not null && !new Rect(Bounds.Size).Contains(e.GetPosition(this))) CancelHold();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        CancelHold();
        base.OnPointerExited(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        CancelHold();
        base.OnPointerCaptureLost(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) CancelHold();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelHold();
        base.OnDetachedFromVisualTree(e);
    }

    private void CancelHold()
    {
        _timer.Stop();
        _pointer = null;
        HoldProgress = 0;
    }
}
