using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    public sealed class RichTextBuilder
    {
        private readonly ColumnBuilder _col;
        private readonly RichTextElement _rt;

        public RichTextBuilder(ColumnBuilder col, float x, float y, float defaultWidth)
        {
            _col = col;
            _rt = new RichTextElement(x, y) { MaxWidth = defaultWidth };
        }

        public RichTextBuilder Font(string family, float size) { _rt.FontFamily = family; _rt.FontSize = size; return this; }
        public RichTextBuilder LineHeight(float v) { _rt.LineHeight = v; return this; }
        public RichTextBuilder Align(TextAlignment a) { _rt.Alignment = a; return this; }
        public RichTextBuilder MaxWidth(float v) { _rt.MaxWidth = v; return this; }
        public RichTextBuilder MarginTop(float v) { _rt.MarginTop = v; return this; }
        public RichTextBuilder MarginBottom(float v) { _rt.MarginBottom = v; return this; }

        // Add a span then fluently style it
        public SpanBuilder Span(string text)
        {
            var run = new RichRun { Text = text, FontFamily = _rt.FontFamily, FontSize = _rt.FontSize };
            _rt.Runs.Add(run);
            return new SpanBuilder(this, run);
        }

        public float Add()
        {
            float height = _col.AddRichText(_rt);
            var flow = _col.GetFlow();
            if (!flow.CanFit(height))
                flow.Advance(height);
            else
                flow.Reserve(height);

            return height;
        }

        public sealed class SpanBuilder
        {
            private readonly RichTextBuilder _parent; private readonly RichRun _r;
            internal SpanBuilder(RichTextBuilder p, RichRun r) { _parent = p; _r = r; }
            public SpanBuilder Bold() { _r.Bold = true; return this; }
            public SpanBuilder Italic() { _r.Italic = true; return this; }
            public SpanBuilder Underline() { _r.Underline = true; return this; }
            public SpanBuilder Strike() { _r.Strikethrough = true; return this; }
            public SpanBuilder SmallCaps() { _r.SmallCaps = true; return this; }
            public SpanBuilder Size(float s) { _r.FontSize = s; return this; }
            public SpanBuilder Color(string hex) { _r.Color = hex; return this; }
            public SpanBuilder LinkUrl(string url) { _r.LinkUrl = url; _r.LinkAnchor = null; return this; }
            public SpanBuilder LinkAnchor(string id) { _r.LinkAnchor = id; _r.LinkUrl = null; return this; }
            public RichTextBuilder EndSpan() => _parent;
        }
    }
}
