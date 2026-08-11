namespace PdfBuilder.Document;

/// <summary>Describes one coordinate-free document page.</summary>
public interface IPageDescriptor
{
    /// <summary>Sets the page size in points.</summary>
    void Size(PageSize size);
    /// <summary>Sets the page orientation.</summary>
    void Orientation(PageOrientation orientation);
    /// <summary>Sets a uniform page margin in points.</summary>
    void Margin(float value);
    /// <summary>Sets page margins in points.</summary>
    void Margin(float left, float top, float right, float bottom);
    /// <summary>Configures the default text style for page content.</summary>
    void DefaultTextStyle(Action<ITextStyleDescriptor> configure);
    /// <summary>Returns the root content container.</summary>
    IContainer Content();
    /// <summary>Returns the repeating header container.</summary>
    IContainer Header();
    /// <summary>Returns the repeating footer container.</summary>
    IContainer Footer();
    /// <summary>Returns the page background container.</summary>
    IContainer Background();
    /// <summary>Suppresses the canonical header and footer on this page when it is the document's first page.</summary>
    void FirstPageDifferent();
    /// <summary>Suppresses the canonical footer when this page is the document's last page.</summary>
    void HideFooterOnLastPage();
    /// <summary>Configures equal-width content columns and their gutter in points.</summary>
    void Columns(int count, float gutter = 14f);
}

/// <summary>Describes immutable values resolved from the final pagination result.</summary>
public sealed class PageContext
{
    internal PageContext(
        int currentPage,
        int totalPages,
        float pageWidth,
        float pageHeight,
        float availableContentWidth,
        float availableContentHeight)
    {
        CurrentPage = currentPage;
        TotalPages = totalPages;
        PageWidth = pageWidth;
        PageHeight = pageHeight;
        AvailableContentWidth = availableContentWidth;
        AvailableContentHeight = availableContentHeight;
    }

    /// <summary>Gets the one-based current page number.</summary>
    public int CurrentPage { get; }
    /// <summary>Gets the final total page count.</summary>
    public int TotalPages { get; }
    /// <summary>Gets whether this is the first page.</summary>
    public bool IsFirstPage => CurrentPage == 1;
    /// <summary>Gets whether this is the final page.</summary>
    public bool IsLastPage => CurrentPage == TotalPages;
    /// <summary>Gets whether the current one-based page number is odd.</summary>
    public bool IsOddPage => (CurrentPage & 1) == 1;
    /// <summary>Gets whether the current one-based page number is even.</summary>
    public bool IsEvenPage => !IsOddPage;
    /// <summary>Gets the physical page width in points.</summary>
    public float PageWidth { get; }
    /// <summary>Gets the physical page height in points.</summary>
    public float PageHeight { get; }
    /// <summary>Gets the usable content width in points after page margins.</summary>
    public float AvailableContentWidth { get; }
    /// <summary>Gets the usable content height in points after margins and visible repeated bands.</summary>
    public float AvailableContentHeight { get; }
}

/// <summary>Provides canonical placeholders resolved from final pagination.</summary>
public static class PageTextTokens
{
    /// <summary>Represents the one-based current page number.</summary>
    public const string CurrentPage = "{page}";
    /// <summary>Represents the final total page count.</summary>
    public const string TotalPages = "{pages}";
}

/// <summary>Defines the orientation applied to a page size.</summary>
public enum PageOrientation { Portrait, Landscape }

/// <summary>Represents a PDF page size in points.</summary>
public readonly record struct PageSize(float Width, float Height)
{
    /// <summary>Returns this size rotated to the requested orientation.</summary>
    public PageSize WithOrientation(PageOrientation orientation) => orientation == PageOrientation.Landscape
        ? (Width >= Height ? this : new PageSize(Height, Width))
        : (Height >= Width ? this : new PageSize(Height, Width));
}

/// <summary>Provides common page sizes in PDF points.</summary>
public static class PageSizes
{
    /// <summary>ISO A4 (595 × 842 points).</summary>
    public static PageSize A4 => new(595f, 842f);
    /// <summary>US Letter (612 × 792 points).</summary>
    public static PageSize Letter => new(612f, 792f);
    /// <summary>ISO A3 (842 × 1191 points).</summary>
    public static PageSize A3 => new(842f, 1191f);
}
