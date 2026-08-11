using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    internal static class HeaderFooterLayoutComposer
    {
        public static void Prepare(PdfDocument document, DateTime timestampUtc)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            int pageCount = document.Pages.Count;
            for (int i = 0; i < document.Pages.Count; i++)
            {
                var page = document.Pages[i];
                var spec = page.HeaderFooterOverride ?? document.HeaderFooter;
                if (spec == null)
                {
                    page.SetHeaderElements(Array.Empty<PdfElement>());
                    page.SetFooterElements(Array.Empty<PdfElement>());
                    continue;
                }

                var context = new HeaderFooterRenderContext(document, page, i + 1, pageCount, timestampUtc);
                using (HeaderFooterRenderScope.Push(context))
                {
                    bool isFirst = i == 0;
                    bool isLast = i == pageCount - 1;
                    page.SetHeaderElements(spec.FirstPageDifferent && isFirst
                        ? Array.Empty<PdfElement>()
                        : Render(spec.HeaderLayout, isHeader: true, page, spec));
                    page.SetFooterElements((spec.FirstPageDifferent && isFirst) || (spec.HideOnLastPage && isLast)
                        ? Array.Empty<PdfElement>()
                        : Render(spec.FooterLayout, isHeader: false, page, spec));
                }
            }
        }

        private static IReadOnlyList<PdfElement> Render(
            HeaderFooterLayoutDefinition? layout,
            bool isHeader,
            PdfPage page,
            HeaderFooterSpec spec)
        {
            if (layout == null)
                return Array.Empty<PdfElement>();

            float left = page.MarginLeft;
            float right = page.Width - page.MarginRight;
            float width = Math.Max(0f, right - left);
            if (width <= 0.1f)
                return Array.Empty<PdfElement>();

            float top;
            float bottom;
            float height = isHeader ? spec.HeaderHeight : spec.FooterHeight;
            if (height <= 0.1f)
                return Array.Empty<PdfElement>();

            if (isHeader)
            {
                top = page.Height - page.MarginTop;
                bottom = top - height;
            }
            else
            {
                bottom = Math.Max(0f, page.MarginBottom - height);
                top = bottom + height;
            }

            top = Math.Min(page.Height, top);
            bottom = Math.Max(0f, bottom);
            if (top - bottom <= 0.1f)
                return Array.Empty<PdfElement>();

            FlowColumn[] ColumnFactory(PdfPage _) => new[]
            {
                new FlowColumn(0, left, width, top, bottom)
            };

            var tempPage = new PdfPage(page.Width, page.Height)
            {
                MarginTop = page.MarginTop,
                MarginBottom = page.MarginBottom,
                MarginLeft = page.MarginLeft,
                MarginRight = page.MarginRight,
                TextDefaults = page.TextDefaults.Clone(),
                Theme = page.Theme.Clone(),
                BackgroundColor = page.BackgroundColor
            };
            tempPage.LayoutOptions = page.LayoutOptions.Clone();

            float defaultSpacing = layout.DefaultSpacing ?? 4f;
            var column = new ColumnBuilder(
                tempPage,
                margin: 0f,
                defaultSpacing: defaultSpacing,
                newPage: null,
                hfForPage: null,
                layoutOptions: tempPage.LayoutOptions,
                textDefaults: tempPage.TextDefaults,
                columnFactory: ColumnFactory);

            column.ComposeContent(layout.Configure);

            ApplyDefaults(tempPage.Elements, spec);
            return tempPage.Elements.ToList();
        }

        private static void ApplyDefaults(IEnumerable<PdfElement> elements, HeaderFooterSpec spec)
        {
            foreach (var element in elements)
            {
                switch (element)
                {
                    case TextElement text:
                        if (string.Equals(text.FontFamily, "Helvetica", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(text.FontFamily))
                        {
                            text.FontFamily = spec.FontFamily;
                        }
                        if (Math.Abs(text.FontSize - 12f) < 0.001f || text.FontSize <= 0f)
                        {
                            text.FontSize = spec.FontSize;
                        }
                        if (string.Equals(text.Color, "black", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(text.Color, "#000000", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(text.Color, "#000", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(text.Color))
                        {
                            text.Color = spec.Color;
                        }
                        break;

                    case RichTextElement rich:
                        if (string.Equals(rich.FontFamily, "Helvetica", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(rich.FontFamily))
                        {
                            rich.FontFamily = spec.FontFamily;
                        }
                        if (Math.Abs(rich.FontSize - 12f) < 0.001f || rich.FontSize <= 0f)
                        {
                            rich.FontSize = spec.FontSize;
                        }
                        foreach (var run in rich.Runs)
                        {
                            if (string.Equals(run.FontFamily, "Helvetica", StringComparison.OrdinalIgnoreCase) ||
                                string.IsNullOrWhiteSpace(run.FontFamily))
                            {
                                run.FontFamily = spec.FontFamily;
                            }
                            if (!run.FontSize.HasValue || run.FontSize <= 0f)
                            {
                                run.FontSize = spec.FontSize;
                            }
                            if (string.Equals(run.Color, "#000", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(run.Color, "#000000", StringComparison.OrdinalIgnoreCase) ||
                                string.IsNullOrWhiteSpace(run.Color))
                            {
                                run.Color = spec.Color;
                            }
                        }
                        break;
                }
            }
        }
    }
}
