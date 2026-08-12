using System.Text;

namespace PdfBuilder.Operations;

internal static class PdfFileValidator
{
    internal static void Validate(string path, long maximumBytes)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 12)
            throw new PdfOutputValidationException("The operation did not produce a non-empty PDF.");
        if (info.Length > maximumBytes)
            throw new PdfOutputValidationException($"The PDF contains {info.Length} bytes, exceeding the configured temporary-output limit of {maximumBytes}.");

        using FileStream stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[5];
        if (stream.Read(header) != header.Length || !header.SequenceEqual("%PDF-"u8))
            throw new PdfOutputValidationException("The output does not begin with a PDF header.");

        int tailLength = (int)Math.Min(4096, stream.Length);
        stream.Position = stream.Length - tailLength;
        byte[] tail = new byte[tailLength];
        stream.ReadExactly(tail);
        if (!Encoding.ASCII.GetString(tail).Contains("%%EOF", StringComparison.Ordinal))
            throw new PdfOutputValidationException("The output does not contain a PDF end-of-file marker near the end of the file.");
    }

    internal static bool IsLinearized(string path)
    {
        using FileStream stream = File.OpenRead(path);
        int length = (int)Math.Min(4096, stream.Length);
        byte[] prefix = new byte[length];
        stream.ReadExactly(prefix);
        return Encoding.ASCII.GetString(prefix).Contains("/Linearized", StringComparison.Ordinal);
    }
}
