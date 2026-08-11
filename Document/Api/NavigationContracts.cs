namespace PdfBuilder.Document;

/// <summary>Configures canonical section navigation behavior.</summary>
public interface ISectionDescriptor
{
    /// <summary>Sets the outline and table-of-contents nesting level.</summary>
    void Level(int value);
    /// <summary>Enables or disables hierarchical section numbering.</summary>
    void Numbered(bool enabled = true);
    /// <summary>Starts the section on a new page when prior content exists.</summary>
    void StartOnNewPage(bool enabled = true);
    /// <summary>Includes or excludes the section from PDF outlines.</summary>
    void IncludeInOutline(bool enabled = true);
    /// <summary>Includes or excludes the section from canonical tables of contents.</summary>
    void IncludeInTableOfContents(bool enabled = true);
}

/// <summary>Configures a canonical table of contents.</summary>
public interface ITableOfContentsDescriptor
{
    /// <summary>Includes hierarchical section numbers in entry labels.</summary>
    void IncludeSectionNumbers(bool enabled = true);
    /// <summary>Sets indentation per nested section level in points.</summary>
    void IndentPerLevel(float value);
    /// <summary>Sets the format used for resolved page numbers.</summary>
    void PageNumberFormat(string format);
    /// <summary>Sets placeholder text used before page references resolve.</summary>
    void PendingPageText(string value);
}
