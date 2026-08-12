namespace PdfBuilder.Operations;

internal sealed class TemporaryWorkspace : IDisposable
{
    private readonly QpdfBackendOptions _options;
    private bool _disposed;

    internal TemporaryWorkspace(QpdfBackendOptions options)
    {
        _options = options;
        string root = string.IsNullOrWhiteSpace(options.TemporaryRoot)
            ? Path.Combine(Path.GetTempPath(), "PdfBuilder.Operations")
            : Path.GetFullPath(options.TemporaryRoot);
        Directory.CreateDirectory(root);
        DirectoryPath = Path.Combine(root, $"operation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    internal string DirectoryPath { get; }

    internal string NewPdfPath(string name)
    {
        string safeName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A safe PDF file name is required.", nameof(name));
        return Path.Combine(DirectoryPath, safeName);
    }

    internal void EnforceBounds()
    {
        FileInfo[] files = new DirectoryInfo(DirectoryPath).GetFiles("*", SearchOption.AllDirectories);
        if (files.Length > _options.MaximumFiles)
            throw new PdfOperationsException($"The operation produced {files.Length} temporary files, exceeding the configured maximum of {_options.MaximumFiles}.");
        long bytes = files.Sum(file => file.Length);
        if (bytes > _options.MaximumTemporaryBytes)
            throw new PdfOperationsException($"The operation produced {bytes} temporary bytes, exceeding the configured maximum of {_options.MaximumTemporaryBytes}.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
        catch
        {
            // Cleanup is best effort; a process-level file lock may outlive cancellation briefly.
        }
    }
}
