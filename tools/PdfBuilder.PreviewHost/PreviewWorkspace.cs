using PdfBuilder.Document;
using PdfBuilder.Document.Layout;
using PdfBuilder.Models;

internal sealed class PreviewWorkspace
{
    private readonly object _sync = new();
    private readonly Dictionary<(int Page, int Dpi, bool Guides), byte[]> _previews = new();
    private PdfDocument _document = null!;
    private PdfDocument _cleanDocument = null!;
    private byte[] _pdf = Array.Empty<byte>();

    public PreviewWorkspace() => Reload();

    public void Reload()
    {
        PdfDocument document = PreviewDocumentFactory.Create(visualGuides: true);
        PdfDocument cleanDocument = PreviewDocumentFactory.Create(visualGuides: false);
        byte[] pdf = document.GenerateBytes();
        lock (_sync)
        {
            _document = document;
            _cleanDocument = cleanDocument;
            _pdf = pdf;
            _previews.Clear();
        }
    }

    public PreviewManifest GetManifest()
    {
        lock (_sync)
        {
            IReadOnlyList<PreviewPageInfo> pages = _document.Pages
                .Select((page, index) => new PreviewPageInfo(
                    index + 1,
                    page.Width,
                    page.Height,
                    page.MarginLeft,
                    page.MarginTop,
                    page.MarginRight,
                    page.MarginBottom))
                .ToArray();
            return new PreviewManifest(
                pages,
                _pdf.LongLength,
                _document.LastGenerationMetrics,
                _document.ProfilerSession.Snapshot(),
                _document.LayoutTrace.Entries.Count,
                DateTimeOffset.UtcNow);
        }
    }

    public byte[] GetPdf()
    {
        lock (_sync)
            return _pdf.ToArray();
    }

    public byte[] GetPreview(int pageNumber, int dpi, bool guides, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (pageNumber < 1 || pageNumber > _document.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "The requested one-based page does not exist.");
            if (_previews.TryGetValue((pageNumber, dpi, guides), out byte[]? cached))
                return cached.ToArray();

            PdfDocument source = guides ? _document : _cleanDocument;
            byte[] rendered = source.GeneratePreviewImages(dpi, new[] { pageNumber }, cancellationToken).Single().ImageData;
            _previews[(pageNumber, dpi, guides)] = rendered;
            return rendered.ToArray();
        }
    }

    public IReadOnlyList<PdfLayoutTraceEntry> GetTrace()
    {
        lock (_sync)
            return _document.LayoutTrace.Entries;
    }

    public IReadOnlyList<PreviewHierarchyNode> GetHierarchy()
    {
        lock (_sync)
            return PreviewHierarchyNode.Build(_document.LayoutTrace.Entries);
    }
}

internal sealed record PreviewManifest(
    IReadOnlyList<PreviewPageInfo> Pages,
    long PdfBytes,
    PdfGenerationMetrics? Generation,
    LayoutProfileSnapshot Timing,
    int TraceEvents,
    DateTimeOffset GeneratedUtc);

internal sealed record PreviewPageInfo(
    int Number,
    float Width,
    float Height,
    float MarginLeft,
    float MarginTop,
    float MarginRight,
    float MarginBottom);

internal sealed class PreviewHierarchyNode
{
    private readonly SortedDictionary<string, PreviewHierarchyNode> _children = new(StringComparer.Ordinal);

    private PreviewHierarchyNode(string name) => Name = name;

    public string Name { get; }
    public int Events { get; private set; }
    public IReadOnlyCollection<PreviewHierarchyNode> Children => _children.Values;

    public static IReadOnlyList<PreviewHierarchyNode> Build(IEnumerable<PdfLayoutTraceEntry> entries)
    {
        var root = new PreviewHierarchyNode("Document");
        foreach (PdfLayoutTraceEntry entry in entries)
        {
            PreviewHierarchyNode current = root;
            IEnumerable<string> parts = entry.ComponentPath
                .Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts.SkipWhile(part => part == "Document"))
            {
                if (!current._children.TryGetValue(part, out PreviewHierarchyNode? child))
                {
                    child = new PreviewHierarchyNode(part);
                    current._children.Add(part, child);
                }
                current = child;
            }
            current.Events++;
        }
        return new[] { root };
    }
}

internal sealed record StructuredPreviewError(
    string Type,
    string Message,
    string? ComponentPath,
    int? PageNumber,
    IReadOnlyList<string> SuggestedActions)
{
    public static StructuredPreviewError From(Exception exception)
    {
        if (exception is PdfLayoutException layout)
        {
            return new StructuredPreviewError(
                layout.GetType().Name,
                layout.Message,
                layout.Context.ComponentPath,
                layout.Context.PageNumber,
                layout.Context.SuggestedActions);
        }

        return new StructuredPreviewError(
            exception.GetType().Name,
            exception.Message,
            null,
            null,
            Array.Empty<string>());
    }
}
