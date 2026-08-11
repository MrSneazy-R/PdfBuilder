namespace PdfBuilder.Document;

public partial class PdfDocument
{
    private sealed class CanonicalTaggedPdfDescriptor : ITaggedPdfDescriptor
    {
        private readonly PdfDocument _document;
        internal CanonicalTaggedPdfDescriptor(PdfDocument document) => _document = document;
        public void Enabled(bool value = true) => _document.Tagging.Enabled = value;
        public void Language(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("A document language is required.", nameof(language));
            _document.Metadata.Language = language;
            _document.Tagging.Enabled = true;
        }
        public void RoleMap(string customRole, string standardRole)
            => _document.Tagging.MapRole(customRole, standardRole);
    }
}
