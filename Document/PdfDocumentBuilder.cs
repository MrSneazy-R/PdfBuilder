using PdfBuilder.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfBuilder.Document
{
    public interface IPdfComposable
    {
        void Compose(PdfDocumentBuilder builder);
    }

   public class PdfDocumentBuilder
{
    private readonly List<PdfPage> _pages = new();

    public PdfDocumentBuilder Page(Action<PdfPageBuilder> pageAction)
    {
        var page = new PdfPage(595, 842); // A4
        var builder = new PdfPageBuilder(page);
        pageAction(builder);
        _pages.Add(builder.Build());
        return this;
    }

    public PdfDocument Build() => new PdfDocument(_pages);
}


}
