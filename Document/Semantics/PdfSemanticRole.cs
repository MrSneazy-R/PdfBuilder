namespace PdfBuilder.Document;

/// <summary>Semantic roles available to the tagged-PDF structure tree.</summary>
public enum PdfSemanticRole
{
    Document,
    Section,
    Heading,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,
    Paragraph,
    List,
    ListItem,
    Label,
    ListBody,
    Table,
    TableRow,
    TableHeaderCell,
    TableCell,
    TableHead,
    TableBody,
    TableFoot,
    Header,
    Footer,
    Figure,
    Caption,
    Link,
    Span,
    Quote,
    Note,
    Code
}

internal static class PdfSemanticRoleNames
{
    internal static string PdfName(PdfSemanticRole role) => role switch
    {
        PdfSemanticRole.Document => "Document",
        PdfSemanticRole.Section => "Sect",
        PdfSemanticRole.Heading => "H",
        PdfSemanticRole.Heading1 => "H1",
        PdfSemanticRole.Heading2 => "H2",
        PdfSemanticRole.Heading3 => "H3",
        PdfSemanticRole.Heading4 => "H4",
        PdfSemanticRole.Heading5 => "H5",
        PdfSemanticRole.Heading6 => "H6",
        PdfSemanticRole.Paragraph => "P",
        PdfSemanticRole.List => "L",
        PdfSemanticRole.ListItem => "LI",
        PdfSemanticRole.Label => "Lbl",
        PdfSemanticRole.ListBody => "LBody",
        PdfSemanticRole.Table => "Table",
        PdfSemanticRole.TableRow => "TR",
        PdfSemanticRole.TableHeaderCell => "TH",
        PdfSemanticRole.TableCell => "TD",
        PdfSemanticRole.TableHead => "THead",
        PdfSemanticRole.TableBody => "TBody",
        PdfSemanticRole.TableFoot => "TFoot",
        PdfSemanticRole.Header => "Header",
        PdfSemanticRole.Footer => "Footer",
        PdfSemanticRole.Figure => "Figure",
        PdfSemanticRole.Caption => "Caption",
        PdfSemanticRole.Link => "Link",
        PdfSemanticRole.Span => "Span",
        PdfSemanticRole.Quote => "Quote",
        PdfSemanticRole.Note => "Note",
        PdfSemanticRole.Code => "Code",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}
