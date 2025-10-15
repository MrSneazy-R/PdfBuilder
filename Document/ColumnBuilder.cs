using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PdfBuilder.Document
{
    public class ColumnBuilder
    {
        // Current page and geometry (mutable so we can page/column-break)
        private PdfPage _page;
        private readonly float _defaultSpacing;
        private readonly float _margin;
        private readonly LayoutOptions _layoutOptions;
        private readonly Dictionary<CacheKey, LayoutMeasurement> _measurementCache = new();

        // Header/Footer reserved heights
        private readonly Func<PdfPage, HeaderFooterSpec?>? _hfForPage; // optional
        private float _headerH;
        private float _footerH;
        private bool _flowGuidesInjected;
        private int _pageSequence;

        // Flow columns
        private int _colIndex = 0;
        private FlowColumn[] _columns = Array.Empty<FlowColumn>();
        private FlowColumn CurrentColumn => _columns.Length == 0 ? throw new InvalidOperationException("Flow columns not initialized.") : _columns[_colIndex];

        // Optional factory for new pages
        private readonly Func<PdfPage>? _newPage;

        public ColumnBuilder(
            PdfPage page,
            float margin,
            float defaultSpacing = 8f,
            Func<PdfPage>? newPage = null,
            Func<PdfPage, HeaderFooterSpec?>? hfForPage = null,
            LayoutOptions? layoutOptions = null)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _margin = margin;
            _defaultSpacing = defaultSpacing;
            _newPage = newPage;
            _hfForPage = hfForPage;
            _layoutOptions = layoutOptions ?? page.LayoutOptions ?? new LayoutOptions();

            ResolveHeaderFooterBands(_page, out _headerH, out _footerH);
            InitColumns(_page);
            _pageSequence = 1;
        }

        private void Trace(string message)
        {
            if (_layoutOptions.Debug.TraceLayout)
            {
                System.Diagnostics.Trace.WriteLine($"[Layout] {message}");
            }
        }

        private void ResolveHeaderFooterBands(PdfPage page, out float headerH, out float footerH)
        {
            headerH = 0f; footerH = 0f;
            if (_hfForPage != null)
            {
                var hf = _hfForPage(page);
                if (hf != null)
                {
                    headerH = Math.Max(0f, hf.HeaderHeight);
                    footerH = Math.Max(0f, hf.FooterHeight);
                }
            }
        }

        private void InitColumns(PdfPage page)
        {
            var layout = page.Columns ?? new ColumnLayoutSpec { Columns = 1, Gutter = 14f };
            int columnCount = Math.Max(1, layout.Widths?.Length ?? layout.Columns);
            float gutter = layout.Gutter;
            float[]? widths = null;
            if (layout.Widths != null && layout.Widths.Length == columnCount)
                widths = (float[])layout.Widths.Clone();

            _columns = FlowGrid.Create(page, _margin, columnCount, gutter, _headerH, _footerH, widths);
            if (_columns.Length == 0)
            {
                float width = Math.Max(0, page.Width - (_margin > 0 ? _margin * 2 : page.MarginLeft + page.MarginRight));
                float top = page.Height - (_margin > 0 ? _margin : page.MarginTop) - _headerH;
                float bottom = (_margin > 0 ? _margin : page.MarginBottom) + _footerH;
                _columns = new[] { new FlowColumn(0, _margin > 0 ? _margin : page.MarginLeft, width, top, bottom) };
            }
            _colIndex = Math.Min(Math.Max(0, _colIndex), _columns.Length - 1);
            _columns[_colIndex].Reset();
            _flowGuidesInjected = false;
            InjectFlowGuidesIfRequested();
        }

        // -- Navigation -------------------------------------------------------

        public float GetCurrentY() => CurrentColumn.Y;

        // Force a column break (NEW)
        public FlowColumn ActivateColumn(int index, bool reset = false)
        {
            if (index < 0 || index >= _columns.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            _colIndex = index;
            if (reset)
                _columns[_colIndex].Reset();

            return CurrentColumn;
        }

        public ColumnBuilder ColumnBreak()
        {
            if (_columns.Length == 0)
                return this;

            if (_colIndex < _columns.Length - 1)
            {
                _colIndex++;
                _columns[_colIndex].Reset();
            }
            else
            {
                PageBreak();
            }
            return this;
        }

        // Force a page break (resets to first column)
        public ColumnBuilder PageBreak()
        {
            if (_newPage == null) return this;
            Trace($"Flow requested page break from page {_pageSequence}");
            _page = _newPage();
            ResolveHeaderFooterBands(_page, out _headerH, out _footerH);
            InitColumns(_page);

            _colIndex = 0;
            if (_columns.Length > 0)
                _columns[_colIndex].Reset();
            _pageSequence++;
            Trace($"Started new page {_pageSequence} with {_columns.Length} columns");
            return this;
        }

        // Try next column before new page (NEW)
        private void NextColumnOrPage()
        {
            if (_columns.Length == 0)
                return;

            if (_colIndex < _columns.Length - 1)
            {
                _colIndex++;
                _columns[_colIndex].Reset();
                Trace($"Advanced to column {_colIndex + 1} on page {_pageSequence}");
            }
            else
            {
                Trace("Last column exhausted; issuing page break");
                PageBreak();
            }
        }

        private void EnsureSpace(float contentHeight, float marginTop, float marginBottom, bool avoidBreakInside = false)
        {
            float need = marginTop + Math.Max(0f, contentHeight) + marginBottom;
            int guard = 0;

       
            while (true)
            {
                var current = CurrentColumn;
                float topLimit = current.TopY;
                float bottomLimit = current.BottomY;
                float maxInColumn = current.Capacity;

                if (need > maxInColumn + 0.1f)
                {
                    if (_newPage != null && Math.Abs(current.Y - topLimit) > 0.5f)
                    {
                        NextColumnOrPage();
                        if (++guard > 16) break;
                        continue;
                    }
                    return;
                }

                if ((current.Y - need) >= bottomLimit)
                    return;

                if (_newPage == null)
                    return;

                if (!avoidBreakInside && Math.Abs(current.Y - topLimit) <= 0.5f)
                    return;

                NextColumnOrPage();
                if (++guard > 16)
                    break;
            }
        }


        public FlowColumn GetFlow() => CurrentColumn;
        public FlowColumn[] GetFlowColumns() => _columns;

        internal LayoutMode LayoutMode => _layoutOptions.Mode;
        internal LayoutOptions LayoutOptions => _layoutOptions;
        internal float DefaultSpacing => _defaultSpacing;


        // -- Drawing helpers (kept) ------------------------------------------

        public ColumnBuilder Underline(float x, float? y, float width)
        {
            float useY = y ?? CurrentColumn.Y;
            _page.Elements.Add(new UnderlineElement(x, useY)
            {
                Width = width,
                Thickness = 1,
                Color = "#000000"
            });
            return this;
        }

        // Entry points (kept)
        public TextBuilder Text(string content) =>
            new TextBuilder(this, content, CurrentColumn.X, CurrentColumn.Y, CurrentColumn.Width);

        public ImageBuilder Image(byte[] data, float x, float y, float width, float height) =>
            new ImageBuilder(this, data, x, y, width, height);

        public TableBuilder Table(float x, float y, float width, float /*ignored*/ height) =>
            new TableBuilder(this, x, y, width);

        public ChartBuilder Chart(float x, float y, float width, float height) =>
            new ChartBuilder(this, x, y, width, height);

        // Row/Grid container (NEW)
        public RowBuilder Row(float gap = 12f) => new RowBuilder(this, gap, CurrentColumn.X, CurrentColumn.Y, CurrentColumn.Width);

        public ColumnBuilder Compose(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            AddComponent(component);
            return this;
        }

        public ColumnBuilder Compose(Action<LayoutComponentCollection> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var collection = new LayoutComponentCollection(this);
            configure(collection);

            foreach (var component in collection.Components)
            {
                AddComponent(component);
            }

            return this;
        }

        // -- Adders invoked by builders (kept + tiny changes) ----------------

        internal float AddComponent(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            float totalHeight = 0f;
            IMeasurable? current = component;
            const int guardLimit = 32;
            int guard = 0;

            while (current != null)
            {
                var column = CurrentColumn;
                var measureContext = new LayoutMeasureContext(_page, column, _layoutOptions);
                var measurement = MeasureWithCache(current, measureContext);

                if (measurement.IsWrap)
                {
                    NextColumnOrPage();
                    if (++guard > guardLimit)
                        throw new InvalidOperationException("Component could not be placed due to insufficient space.");
                    continue;
                }

                if (measurement.ReservedHeight > column.Available + 0.1f)
                {
                    NextColumnOrPage();
                    if (++guard > guardLimit)
                        throw new InvalidOperationException("Component measurement unstable across pages/columns.");
                    continue;
                }

                float contentTop = column.Y - measurement.MarginTop;
                var drawContext = new LayoutDrawContext(_page, column, column.X, contentTop, column.Width, _layoutOptions);
                current.Draw(drawContext, measurement);

                float reserved = Math.Max(0f, measurement.ReservedHeight);
                if (reserved > 0f)
                {
                    var rect = new FlowRect(column.X, column.Y, column.Width, reserved);
                    column.Reserve(reserved);
                    totalHeight += reserved;
                    EmitDiagnostics(rect, current, measurement);
                }

                current = measurement.Remainder;
                if (current != null)
                {
                    NextColumnOrPage();
                    if (++guard > guardLimit)
                        throw new InvalidOperationException("Component remainder could not be placed after page break.");
                }
            }

            return totalHeight;
        }

        private LayoutMeasurement MeasureWithCache(IMeasurable component, LayoutMeasureContext context)
        {
            if (!_layoutOptions.EnableMeasurementCaching)
            {
                return component.Measure(context);
            }

            int widthKey = Quantize(context.AvailableWidth);
            int heightKey = Quantize(context.AvailableHeight);
            var key = new CacheKey(component, widthKey, heightKey);

            if (_measurementCache.TryGetValue(key, out var cached))
            {
                Trace($"Cache hit for {component.GetType().Name} ({widthKey},{heightKey})");
                return cached;
            }

            var measurement = component.Measure(context);
            if (measurement.Result == LayoutResultKind.Full && measurement.Remainder == null)
            {
                _measurementCache[key] = measurement;
                Trace($"Cached measurement for {component.GetType().Name} ({widthKey},{heightKey})");
            }
            return measurement;
        }

        private static int Quantize(float value)
        {
            return (int)MathF.Round(value * 1000f, MidpointRounding.AwayFromZero);
        }

        private void EmitDiagnostics(FlowRect rect, IMeasurable component, LayoutMeasurement measurement)
        {
            var debug = _layoutOptions.Debug;
            if (!debug.DrawBoundingBoxes && !debug.TraceLayout)
                return;

            if (debug.TraceLayout)
            {
                Trace($"Column {CurrentColumn.Index} reserved {rect.Height:0.##}pt for {component.GetType().Name} ({measurement.Result})");
            }

            if (debug.DrawBoundingBoxes && rect.Height > 0f)
            {
                AddDebugRectangle(rect);
            }
        }

        private void AddDebugRectangle(FlowRect rect)
        {
            var element = new DebugRectangleElement(rect.X, rect.Bottom, rect.Width, rect.Height)
            {
                StrokeColor = "#FF6A00",
                StrokeWidth = 0.75f,
                DashPattern = new[] { 2f, 2f },
                Opacity = 0.4f
            };
            _page.AddElement(element);
        }

        private void InjectFlowGuidesIfRequested()
        {
            if (_flowGuidesInjected || !_layoutOptions.Debug.ShowFlowGuides || _columns.Length == 0)
                return;

            foreach (var column in _columns)
            {
                var guide = new DebugRectangleElement(column.X, column.BottomY, column.Width, column.Capacity)
                {
                    StrokeColor = "#4A90E2",
                    StrokeWidth = 0.5f,
                    DashPattern = new[] { 4f, 4f },
                    Opacity = 0.2f
                };
                _page.AddElement(guide);
            }

            _flowGuidesInjected = true;
        }

        internal float AddText(TextElement text)
        {
            var component = new TextComponent(text, _defaultSpacing);
            return AddComponent(component);
        }

        internal float AddImage(ImageElement image)
        {
            var component = new ImageComponent(image, _defaultSpacing);
            return AddComponent(component);
        }

        internal float AddTable(TableElement table)
        {
            var component = new TableComponent(table);
            return AddComponent(component);
        }

        internal float AddChart(ChartElement chart)
        {
            var component = new ChartComponent(chart);
            return AddComponent(component);
        }

        // ===== Row/Grid (NEW, simple row with %/fr/px cols and gap) =====
        public sealed class RowBuilder
        {
            private readonly ColumnBuilder _col;
            private readonly float _gap;
            private readonly float _baseX, _baseY, _maxW;

            private readonly List<(Unit u, float v, Action<float /*x*/, float /*y*/, float /*w*/> draw)> _cells = new();

            public enum Unit { Px, Percent, Fr }

            internal RowBuilder(ColumnBuilder col, float gap, float x, float y, float w)
            { _col = col; _gap = gap; _baseX = x; _baseY = y; _maxW = w; }

            public RowBuilder ColPx(float px, Action<float, float, float> draw) { _cells.Add((Unit.Px, px, draw)); return this; }
            public RowBuilder ColPercent(float pct, Action<float, float, float> draw) { _cells.Add((Unit.Percent, pct, draw)); return this; }
            public RowBuilder ColFr(float fr, Action<float, float, float> draw) { _cells.Add((Unit.Fr, fr, draw)); return this; }

            public ColumnBuilder Add(float estimatedRowHeight = 24f)
            {
                // Ensure space in current column
                _col.EnsureSpace(estimatedRowHeight, _col._defaultSpacing, 0f);
                var column = _col.CurrentColumn;
                column.Advance(_col._defaultSpacing);

                // Compute column widths
                float fixedSum = 0f, frSum = 0f, pctSum = 0f;
                foreach (var c in _cells)
                {
                    if (c.u == Unit.Px) fixedSum += c.v;
                    else if (c.u == Unit.Percent) pctSum += c.v;
                    else frSum += c.v;
                }

                float gaps = Math.Max(0, _cells.Count - 1) * _gap;
                float remain = Math.Max(0, _maxW - gaps - fixedSum - (_maxW * (pctSum / 100f)));
                float frUnit = frSum > 0 ? remain / frSum : 0f;

                float x = _baseX;
                foreach (var c in _cells)
                {
                    float w = c.u switch
                    {
                        Unit.Px => c.v,
                        Unit.Percent => _maxW * (c.v / 100f),
                        Unit.Fr => frUnit * c.v,
                        _ => 0f
                    };

                    c.draw(x, column.Y, w);
                    x += w + _gap;
                }

                column.Advance(estimatedRowHeight);
                return _col;
            }
        }

        // === existing helpers for table height etc. (kept) ===
        // -- Anchors / Lists / RichText integration --------------------------

        // Optional convenience: start an anchor at the current flow position
        public AnchorBuilder Anchor(string id) => new AnchorBuilder(this, id, CurrentColumn.X, CurrentColumn.Y);

        // Called by AnchorBuilder.Add()
        internal void AddAnchor(AnchorElement a)
        {
            // If caller didn't set coordinates, place at current flow position
            if (a.X == 0f) a.X = CurrentColumn.X;
            if (a.Y == 0f) a.Y = CurrentColumn.Y;

            _page.AddElement(a);
            // Anchors have no visual height -- don't move the flow cursor.
        }

        // Called by ListBuilder.Add()
        internal float AddList(ListElement list)
        {
            var component = new ListComponent(list, _defaultSpacing);
            return AddComponent(component);
        }

        // Called by RichTextBuilder.Add()
        internal float AddRichText(RichTextElement rt)
        {
            var component = new RichTextComponent(rt, _defaultSpacing);
            return AddComponent(component);
        }

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly IMeasurable _component;
            private readonly int _widthKey;
            private readonly int _heightKey;

            public CacheKey(IMeasurable component, int widthKey, int heightKey)
            {
                _component = component ?? throw new ArgumentNullException(nameof(component));
                _widthKey = widthKey;
                _heightKey = heightKey;
            }

            public bool Equals(CacheKey other) =>
                ReferenceEquals(_component, other._component)
                && _widthKey == other._widthKey
                && _heightKey == other._heightKey;

            public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

            public override int GetHashCode()
            {
                int componentHash = RuntimeHelpers.GetHashCode(_component);
                return HashCode.Combine(componentHash, _widthKey, _heightKey);
            }
        }
    }
}










