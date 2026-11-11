using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ZXing.PDF417;

namespace QRLab.Image.Recognition.Inspection;

/// <summary>Isolates access to the symbol table hidden by the pinned ZXing.Net 0.16.11 API.</summary>
internal static class Pdf417Symbols
{
    private static readonly Lazy<(int[] Symbols, Func<long, int> Decode)> Table = new(Load);
    public static IReadOnlyList<int> Patterns => Table.Value.Symbols;
    public static int Decode(long pattern) => Table.Value.Decode(pattern);

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "ZXing.PDF417.PDF417Common", "zxing")]
    private static (int[], Func<long, int>) Load()
    {
        var type = typeof(PDF417Reader).Assembly.GetType("ZXing.PDF417.PDF417Common", throwOnError: true)!;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var symbols = (int[])type.GetField("SYMBOL_TABLE", flags)!.GetValue(null)!;
        var decode = type.GetMethod("getCodeword", flags)!.CreateDelegate<Func<long, int>>();
        return (symbols.ToArray(), decode);
    }
}
