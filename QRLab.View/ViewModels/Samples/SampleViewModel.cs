using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QRLab.Core.Models.Generation.Qrcode;
using QRLab.Core.Models.Images;
using QRLab.Core.Models.Recognition;
using QRLab.Core.Models.Samples;
using QRLab.View.ViewModels.Generation;
using QRLab.View.ViewModels.Mutations;
using QRLab.View.ViewModels.Reading;
using ResultStatus = QRLab.Core.Models.Recognition.Status;

namespace QRLab.View.ViewModels.Samples;

public sealed class SampleViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<OperationViewModel> _operations = [];
    private long _revision = -1;
    public SampleState State { get; }
    public SamplePreview Preview => State.Preview;
    public ReadOnlyObservableCollection<OperationViewModel> Operations { get; }
    public OperationViewModel OriginalFrame { get; }
    public SampleViewModel(SampleState state)
    {
        State = state;
        Operations = new(_operations);
        OriginalFrame = new("00", null, OriginalImage);
        SynchronizeOperations();
        State.Changed += StateChanged;
    }
    public string Name => Preview.Name;
    public string Content => Preview.Content;
    public string Dimensions => $"{OriginalImage.Width} × {OriginalImage.Height} px";
    public string Correction => Preview.Settings is QrGenerationOptions qr ? GenerationChoices.Name(GenerationChoices.Corrections, qr.Correction) : "—";
    public string Format => GenerationChoices.CodeName(Preview.Settings);
    public RasterImage OriginalImage => Preview.Image;
    public RasterImage Image => State.Image;
    public RecognitionPreview? Result => State.Result;
    public string Status => RecognitionChoices.StatusName(Result?.Status);
    public bool IsSuccess => Result is { Status: ResultStatus.Success };
    public bool IsFailure => Result?.Status is ResultStatus.Failed or ResultStatus.Timeout or ResultStatus.Mismatch;
    public string Time => Result is null ? "—" : $"{Result.Milliseconds:N1} мс";
    public string Chain => Operations.Count == 0 ? MutationChoices.Original.Name : string.Join(" → ", Operations.Select(operation => operation.Name));
    public string Decoded => Result?.DecodedContent ?? "—";
    private void SynchronizeOperations()
    {
        if (_revision == State.Mutations.Revision) return;
        _revision = State.Mutations.Revision;
        _operations.Clear();
        foreach (var (frame, index) in State.Mutations.Frames.Select((frame, index) => (frame, index)))
            _operations.Add(new($"{index + 1:00}", frame.Options, frame.Image));
    }
    private void StateChanged(object? sender, EventArgs args)
    {
        SynchronizeOperations();
        OnPropertyChanged(nameof(Image));
        OnPropertyChanged(nameof(Chain));
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsFailure));
        OnPropertyChanged(nameof(Time));
        OnPropertyChanged(nameof(Decoded));
    }
    public void Dispose() => State.Changed -= StateChanged;
}
