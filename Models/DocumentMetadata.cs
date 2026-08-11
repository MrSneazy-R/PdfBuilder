using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace PdfBuilder.Models;

/// <summary>Standard document information plus optional language and custom XMP metadata.</summary>
public sealed partial class DocumentMetadata
{
    /// <summary>Gets or sets the document title.</summary>
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public string? Creator { get; set; }
    public string? Producer { get; set; }
    /// <summary>Gets or sets the document language as a BCP 47 language tag.</summary>
    public string? Language { get; set; }
    /// <summary>Gets or sets a complete, well-formed custom XMP XML packet.</summary>
    public string? CustomXmp { get; set; }
    public DateTimeOffset? CreatedUtc { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>Validates metadata using the library's default metadata and XMP limits.</summary>
    public void Validate() => Validate(1_000_000, 1_000_000);

    internal void Validate(int maximumMetadataCharacters, int maximumXmpBytes)
    {
        if (maximumMetadataCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMetadataCharacters));
        if (maximumXmpBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumXmpBytes));

        int characters = CheckedLength(Title) + CheckedLength(Author) + CheckedLength(Subject)
            + CheckedLength(Keywords) + CheckedLength(Creator) + CheckedLength(Producer) + CheckedLength(Language);
        if (characters > maximumMetadataCharacters)
            throw new ArgumentException($"Document metadata contains {characters} characters, exceeding the configured maximum of {maximumMetadataCharacters}.");

        if (Language != null && !LanguageTagExpression().IsMatch(Language))
            throw new ArgumentException("Document language must be a valid BCP 47-style language tag such as 'en', 'en-ZA', or 'zh-Hans-CN'.", nameof(Language));

        if (CustomXmp == null)
            return;
        int xmpBytes = Encoding.UTF8.GetByteCount(CustomXmp);
        if (xmpBytes > maximumXmpBytes)
            throw new ArgumentException($"Custom XMP contains {xmpBytes} bytes, exceeding the configured maximum of {maximumXmpBytes}.", nameof(CustomXmp));
        try
        {
            using var reader = XmlReader.Create(
                new StringReader(CustomXmp),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = maximumXmpBytes,
                    MaxCharactersFromEntities = 0
                });
            while (reader.Read()) { }
        }
        catch (XmlException exception)
        {
            throw new ArgumentException("Custom XMP must be well-formed XML without a DTD or external resources.", nameof(CustomXmp), exception);
        }
    }

    public void CopyFrom(DocumentMetadata other)
    {
        if (other == null) return;
        Title = other.Title;
        Author = other.Author;
        Subject = other.Subject;
        Keywords = other.Keywords;
        Creator = other.Creator;
        Producer = other.Producer;
        Language = other.Language;
        CustomXmp = other.CustomXmp;
        CreatedUtc = other.CreatedUtc;
        ModifiedUtc = other.ModifiedUtc;
    }

    public DocumentMetadata Clone()
    {
        var clone = new DocumentMetadata();
        clone.CopyFrom(this);
        return clone;
    }

    private static int CheckedLength(string? value) => value?.Length ?? 0;

    [GeneratedRegex("^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex LanguageTagExpression();
}
