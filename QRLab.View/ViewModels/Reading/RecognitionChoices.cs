using QRLab.Core.Models.Recognition;
using QRLab.View.ViewModels.Common;

namespace QRLab.View.ViewModels.Reading;

public static class RecognitionChoices
{
    public static IReadOnlyList<EnumOption<RecognitionFilter>> Filters { get; } = Array.AsReadOnly<EnumOption<RecognitionFilter>>(
        [new(RecognitionFilter.All, "Все статусы"), new(RecognitionFilter.NotRecognized, "Не распознавался"),
         new(RecognitionFilter.NotFound, "Код не найден"), new(RecognitionFilter.Failed, "Найден, не расшифрован"),
         new(RecognitionFilter.Successful, "Найден, расшифрован"), new(RecognitionFilter.Mismatch, "Расшифрован неверно"),
         new(RecognitionFilter.Timeout, "Тайм-аут")]);
    public static IReadOnlyList<RecognitionStatusLabel> Statuses { get; } = Array.AsReadOnly<RecognitionStatusLabel>(
        [new(null, "Не распознавался"), new(Status.NotFound, "Код не найден"),
         new(Status.Failed, "Найден, не расшифрован"), new(Status.Success, "Найден, расшифрован"),
         new(Status.Timeout, "Тайм-аут"), new(Status.Mismatch, "Расшифрован неверно")]);
    public static string StatusName(Status? value) => Statuses.Single(label => label.Value == value).Name;
}

public enum RecognitionFilter { All, Successful, Failed, NotRecognized, NotFound, Mismatch, Timeout }

public sealed record RecognitionStatusLabel(Status? Value, string Name);
