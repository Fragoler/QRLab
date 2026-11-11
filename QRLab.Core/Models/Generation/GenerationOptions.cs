using QRLab.Core.Models.Generation.Content;
using System.Globalization;
using System.Text.Json.Serialization;
using QRLab.Core.Models.Generation.Qrcode;

namespace QRLab.Core.Models.Generation;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$code")]
[JsonDerivedType(typeof(QrGenerationOptions), "qr")]
[JsonDerivedType(typeof(Ean13GenerationOptions), "ean13")]
[JsonDerivedType(typeof(Code128GenerationOptions), "code128")]
[JsonDerivedType(typeof(Pdf417GenerationOptions), "pdf417")]
public abstract record GenerationOptions
{
    [JsonIgnore] public abstract CodeKind Kind { get; }
    [JsonIgnore] public virtual bool IsLinear => false;
    [JsonIgnore] public abstract string SamplePrefix { get; }
    
    public string Template { get; init; } = GenerationDefaults.Template;
    public int FirstNumber { get; init; } = GenerationDefaults.FirstNumber;
    public int Count { get; init; } = GenerationDefaults.Count;
    public int Width { get; init; } = GenerationDefaults.Width;
    public int Height { get; init; } = GenerationDefaults.Height;
    public int QuietZone { get; init; } = GenerationDefaults.QuietZone;
    public ContentSource Source { get; init; } = GenerationDefaults.Source;
    public int RandomLength { get; init; } = GenerationDefaults.RandomLength;

    public virtual void Validate()
    {
        if (Source != ContentSource.RandomString && string.IsNullOrWhiteSpace(Template)) throw new ArgumentException("Введите содержимое или шаблон кода.");
        if (Source == ContentSource.RandomString && RandomLength is < 1 or > GenerationLimits.MaximumContentLength)
            throw new ArgumentException($"Длина случайной строки должна быть от 1 до {GenerationLimits.MaximumContentLength} символов.");
        
        if (Count < GenerationLimits.MinimumCount || Count > GenerationLimits.MaximumCount
            || FirstNumber < GenerationDefaults.FirstNumber || (long)FirstNumber + Count - 1 > int.MaxValue)
            throw new ArgumentException($"Количество должно быть от {GenerationLimits.MinimumCount} до {GenerationLimits.MaximumCount}, первый номер — положительным.");
       
        if (Width < GenerationLimits.MinimumImageSize || Width > GenerationLimits.MaximumImageSize
            || Height < GenerationLimits.MinimumImageSize || Height > GenerationLimits.MaximumImageSize)
            throw new ArgumentException($"Размер изображения должен быть от {GenerationLimits.MinimumImageSize} до {GenerationLimits.MaximumImageSize} пикселей.");
        
        if (QuietZone is < GenerationLimits.MinimumQuietZone or > GenerationLimits.MaximumQuietZone)
            throw new ArgumentException($"Тихая зона должна быть от {GenerationLimits.MinimumQuietZone} до {GenerationLimits.MaximumQuietZone} модулей.");
        
        if (!Enum.IsDefined(Source)) throw new ArgumentException("Неизвестный источник содержимого.");
    }

    public virtual string PrepareContent(int number) => ContentProvider.For(Source).Create(this, number);

    public string GetSampleName(int number) => $"{SamplePrefix}-{number.ToString(GenerationDefaults.NumberFormat, CultureInfo.InvariantCulture)}";
}
