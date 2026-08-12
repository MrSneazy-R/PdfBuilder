using System.Security;
using PdfBuilder.Models;

namespace PdfBuilder.Compliance;

internal static class ComplianceXmpBuilder
{
    internal static string Build(PdfComplianceProfile profile, DocumentMetadata metadata)
    {
        string profileDescription = profile switch
        {
            PdfComplianceProfile.PdfA2B => "xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\" pdfaid:part=\"2\" pdfaid:conformance=\"B\"",
            PdfComplianceProfile.PdfA3B => "xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\" pdfaid:part=\"3\" pdfaid:conformance=\"B\"",
            PdfComplianceProfile.PdfUa1 => "xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\" pdfuaid:part=\"1\"",
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        string title = SecurityElement.Escape(metadata.Title ?? string.Empty) ?? string.Empty;
        string creator = SecurityElement.Escape(metadata.Author ?? string.Empty) ?? string.Empty;
        string language = SecurityElement.Escape(metadata.Language ?? string.Empty) ?? string.Empty;
        string creatorTool = SecurityElement.Escape(metadata.Creator ?? "PdfBuilder") ?? "PdfBuilder";
        string producer = SecurityElement.Escape(metadata.Producer ?? "PdfBuilder") ?? "PdfBuilder";
        string created = metadata.CreatedUtc?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        string modified = metadata.ModifiedUtc?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture) ?? created;
        return $"<?xpacket begin='﻿' id='W5M0MpCehiHzreSzNTczkc9d'?><x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' {profileDescription} xmlns:dc='http://purl.org/dc/elements/1.1/' xmlns:xmp='http://ns.adobe.com/xap/1.0/' xmlns:pdf='http://ns.adobe.com/pdf/1.3/' xmp:CreatorTool='{creatorTool}' xmp:CreateDate='{created}' xmp:ModifyDate='{modified}' xmp:MetadataDate='{modified}' pdf:Producer='{producer}'><dc:format>application/pdf</dc:format><dc:title><rdf:Alt><rdf:li xml:lang='x-default'>{title}</rdf:li></rdf:Alt></dc:title><dc:creator><rdf:Seq><rdf:li>{creator}</rdf:li></rdf:Seq></dc:creator><dc:language><rdf:Bag><rdf:li>{language}</rdf:li></rdf:Bag></dc:language></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end='w'?>";
    }
}
