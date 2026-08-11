using PdfBuilder.Models;

namespace PdfBuilder.Document;

/// <summary>Describes a document composed through PdfBuilder's canonical API.</summary>
public interface IDocumentDescriptor
{
    /// <summary>Configures document metadata.</summary>
    void Metadata(Action<DocumentMetadata> configure);

    /// <summary>Applies a coherent output preset.</summary>
    void OutputPreset(PdfOutputPreset preset);

    /// <summary>Configures PDF output encoding and version options.</summary>
    void Output(Action<PdfOutputOptions> configure);

    /// <summary>Configures deterministic generation and document identity.</summary>
    void Generation(Action<PdfGenerationOptions> configure);

    /// <summary>Configures document-scoped colors, text styles, and spacing tokens.</summary>
    void Theme(Action<DocumentThemeBuilder> configure);

    /// <summary>Configures optional layout diagnostics before pages are composed.</summary>
    void Diagnostics(Action<Layout.PdfDiagnosticsOptions> configure);

    /// <summary>Configures marked content and the document structure tree without claiming PDF/UA conformance.</summary>
    void Tagged(Action<ITaggedPdfDescriptor> configure);

    /// <summary>Configures bounded rendering and pagination limits before canonical pages are composed.</summary>
    void RenderLimits(Action<Layout.PdfRenderLimits> configure);

    /// <summary>Adds and configures a page.</summary>
    void Page(Action<IPageDescriptor> configure);
}
