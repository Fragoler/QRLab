using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using QRLab.View.Controls;
using QRLab.View.ViewModels;

namespace QRLab.View.Views;

public partial class ComparisonView : UserControl
{
    private ComparisonViewModel? _model;

    public ComparisonView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ObserveModel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveModel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= ModelChanged;
            _model.IsQrHelpOpen = false;
            _model = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void ObserveModel()
    {
        if (_model is not null) _model.PropertyChanged -= ModelChanged;
        _model = DataContext as ComparisonViewModel;
        if (_model is not null) _model.PropertyChanged += ModelChanged;
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ComparisonViewModel.IsQrHelpOpen) || _model is not { } model) return;
        var opening = model.IsQrHelpOpen;
        if (opening && this.FindControl<HoldButton>("QrLegendButton") is { } legend)
        {
            legend.Flyout?.Hide();
            ToolTip.SetIsOpen(legend, false);
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (_model != model || model.IsQrHelpOpen != opening) return;
            if (opening) this.FindControl<Button>("CloseQrHelpButton")?.Focus();
            else this.FindControl<HoldButton>("QrLegendButton")?.Focus();
        }, DispatcherPriority.Input);
    }

    private void DismissQrHelpOnBackground(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _model?.CloseQrHelpCommand.Execute(null);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _model?.IsQrHelpOpen == true)
        {
            _model.CloseQrHelpCommand.Execute(null);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
