using PdfBuilder.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfBuilder.Elements.Table;

namespace PdfBuilder.Document
{
    public sealed class PaginationRegistry
    {
        private readonly List<SectionEntry> _sections = new();
        private readonly List<PageReference> _references = new();
        private readonly List<int> _counters = new();
        private readonly Dictionary<string, int> _anchorCounts = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<SectionEntry> Sections => _sections;

        internal string EnsureAnchorId(string title)
        {
            string baseId = Slugify(title);
            if (_anchorCounts.TryGetValue(baseId, out int count))
            {
                count++;
                _anchorCounts[baseId] = count;
                return $"{baseId}-{count}";
            }

            _anchorCounts[baseId] = 1;
            return baseId;
        }

        internal SectionEntry RegisterSection(string title, int level, string anchorId, bool includeInToc)
        {
            level = Math.Max(1, level);
            while (_counters.Count < level)
                _counters.Add(0);

            _counters[level - 1]++;
            for (int i = level; i < _counters.Count; i++)
                _counters[i] = 0;

            var parts = new List<string>();
            for (int i = 0; i < level; i++)
            {
                int value = Math.Max(1, _counters[i]);
                parts.Add(value.ToString());
            }

            string number = string.Join(".", parts);
            var entry = new SectionEntry(title, level, number, anchorId, includeInToc);
            _sections.Add(entry);
            return entry;
        }

        internal void RegisterPageReference(TableCell cell, SectionEntry section, TableOfContentsOptions options)
        {
            if (cell == null) throw new ArgumentNullException(nameof(cell));
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _references.Add(new PageReference(cell, section, options.PageNumberFormat, options.PendingPageText));
        }

        internal void ApplyPageLookup(Dictionary<string, (int pageIndex, float xPdf, float yPdf)> anchorLookup)
        {
            if (anchorLookup == null)
                return;

            foreach (var section in _sections)
            {
                if (anchorLookup.TryGetValue(section.AnchorId, out var info))
                    section.PageNumber = info.pageIndex + 1;
                else
                    section.PageNumber = 0;
            }

            foreach (var reference in _references)
            {
                int pageNumber = reference.Section.PageNumber;
                reference.Cell.Text = pageNumber > 0
                    ? string.Format(reference.Format, pageNumber)
                    : reference.PendingText;
            }
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "section";

            var sb = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_' || ch == '.')
                {
                    if (sb.Length > 0 && sb[^1] != '-')
                        sb.Append('-');
                }
            }

            if (sb.Length == 0)
                sb.Append("section");

            var slug = sb.ToString().Trim('-');
            return string.IsNullOrEmpty(slug) ? "section" : slug;
        }

        private sealed record PageReference(TableCell Cell, SectionEntry Section, string Format, string PendingText);
    }

    public sealed class SectionEntry
    {
        internal SectionEntry(string title, int level, string number, string anchorId, bool includeInToc)
        {
            Title = title;
            Level = level;
            Number = number;
            AnchorId = anchorId;
            IncludeInToc = includeInToc;
        }

        public string Title { get; }
        public int Level { get; }
        public string Number { get; }
        public string AnchorId { get; }
        public bool IncludeInToc { get; }
        public int PageNumber { get; internal set; }

        public string TitleWithNumber => string.IsNullOrEmpty(Number)
            ? Title
            : $"{Number} {Title}";
    }

    public sealed class TableOfContentsOptions
    {
        public bool IncludeNumbers { get; set; } = true;
        public float IndentPerLevel { get; set; } = 12f;
        public float PageNumberColumnWidth { get; set; } = 48f;
        public string PageNumberFormat { get; set; } = "{0}";
        public string PendingPageText { get; set; } = "â€¦";
        public string NumberSeparator { get; set; } = " ";
    }
}

