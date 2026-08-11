namespace PdfBuilder.Document;

internal enum PageParity
{
    Any,
    Odd,
    Even
}

internal sealed class PageVisibilityRule
{
    internal bool FirstPageOnly { get; private set; }
    internal bool LastPageOnly { get; private set; }
    internal bool SkipFirstPage { get; private set; }
    internal PageParity Parity { get; private set; }

    internal bool IsAllPages => !FirstPageOnly && !LastPageOnly && !SkipFirstPage && Parity == PageParity.Any;
    internal bool RequiresFinalPageCount => LastPageOnly;

    internal void ShowOnce() => FirstPageOnly = true;
    internal void SkipOnce() => SkipFirstPage = true;
    internal void OnFirstPage() => FirstPageOnly = true;
    internal void OnLastPage() => LastPageOnly = true;
    internal void OnOddPages() => Parity = PageParity.Odd;
    internal void OnEvenPages() => Parity = PageParity.Even;
    internal void OnContinuationPages() => SkipFirstPage = true;

    internal bool Matches(int currentPage, int totalPages)
    {
        if (currentPage <= 0 || totalPages < currentPage)
            return false;
        if (FirstPageOnly && currentPage != 1)
            return false;
        if (LastPageOnly && currentPage != totalPages)
            return false;
        if (SkipFirstPage && currentPage == 1)
            return false;
        if (Parity == PageParity.Odd && (currentPage & 1) == 0)
            return false;
        if (Parity == PageParity.Even && (currentPage & 1) != 0)
            return false;
        return true;
    }

    internal PageVisibilityRule Clone()
        => new()
        {
            FirstPageOnly = FirstPageOnly,
            LastPageOnly = LastPageOnly,
            SkipFirstPage = SkipFirstPage,
            Parity = Parity
        };

    internal PageVisibilityRule WithFirstPageOnly()
    {
        PageVisibilityRule clone = Clone();
        clone.OnFirstPage();
        return clone;
    }

    internal PageVisibilityRule WithContinuationPages()
    {
        PageVisibilityRule clone = Clone();
        clone.OnContinuationPages();
        return clone;
    }
}
