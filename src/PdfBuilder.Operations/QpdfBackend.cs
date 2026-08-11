using System.Text.Json;
using System.Text.RegularExpressions;

namespace PdfBuilder.Operations;

/// <summary>Direct, no-shell qpdf backend for operations on existing PDF files.</summary>
public sealed partial class QpdfBackend : IPdfOperationsBackend
{
    private readonly QpdfBackendOptions _options;
    private readonly QpdfProcessRunner _runner;

    public QpdfBackend(QpdfBackendOptions? options = null)
    {
        _options = options ?? new QpdfBackendOptions();
        _runner = new QpdfProcessRunner(_options);
    }

    public async Task<PdfInspection> InspectAsync(PdfInput input, CancellationToken cancellationToken = default)
    {
        string inputPath = ValidateInput(input);
        using var workspace = new TemporaryWorkspace(_options);
        var arguments = new List<string> { "--json" };
        AddPassword(arguments, input.Password);
        arguments.Add(inputPath);
        QpdfProcessResult result = await _runner.RunAsync(arguments, workspace.DirectoryPath, cancellationToken).ConfigureAwait(false);
        return ParseInspection(inputPath, result.StandardOutput);
    }

    public async Task SelectPagesAsync(PdfInput input, string pages, string outputPath, CancellationToken cancellationToken = default)
    {
        string inputPath = ValidateInput(input);
        string range = ValidatePageSelection(pages);
        using var workspace = new TemporaryWorkspace(_options);
        string temporaryOutput = workspace.NewPdfPath("selected.pdf");
        var arguments = new List<string> { "--empty", "--pages", inputPath };
        AddPassword(arguments, input.Password);
        arguments.Add(range);
        arguments.Add("--");
        arguments.Add(temporaryOutput);
        await ExecuteAndCommitAsync(arguments, workspace, temporaryOutput, outputPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task MergeAsync(IReadOnlyList<PdfMergeSource> sources, string outputPath, CancellationToken cancellationToken = default)
    {
        if (sources == null) throw new ArgumentNullException(nameof(sources));
        if (sources.Count == 0) throw new ArgumentException("At least one merge source is required.", nameof(sources));
        ValidateFileCount(sources.Count);

        using var workspace = new TemporaryWorkspace(_options);
        string temporaryOutput = workspace.NewPdfPath("merged.pdf");
        var arguments = new List<string> { "--empty", "--pages" };
        foreach (PdfMergeSource source in sources)
        {
            if (source?.Input == null) throw new ArgumentException("Merge sources cannot be null.", nameof(sources));
            arguments.Add(ValidateInput(source.Input));
            AddPassword(arguments, source.Input.Password);
            arguments.Add(ValidatePageSelection(source.Pages));
        }
        arguments.Add("--");
        arguments.Add(temporaryOutput);
        await ExecuteAndCommitAsync(arguments, workspace, temporaryOutput, outputPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> SplitAsync(
        PdfInput input,
        string outputDirectory,
        int pagesPerFile = 1,
        CancellationToken cancellationToken = default)
    {
        if (pagesPerFile <= 0) throw new ArgumentOutOfRangeException(nameof(pagesPerFile));
        string inputPath = ValidateInput(input);
        PdfInspection inspection = await InspectAsync(input, cancellationToken).ConfigureAwait(false);
        int outputCount = (int)Math.Ceiling(inspection.PageCount / (double)pagesPerFile);
        ValidateFileCount(outputCount);
        string destination = ValidateOutputDirectory(outputDirectory);

        using var workspace = new TemporaryWorkspace(_options);
        var staged = new List<(string Temporary, string Destination)>(outputCount);
        for (int index = 0; index < outputCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int first = index * pagesPerFile + 1;
            int last = Math.Min(inspection.PageCount, first + pagesPerFile - 1);
            string range = first == last ? first.ToString() : $"{first}-{last}";
            string fileName = $"{Path.GetFileNameWithoutExtension(inputPath)}-{index + 1:0000}.pdf";
            string temporaryOutput = workspace.NewPdfPath(fileName);
            var arguments = new List<string> { "--empty", "--pages", inputPath };
            AddPassword(arguments, input.Password);
            arguments.Add(range);
            arguments.Add("--");
            arguments.Add(temporaryOutput);
            await ExecuteToTemporaryAsync(arguments, workspace, temporaryOutput, cancellationToken).ConfigureAwait(false);
            staged.Add((temporaryOutput, Path.Combine(destination, fileName)));
        }

        foreach ((string temporary, string target) in staged)
            Commit(temporary, target);
        return staged.Select(item => item.Destination).ToArray();
    }

    public Task OverlayAsync(PdfInput input, PdfInput overlay, string outputPath, CancellationToken cancellationToken = default)
        => ApplyLayerAsync(input, overlay, outputPath, "--overlay", cancellationToken);

    public Task UnderlayAsync(PdfInput input, PdfInput underlay, string outputPath, CancellationToken cancellationToken = default)
        => ApplyLayerAsync(input, underlay, outputPath, "--underlay", cancellationToken);

    public async Task AddAttachmentAsync(
        PdfInput input,
        PdfAttachment attachment,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (attachment == null) throw new ArgumentNullException(nameof(attachment));
        string attachmentPath = Path.GetFullPath(attachment.Path);
        if (!File.Exists(attachmentPath)) throw new FileNotFoundException("The attachment file does not exist.", attachmentPath);
        ValidateSingleLine(attachment.DisplayName, nameof(attachment.DisplayName));

        using var workspace = new TemporaryWorkspace(_options);
        string temporaryOutput = workspace.NewPdfPath("attached.pdf");
        var arguments = InputArguments(input);
        arguments.Add("--add-attachment");
        arguments.Add(attachmentPath);
        if (!string.IsNullOrWhiteSpace(attachment.DisplayName))
            arguments.Add($"--filename={attachment.DisplayName}");
        arguments.Add("--");
        arguments.Add(temporaryOutput);
        await ExecuteAndCommitAsync(arguments, workspace, temporaryOutput, outputPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task EncryptAsync(
        PdfInput input,
        string outputPath,
        PdfEncryptionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrEmpty(options.OwnerPassword))
            throw new ArgumentException("A non-empty owner password is required for encryption.", nameof(options));
        ValidateSingleLine(options.UserPassword, nameof(options.UserPassword));
        ValidateSingleLine(options.OwnerPassword, nameof(options.OwnerPassword));

        using var workspace = new TemporaryWorkspace(_options);
        string temporaryOutput = workspace.NewPdfPath("encrypted.pdf");
        var arguments = InputArguments(input);
        arguments.Add("--encrypt");
        arguments.Add(options.UserPassword);
        arguments.Add(options.OwnerPassword);
        arguments.Add("256");
        arguments.Add($"--print={PrintValue(options.Print)}");
        arguments.Add($"--modify={ModifyValue(options.Modify)}");
        arguments.Add($"--extract={YesNo(options.AllowExtraction)}");
        arguments.Add($"--accessibility={YesNo(options.AllowAccessibility)}");
        arguments.Add("--");
        arguments.Add(temporaryOutput);
        await ExecuteAndCommitAsync(arguments, workspace, temporaryOutput, outputPath, cancellationToken, options.OwnerPassword).ConfigureAwait(false);
    }

    public Task DecryptAsync(PdfInput input, string outputPath, CancellationToken cancellationToken = default)
        => TransformAsync(input, outputPath, "decrypted.pdf", new[] { "--decrypt" }, cancellationToken);

    public Task LinearizeAsync(PdfInput input, string outputPath, CancellationToken cancellationToken = default)
        => TransformAsync(input, outputPath, "linearized.pdf", new[] { "--linearize" }, cancellationToken);

    private async Task ApplyLayerAsync(
        PdfInput input,
        PdfInput layer,
        string outputPath,
        string operation,
        CancellationToken cancellationToken)
    {
        string layerPath = ValidateInput(layer);
        using var workspace = new TemporaryWorkspace(_options);
        string temporaryOutput = workspace.NewPdfPath("layered.pdf");
        var arguments = InputArguments(input);
        arguments.Add(operation);
        arguments.Add(layerPath);
        AddPassword(arguments, layer.Password);
        arguments.Add("--repeat=1-z");
        arguments.Add("--");
        arguments.Add(temporaryOutput);
        await ExecuteAndCommitAsync(arguments, workspace, temporaryOutput, outputPath, cancellationToken).ConfigureAwait(false);
    }

    private async Task TransformAsync(
        PdfInput input,
        string outputPath,
        string temporaryName,
        IReadOnlyList<string> operationArguments,
        CancellationToken cancellationToken)
    {
        using var workspace = new TemporaryWorkspace(_options);
        string temporaryOutput = workspace.NewPdfPath(temporaryName);
        var arguments = InputArguments(input);
        arguments.AddRange(operationArguments);
        arguments.Add(temporaryOutput);
        await ExecuteAndCommitAsync(arguments, workspace, temporaryOutput, outputPath, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAndCommitAsync(
        IReadOnlyList<string> arguments,
        TemporaryWorkspace workspace,
        string temporaryOutput,
        string outputPath,
        CancellationToken cancellationToken,
        string? validationPassword = null)
    {
        await ExecuteToTemporaryAsync(arguments, workspace, temporaryOutput, cancellationToken, validationPassword).ConfigureAwait(false);
        Commit(temporaryOutput, ValidateOutputPath(outputPath));
    }

    private async Task ExecuteToTemporaryAsync(
        IReadOnlyList<string> arguments,
        TemporaryWorkspace workspace,
        string temporaryOutput,
        CancellationToken cancellationToken,
        string? validationPassword = null)
    {
        await _runner.RunAsync(arguments, workspace.DirectoryPath, cancellationToken).ConfigureAwait(false);
        workspace.EnforceBounds();
        PdfFileValidator.Validate(temporaryOutput, _options.MaximumTemporaryBytes);
        var validationArguments = new List<string>();
        AddPassword(validationArguments, validationPassword);
        validationArguments.Add("--check");
        validationArguments.Add(temporaryOutput);
        await _runner.RunAsync(validationArguments, workspace.DirectoryPath, cancellationToken).ConfigureAwait(false);
    }

    private void Commit(string temporaryOutput, string outputPath)
    {
        PdfFileValidator.Validate(temporaryOutput, _options.MaximumTemporaryBytes);
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.Copy(temporaryOutput, outputPath, overwrite: true);
    }

    private List<string> InputArguments(PdfInput input)
    {
        string path = ValidateInput(input);
        var arguments = new List<string>();
        AddPassword(arguments, input.Password);
        arguments.Add(path);
        return arguments;
    }

    private static void AddPassword(List<string> arguments, string? password)
    {
        ValidateSingleLine(password, nameof(password));
        if (password != null)
            arguments.Add($"--password={password}");
    }

    private static string ValidateInput(PdfInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (!File.Exists(input.Path)) throw new FileNotFoundException("The input PDF does not exist.", input.Path);
        return input.Path;
    }

    private static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("An output path is required.", nameof(outputPath));
        string path = Path.GetFullPath(outputPath);
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The output path must use a .pdf extension.", nameof(outputPath));
        return path;
    }

    private static string ValidateOutputDirectory(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        string path = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(path);
        return path;
    }

    internal static string ValidatePageSelection(string pages)
    {
        if (string.IsNullOrWhiteSpace(pages) || !PageSelectionPattern().IsMatch(pages))
            throw new ArgumentException("Page selections may contain page numbers, z, r, odd, even, commas, and hyphens only.", nameof(pages));
        return pages;
    }

    private void ValidateFileCount(int count)
    {
        if (_options.MaximumFiles <= 0 || count > _options.MaximumFiles)
            throw new PdfOperationsException($"The operation requests {count} files, exceeding the configured maximum of {_options.MaximumFiles}.");
    }

    private static void ValidateSingleLine(string? value, string parameterName)
    {
        if (value?.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            throw new ArgumentException("The value cannot contain newline or NUL characters.", parameterName);
    }

    private static string YesNo(bool value) => value ? "y" : "n";
    private static string PrintValue(PdfPrintPermission value) => value switch
    {
        PdfPrintPermission.None => "none",
        PdfPrintPermission.LowResolution => "low",
        PdfPrintPermission.Full => "full",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static string ModifyValue(PdfModifyPermission value) => value switch
    {
        PdfModifyPermission.None => "none",
        PdfModifyPermission.Assembly => "assembly",
        PdfModifyPermission.Form => "form",
        PdfModifyPermission.Annotate => "annotate",
        PdfModifyPermission.All => "all",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static PdfInspection ParseInspection(string path, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        int pages = root.TryGetProperty("pages", out JsonElement pageArray) && pageArray.ValueKind == JsonValueKind.Array
            ? pageArray.GetArrayLength()
            : 0;
        string? version = root.TryGetProperty("pdfversion", out JsonElement versionElement)
            ? versionElement.GetString()
            : null;
        bool encrypted = root.TryGetProperty("encrypt", out JsonElement encryptElement)
            && encryptElement.ValueKind == JsonValueKind.Object
            && encryptElement.TryGetProperty("encrypted", out JsonElement encryptedElement)
            && encryptedElement.ValueKind == JsonValueKind.True;
        IReadOnlyList<string> attachments = root.TryGetProperty("attachments", out JsonElement attachmentElement)
            && attachmentElement.ValueKind == JsonValueKind.Object
                ? attachmentElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
        return new PdfInspection(path, pages, version, encrypted, PdfFileValidator.IsLinearized(path), attachments);
    }

    [GeneratedRegex(@"^(?:(?:[1-9][0-9]*|z|r[1-9][0-9]*|odd|even)(?:-(?:[1-9][0-9]*|z|r[1-9][0-9]*))?)(?:,(?:(?:[1-9][0-9]*|z|r[1-9][0-9]*|odd|even)(?:-(?:[1-9][0-9]*|z|r[1-9][0-9]*))?))*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PageSelectionPattern();
}
