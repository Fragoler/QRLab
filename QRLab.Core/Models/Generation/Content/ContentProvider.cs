using System.Globalization;

namespace QRLab.Core.Models.Generation.Content;

/// <summary>Creates payloads independently of the barcode format and rendering.</summary>
public abstract class ContentProvider
{
    public abstract string Create(GenerationOptions options, int number);

    public static ContentProvider For(ContentSource source) => source switch
    {
        ContentSource.NumberedTemplate => new NumberedContentProvider(),
        ContentSource.Text => new TextContentProvider(),
        ContentSource.RandomString => new RandomContentProvider(),
        _ => throw new ArgumentException("Неизвестный источник содержимого.")
    };
}

public sealed class NumberedContentProvider : ContentProvider
{
    public override string Create(GenerationOptions options, int number) => options.Template.Replace(
        GenerationDefaults.NumberPlaceholder, number.ToString(GenerationDefaults.NumberFormat, CultureInfo.InvariantCulture));
}

public sealed class TextContentProvider : ContentProvider
{
    public override string Create(GenerationOptions options, int number) => options.Template;
}

public sealed class RandomContentProvider : ContentProvider
{
    public const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public override string Create(GenerationOptions options, int number)
    {
        var alphabet = options is Ean13GenerationOptions ? "0123456789" : Alphabet;
        var length = options is Ean13GenerationOptions && options.RandomLength == 13 ? 12 : options.RandomLength;
        if (length is < 1 or > GenerationLimits.MaximumContentLength)
            throw new ArgumentException("Укажите допустимую длину случайной строки.");
        return string.Create(length, alphabet, (characters, source) =>
        {
            for (var i = 0; i < characters.Length; i++) characters[i] = source[Random.Shared.Next(source.Length)];
        });
    }
}
