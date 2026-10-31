using QRLab.Core.Models.Recognition;

namespace QRLab.View.Presentation.Recognition;

public static class RecognitionChoices
{
    public static IReadOnlyList<EnumOption<RecognitionFilter>> Filters { get; } = Array.AsReadOnly<EnumOption<RecognitionFilter>>(
        [new(RecognitionFilter.All, "Все"), new(RecognitionFilter.Successful, "Успешные"), new(RecognitionFilter.Failed, "Ошибки")]);
    public static IReadOnlyList<RecognitionStatusLabel> Statuses { get; } = Array.AsReadOnly<RecognitionStatusLabel>(
        [new(null, "Не обработан"), new(Status.Success, "Считан"), new(Status.Failed, "Не считан"),
         new(Status.Timeout, "Тайм-аут"), new(Status.Mismatch, "Не совпадает")]);
    public static string StatusName(Status? value) => Statuses.Single(label => label.Value == value).Name;
}

public enum RecognitionFilter { All, Successful, Failed }

public sealed record RecognitionStatusLabel(Status? Value, string Name);
