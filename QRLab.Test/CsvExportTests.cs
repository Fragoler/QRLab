using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using QRLab.Core.Models.Recognition;
using QRLab.Image.Recognition.Reports;
using QRLab.Test.Fixtures;
using Xunit;

namespace QRLab.Test;

public sealed class CsvExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"QRLab.Csv-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExportsUtf8WithEscapedQuotesSeparatorsAndMultilineContent()
    {
        var path = Path.Combine(_root, "report.csv");
        var payload = "Привет; \"код\"\r\nВторая строка";
        var row = new RecognitionReportRow(3, "QR-0003", "QR", payload, payload, Status.Success, 12.345, "Размытие", null);
        await new CsvRecognitionReportExporter().ExportAsync(path, [row], TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 239, 187, 191 }, File.ReadAllBytes(path).Take(3).ToArray());
        using var parser = new TextFieldParser(path, Encoding.UTF8) { HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(";");
        Assert.Equal(9, parser.ReadFields()!.Length);
        var fields = parser.ReadFields()!;
        Assert.Equal("3", fields[0]);
        Assert.Equal(payload.Replace("\r\n", "\n"), fields[3].Replace("\r\n", "\n"));
        Assert.Equal(fields[3], fields[4]);
        Assert.Equal("Success", fields[5]);
        Assert.Equal(12.345, double.Parse(fields[6], CultureInfo.InvariantCulture));
        Assert.True(parser.EndOfData);
    }

    [Fact]
    public async Task ReadingExportWritesAllRowsAndCancellationPreservesExistingFile()
    {
        var vm = TestApplicationFactory.CreateMainWindow();
        vm.Generation.Settings.Count = 2; vm.Generation.Settings.ImageWidth = 256; vm.Generation.Settings.ImageHeight = 256;
        vm.Generation.GenerateCommand.Execute(null);
        vm.Reading.Settings.Timeout = 10000;
        await vm.Reading.RunCommand.ExecuteAsync(null);
        vm.Reading.ExportDestination = Path.Combine(_root, "report.csv");
        vm.Reading.IsExportOpen = true;
        await vm.Reading.ConfirmExportCommand.ExecuteAsync(null);
        Assert.False(vm.Reading.IsExportOpen);
        using (var parser = new TextFieldParser(vm.Reading.ExportDestination, Encoding.UTF8))
        {
            parser.SetDelimiters(";");
            Assert.NotNull(parser.ReadFields());
            Assert.NotNull(parser.ReadFields());
            Assert.NotNull(parser.ReadFields());
            Assert.True(parser.EndOfData);
        }
        var before = File.ReadAllBytes(vm.Reading.ExportDestination);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CsvRecognitionReportExporter().ExportAsync(vm.Reading.ExportDestination, [], cancellation.Token));
        Assert.Equal(before, File.ReadAllBytes(vm.Reading.ExportDestination));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}