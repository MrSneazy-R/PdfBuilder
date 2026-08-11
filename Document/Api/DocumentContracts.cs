using PdfBuilder.Models;

namespace PdfBuilder.Document;

/// <summary>Describes a document composed through PdfBuilder's canonical API.</summary>
public interface IDocumentDescriptor
{
    /// <summary>Configures document metadata.</summary>
    void Metadata(Action<DocumentMetadata> configure);

    /// <summary>Configures document-scoped colors, text styles, and spacing tokens.</summary>
    void Theme(Action<DocumentThemeBuilder> configure);

    /// <summary>Configures optional layout diagnostics before pages are composed.</summary>
    void Diagnostics(Action<Layout.PdfDiagnosticsOptions> configure);

    /// <summary>Adds and configures a page.</summary>
    void Page(Action<IPageDescriptor> configure);
}
