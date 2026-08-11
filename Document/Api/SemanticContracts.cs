namespace PdfBuilder.Document;

/// <summary>Configures document-level tagged-PDF output.</summary>
public interface ITaggedPdfDescriptor
{
    /// <summary>Enables or disables tagged output.</summary>
    void Enabled(bool value = true);
    /// <summary>Sets the required BCP 47 document language.</summary>
    void Language(string language);
    /// <summary>Maps a custom structure role to a standard PDF structure role.</summary>
    void RoleMap(string customRole, string standardRole);
}
