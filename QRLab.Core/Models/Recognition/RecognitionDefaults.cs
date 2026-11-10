namespace QRLab.Core.Models.Recognition;

public static class RecognitionDefaults
{
    public const string DecoderName = "ZXing";
    public const int TimeoutMilliseconds = 500;
    public const int MinimumTimeoutMilliseconds = 1;
    public const int MaximumTimeoutMilliseconds = 60000;
    public const bool Enhanced = true;
}