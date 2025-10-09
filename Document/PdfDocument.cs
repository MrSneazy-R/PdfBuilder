using PdfBuilder.Models;
using System.Collections.Generic;
using System.Linq;


namespace PdfBuilder.Document
{
    /// <summary>
    /// Internal representation of a PDF document. 
    /// Use PdfDocumentBuilder for fluent API creation.
    /// </summary>
    public class PdfDocument
    {
        public List<PdfPage> Pages { get; } = new();

        public PdfDocument() { }

        public PdfDocument(List<PdfPage> pages)
        {
            Pages = pages;
        }

        public PdfPage AddPage(float width = 612f, float height = 792f)
        {
            var page = new PdfPage(width, height);
            Pages.Add(page);
            return page;
        }
        public string? Title { get; set; } = null;
        public HeaderFooterSpec HeaderFooter { get; set; } = new();
        public MasterPageSpec Master { get; set; } = new();
    }

}

