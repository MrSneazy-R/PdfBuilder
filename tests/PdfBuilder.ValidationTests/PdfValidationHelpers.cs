using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PdfBuilder.ValidationTests;

internal static class PdfValidationHelpers
{
    public static string CreateTemporaryDirectory(string fixtureName)
    {
        var path = Path.Combine(Path.GetTempPath(), "PdfBuilder.Validation", fixtureName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void AssertStructuralValidity(string qpdf, string pdfPath)
    {
        var result = ValidationTools.Run(qpdf, "--check", pdfPath);
        result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
    }

    public static string ExtractText(string pdftotext, string pdfPath, string directory)
    {
        var textPath = Path.Combine(directory, "extracted.txt");
        var result = ValidationTools.Run(pdftotext, "-enc", "UTF-8", pdfPath, textPath);
        result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
        return File.ReadAllText(textPath);
    }

    public static IReadOnlyList<string> Rasterize(string pdftoppm, string pdfPath, string directory)
    {
        var prefix = Path.Combine(directory, "page");
        var result = ValidationTools.Run(pdftoppm, "-png", "-r", "96", pdfPath, prefix);
        result.ExitCode.Should().Be(0, result.StandardOutput + result.StandardError);
        return Directory.GetFiles(directory, "page-*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    public static void CompareImages(string approvedPath, string actualPath, string failureDirectory, double changedRatioTolerance = 0.002d)
    {
        using var approved = Image.Load<Rgba32>(approvedPath);
        using var actual = Image.Load<Rgba32>(actualPath);
        approved.Size.Should().Be(actual.Size, "page geometry is part of the visual baseline");

        using var difference = new Image<Rgba32>(approved.Width, approved.Height);
        var changed = 0;
        const int channelTolerance = 18;
        for (var y = 0; y < approved.Height; y++)
            for (var x = 0; x < approved.Width; x++)
            {
                var expected = approved[x, y];
                var observed = actual[x, y];
                var delta = Math.Max(Math.Max(Math.Abs(expected.R - observed.R), Math.Abs(expected.G - observed.G)), Math.Abs(expected.B - observed.B));
                if (delta > channelTolerance)
                {
                    changed++;
                    difference[x, y] = new Rgba32(255, 0, 255, 255);
                }
            }

        var changedRatio = (double)changed / (approved.Width * approved.Height);
        if (changedRatio > changedRatioTolerance)
        {
            Directory.CreateDirectory(failureDirectory);
            File.Copy(actualPath, Path.Combine(failureDirectory, Path.GetFileName(actualPath)), overwrite: true);
            File.Copy(approvedPath, Path.Combine(failureDirectory, "expected-" + Path.GetFileName(actualPath)), overwrite: true);
            difference.SaveAsPng(Path.Combine(failureDirectory, Path.GetFileNameWithoutExtension(actualPath) + ".diff.png"));
        }

        changedRatio.Should().BeLessThanOrEqualTo(changedRatioTolerance, $"visual comparison permits anti-aliasing variance only: 18 channel values across at most {changedRatioTolerance:P1} of pixels");
    }
}
