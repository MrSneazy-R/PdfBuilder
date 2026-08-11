using PdfBuilder.Models;

namespace PdfBuilder.Document;

internal static class PageContextFactory
{
    internal static PageContext Create(PdfPage page, int currentPage, int totalPages, HeaderFooterSpec? spec)
    {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (currentPage <= 0) throw new ArgumentOutOfRangeException(nameof(currentPage));
        if (totalPages < currentPage) throw new ArgumentOutOfRangeException(nameof(totalPages));

        bool suppressRepeatedContent = spec?.FirstPageDifferent == true && currentPage == 1;
        bool headerVisible = !suppressRepeatedContent
            && spec != null
            && (spec.HeaderLayout != null || !string.IsNullOrWhiteSpace(spec.HeaderTemplate));
        bool footerVisible = !suppressRepeatedContent
            && !(spec?.HideOnLastPage == true && currentPage == totalPages)
            && spec != null
            && (spec.FooterLayout != null || !string.IsNullOrWhiteSpace(spec.FooterTemplate));

        float availableWidth = Math.Max(0f, page.Width - page.MarginLeft - page.MarginRight);
        float availableHeight = Math.Max(
            0f,
            page.Height
                - page.MarginTop
                - page.MarginBottom
                - (headerVisible ? Math.Max(0f, spec!.HeaderHeight) : 0f)
                - (footerVisible ? Math.Max(0f, spec!.FooterHeight) : 0f));

        return new PageContext(
            currentPage,
            totalPages,
            page.Width,
            page.Height,
            availableWidth,
            availableHeight);
    }
}
