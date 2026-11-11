using QRLab.Core.Interfaces.Generation;
using QRLab.Core.Models.Generation;
using QRLab.Core.Models.Samples;
using QRLab.Image.Generation.Encoders;
using QRLab.Image.Recognition.Inspection;
using ZXing;

namespace QRLab.Image.Generation;

public sealed class CodeGenerator : ICodeGenerator
{
    private readonly IReadOnlyList<CodeEncoder> _encoders;
    private readonly CodeImageRenderer _renderer = new();

    public CodeGenerator() : this([new QrCodeEncoder(), new BarcodeEncoder()]) { }

    public CodeGenerator(IEnumerable<CodeEncoder> encoders) => _encoders = encoders.ToArray();

    public IReadOnlyList<SamplePreview> CreateSamples(GenerationOptions options)
    {
        options.Validate();
        var encoder = _encoders.FirstOrDefault(e => e.Supports(options))
            ?? throw new ArgumentException("Кодировщик для выбранного типа кода не зарегистрирован.");
        var result = new List<SamplePreview>(options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            var number = checked(options.FirstNumber + index);
            try
            {
                var content = options.PrepareContent(number);
                var matrix = encoder.Encode(content, options);
                if (matrix.Width > options.Width || matrix.Height > options.Height)
                    throw new ArgumentException($"Увеличьте размер изображения минимум до {matrix.Width} × {matrix.Height} пикселей.");
                var image = _renderer.Render(matrix, options);
                var modules = ModuleMatrix.FromEncoder(matrix, options);
                result.Add(
                    new SamplePreview(number, options.GetSampleName(number), content, image, [], options)
                    {
                        OriginalBitmap = modules,
                        Layout = CodeInspectors.For(options).CreateLayout(modules, options)
                    });
            }
            catch (Exception exception) when (exception is ArgumentException or WriterException)
            {
                throw new ArgumentException($"Образец {number}: {exception.Message}", exception);
            }
        }
        return result;
    }
}
