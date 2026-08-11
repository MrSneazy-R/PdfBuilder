using FluentAssertions;
using PdfBuilder.Document;
using PdfBuilder.Operations;
using Xunit;

namespace PdfBuilder.Operations.Tests;

public sealed class QpdfBackendTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("1-3,5,z")]
    [InlineData("odd")]
    [InlineData("r1-r3")]
    public async Task SelectPagesAsync_ValidRange_ReachesInputValidation(string pages)
    {
        var client = new PdfOperationsClient(new QpdfBackendOptions { QpdfPath = "unused" });
        Func<Task> action = () => client.SelectPagesAsync(new PdfInput(Path.Combine(Path.GetTempPath(), "missing.pdf")), pages, "out.pdf");

        await action.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1;--replace-input")]
    [InlineData("1\n--show-encryption")]
    [InlineData("../secret")]
    public async Task SelectPagesAsync_UnsafeRange_IsRejected(string pages)
    {
        string input = CreateDocument(1);
        try
        {
            var client = new PdfOperationsClient(new QpdfBackendOptions { QpdfPath = "unused" });
            Func<Task> action = () => client.SelectPagesAsync(new PdfInput(input), pages, Path.ChangeExtension(input, ".selected.pdf"));

            await action.Should().ThrowAsync<ArgumentException>();
        }
        finally
        {
            File.Delete(input);
        }
    }

    [Fact]
    public async Task Operations_EndToEnd_ProduceIndependentlyValidatedOutputs()
    {
        string? qpdf = FindQpdf();
        if (qpdf == null)
            return;

        string root = Path.Combine(Path.GetTempPath(), $"PdfBuilder-operations-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string first = CreateDocument(3, Path.Combine(root, "first.pdf"));
            string second = CreateDocument(2, Path.Combine(root, "second.pdf"));
            string attachment = Path.Combine(root, "evidence.txt");
            await File.WriteAllTextAsync(attachment, "sanitised test attachment");
            var client = new PdfOperationsClient(new QpdfBackendOptions
            {
                QpdfPath = qpdf,
                ProcessTimeout = TimeSpan.FromSeconds(30),
                TemporaryRoot = Path.Combine(root, "temporary"),
                MaximumTemporaryBytes = 50_000_000
            });

            PdfInspection firstInspection = await client.InspectAsync(new PdfInput(first));
            firstInspection.PageCount.Should().Be(3);

            string selected = Path.Combine(root, "selected.pdf");
            await client.ExtractAsync(new PdfInput(first), "2-3", selected);
            (await client.InspectAsync(new PdfInput(selected))).PageCount.Should().Be(2);

            string merged = Path.Combine(root, "merged.pdf");
            await client.MergeAsync(new[]
            {
                new PdfMergeSource(new PdfInput(first), "1-2"),
                new PdfMergeSource(new PdfInput(second), "1-z")
            }, merged);
            (await client.InspectAsync(new PdfInput(merged))).PageCount.Should().Be(4);

            IReadOnlyList<string> split = await client.SplitAsync(new PdfInput(merged), Path.Combine(root, "split"), pagesPerFile: 2);
            split.Should().HaveCount(2);
            foreach (string splitFile in split)
                (await client.InspectAsync(new PdfInput(splitFile))).PageCount.Should().Be(2);

            string overlay = Path.Combine(root, "overlay.pdf");
            await client.OverlayAsync(new PdfInput(first), new PdfInput(second), overlay);
            (await client.InspectAsync(new PdfInput(overlay))).PageCount.Should().Be(3);

            string underlay = Path.Combine(root, "underlay.pdf");
            await client.UnderlayAsync(new PdfInput(first), new PdfInput(second), underlay);
            (await client.InspectAsync(new PdfInput(underlay))).PageCount.Should().Be(3);

            string attached = Path.Combine(root, "attached.pdf");
            await client.AddAttachmentAsync(new PdfInput(first), new PdfAttachment(attachment, "evidence.txt"), attached);
            (await client.InspectAsync(new PdfInput(attached))).AttachmentNames.Should().Contain("evidence.txt");

            string encrypted = Path.Combine(root, "encrypted.pdf");
            await client.EncryptAsync(new PdfInput(first), encrypted, new PdfEncryptionOptions
            {
                UserPassword = "reader-password",
                OwnerPassword = "owner-password",
                Print = PdfPrintPermission.LowResolution,
                Modify = PdfModifyPermission.None,
                AllowExtraction = false
            });
            (await client.InspectAsync(new PdfInput(encrypted, "reader-password"))).IsEncrypted.Should().BeTrue();

            string decrypted = Path.Combine(root, "decrypted.pdf");
            await client.DecryptAsync(new PdfInput(encrypted, "owner-password"), decrypted);
            (await client.InspectAsync(new PdfInput(decrypted))).IsEncrypted.Should().BeFalse();

            string linearized = Path.Combine(root, "linearized.pdf");
            await client.LinearizeAsync(new PdfInput(first), linearized);
            (await client.InspectAsync(new PdfInput(linearized))).IsLinearized.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InspectAsync_CancelledToken_IsHonoured()
    {
        string? qpdf = FindQpdf();
        if (qpdf == null)
            return;
        string input = CreateDocument(1);
        try
        {
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var client = new PdfOperationsClient(new QpdfBackendOptions { QpdfPath = qpdf });

            Func<Task> action = () => client.InspectAsync(new PdfInput(input), cancellation.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(input);
        }
    }

    private static string CreateDocument(int pages, string? path = null)
    {
        path ??= Path.Combine(Path.GetTempPath(), $"PdfBuilder-operations-input-{Guid.NewGuid():N}.pdf");
        PdfDocument document = PdfDocument.Create(descriptor =>
        {
            for (int page = 1; page <= pages; page++)
            {
                int number = page;
                descriptor.Page(current => current.Content().Text($"Operations fixture page {number}"));
            }
        });
        document.Save(path);
        return path;
    }

    private static string? FindQpdf()
    {
        string? configured = Environment.GetEnvironmentVariable("PDFBUILDER_QPDF_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        string windows = @"C:\Program Files\qpdf 12.3.2\bin\qpdf.exe";
        if (File.Exists(windows))
            return windows;
        const string unix = "/usr/bin/qpdf";
        return File.Exists(unix) ? unix : null;
    }
}
