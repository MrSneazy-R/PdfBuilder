using System.Diagnostics;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Elements.Table;
using PdfBuilder.Models;
using PdfBuilder.Writer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace PdfBuilder.Document
{
    public class ColumnBuilder
    {
        // Current page and geometry (mutable so we can page/column-break)
        private PdfPage _page;
        private readonly float _defaultSpacing;
        private readonly float _margin;
        private readonly LayoutOptions _layoutOptions;
        private TextStyleDefaults _textDefaults;
        private readonly Func<PdfPage, FlowColumn[]>? _customColumnFactory;
        private readonly Dictionary<CacheKey, LayoutMeasurement> _measurementCache = new();
        private readonly HashSet<string> _showOnceKeys = new(StringComparer.Ordinal);
        private readonly PdfDocument? _document;
        private readonly PaginationRegistry? _pagination;
        private readonly LayoutProfilerSession? _profilerSession;
        private readonly bool _profilerEnabled;

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
            LayoutOptions? layoutOptions = null,
            TextStyleDefaults? textDefaults = null,
            Func<PdfPage, FlowColumn[]>? columnFactory = null,
            PdfDocument? document = null)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _margin = margin;
            _defaultSpacing = defaultSpacing;
            _newPage = newPage;
            _hfForPage = hfForPage;
            _layoutOptions = layoutOptions ?? page.LayoutOptions ?? new LayoutOptions();
            _textDefaults = (textDefaults ?? page.TextDefaults ?? new TextStyleDefaults()).Clone();
            _customColumnFactory = columnFactory;
            _document = document ?? page.Owner;
            _pagination = page.Pagination ?? _document?.Pagination;
            _profilerSession = page.ProfilerSession ?? _document?.ProfilerSession;
            _profilerEnabled = (_layoutOptions?.Profiler.Enabled ?? false) && _profilerSession != null;

            ResolveHeaderFooterBands(_page, out _headerH, out _footerH);
            InitColumns(_page);
            _pageSequence = 1;
        }

        public ColumnBuilder DefaultTextStyle(Action<TextStyleDefaults> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var clone = _textDefaults.Clone();
            configure(clone);
            _textDefaults = clone;
            return this;
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
            if (_customColumnFactory != null)
            {
                var columns = _customColumnFactory(page) ?? Array.Empty<FlowColumn>();
                if (columns.Length == 0)
                {
                    float width = Math.Max(0, page.Width - (_margin > 0 ? _margin * 2 : page.MarginLeft + page.MarginRight));
                    float top = page.Height - (_margin > 0 ? _margin : page.MarginTop);
                    float bottom = (_margin > 0 ? _margin : page.MarginBottom);
                    columns = new[] { new FlowColumn(0, _margin > 0 ? _margin : page.MarginLeft, width, top, bottom) };
                }
                _columns = columns;
            }
            else
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
            _textDefaults = _page.TextDefaults.Clone();
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

        internal bool TryConsumeShowOnce(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("ShowOnce key cannot be null or empty.", nameof(key));
            return _showOnceKeys.Add(key);
        }


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

        public ColumnBuilder Section(string title, Action<SectionContext>? configure = null, int level = 1, bool startOnNewPage = false, bool includeInToc = true)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentNullException(nameof(title));

            if (startOnNewPage && (_page.Elements.Count > 0 || CurrentColumn.Y < CurrentColumn.TopY - 0.1f))
                PageBreak();

            string anchorId = _pagination?.EnsureAnchorId(title) ?? Guid.NewGuid().ToString("N");
            var entry = _pagination?.RegisterSection(title, level, anchorId, includeInToc);
            string number = entry?.Number ?? string.Empty;

            var anchor = new AnchorElement(anchorId, CurrentColumn.X, CurrentColumn.Y)
            {
                Title = includeInToc ? (string.IsNullOrEmpty(number) ? title : $"{number} {title}") : null,
                Level = Math.Max(1, level)
            };
            AddAnchor(anchor);

            var context = new SectionContext(this, title, number, anchorId, level);
            configure?.Invoke(context);
            return this;
        }

        public ColumnBuilder TableOfContents(Action<TableOfContentsOptions>? configure = null)
        {
            if (_pagination == null || _pagination.Sections.Count == 0)
                return this;

            var sections = _pagination.Sections.Where(s => s.IncludeInToc).ToList();
            if (sections.Count == 0)
                return this;

            var options = new TableOfContentsOptions();
            configure?.Invoke(options);

            var table = Table(CurrentColumn.X, CurrentColumn.Y, CurrentColumn.Width, 0f);
            table.TableWidth(CurrentColumn.Width);
            table.Border("#FFFFFF", 0f);
            table.ColumnLayout(
                TableColumn.Relative(1f),
                TableColumn.Fixed(options.PageNumberColumnWidth, minWidth: options.PageNumberColumnWidth, maxWidth: options.PageNumberColumnWidth));

            var pending = new List<(int rowIndex, SectionEntry section)>();
            int rowIndex = 0;

            foreach (var section in sections)
            {
                table.Row(row => row.Cells(
                    cell =>
                    {
                        cell.NoBorder();
                        cell.PaddingLeft(options.IndentPerLevel * Math.Max(0, section.Level - 1));
                        string text = options.IncludeNumbers && !string.IsNullOrEmpty(section.Number)
                            ? string.Concat(section.Number, options.NumberSeparator, section.Title)
                            : section.Title;
                        cell.Text(text);
                    },
                    cell =>
                    {
                        cell.NoBorder();
                        cell.AlignRight();
                        cell.Text(options.PendingPageText);
                    }));

                pending.Add((rowIndex, section));
                rowIndex++;
            }

            table.Add();

            var addedTable = _page.Elements.OfType<TableElement>().LastOrDefault();
            if (addedTable != null)
            {
                foreach (var entry in pending)
                {
                    if (entry.rowIndex >= 0 &&
                        entry.rowIndex < addedTable.Rows.Count &&
                        addedTable.Rows[entry.rowIndex].Cells.Count > 1)
                    {
                        var cell = addedTable.Rows[entry.rowIndex].Cells[1];
                        _pagination.RegisterPageReference(cell, entry.section, options);
                    }
                }
            }

            return this;
        }

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

        public ColumnBuilder ComposeContent(Action<ContentComposer> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var collection = new LayoutComponentCollection(this);
            var composer = new ContentComposer(collection);
            configure(composer);

            foreach (var component in collection.Components)
            {
                AddComponent(component);
            }

            return this;
        }

        // -- Adders invoked by builders (kept + tiny changes) ----------------

        private static string FormatFloat(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private InvalidOperationException CreatePlacementException(
            string reason,
            IMeasurable? component,
            FlowColumn column,
            LayoutMeasurement? measurement,
            int attempts,
            int guardLimit)
        {
            var details = new List<string>
            {
                $"component={component?.GetType().Name ?? "unknown"}",
                $"page={_pageSequence}",
                $"columnIndex={column.Index + 1}",
                $"columns={_columns.Length}",
                $"columnWidth={FormatFloat(column.Width)}pt",
                $"availableHeight={FormatFloat(column.Available)}pt",
                $"cursorY={FormatFloat(column.Y)}pt",
                $"attempts={attempts}",
                $"limit={guardLimit}"
            };

            if (measurement != null)
            {
                details.Add($"result={measurement.Result}");
                if (measurement.IsWrap)
                {
                    details.Add($"usedWidth={FormatFloat(measurement.UsedWidth)}pt");
                }
                else
                {
                    details.Add($"reservedHeight={FormatFloat(measurement.ReservedHeight)}pt");
                    details.Add($"contentHeight={FormatFloat(measurement.ContentHeight)}pt");
                    details.Add($"marginTop={FormatFloat(measurement.MarginTop)}pt");
                    details.Add($"marginBottom={FormatFloat(measurement.MarginBottom)}pt");
                    if (measurement.Remainder != null)
                        details.Add("remainder=true");
                }
            }

            var hint = "Enable LayoutOptions.Debug.TraceLayout or DrawBoundingBoxes for diagnostics.";
            return new InvalidOperationException($"{reason} Details: {string.Join(", ", details)}. {hint}");
        }

        internal float AddComponent(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            if (_profilerEnabled)
                _profilerSession!.EnsureComponent(component.GetType());

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
                    var previousColumn = column;
                    NextColumnOrPage();
                    if (++guard > guardLimit)
                    {
                        int attempts = guard;
                        throw CreatePlacementException(
                            $"Component could not be placed because it repeatedly reported a wrap result after {attempts} page/column breaks",
                            current,
                            previousColumn,
                            measurement,
                            attempts,
                            guardLimit);
                    }
                    continue;
                }

                if (measurement.ReservedHeight > column.Available + 0.1f)
                {
                    var previousColumn = column;
                    NextColumnOrPage();
                    if (++guard > guardLimit)
                    {
                        int attempts = guard;
                        throw CreatePlacementException(
                            $"Component measurement remained larger than the available space after {attempts} page/column breaks",
                            current,
                            previousColumn,
                            measurement,
                            attempts,
                            guardLimit);
                    }
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
                    {
                        int attempts = guard;
                        throw CreatePlacementException(
                            $"Component remainder could not be placed after {attempts} page/column breaks",
                            current,
                            CurrentColumn,
                            null,
                            attempts,
                            guardLimit);
                    }
                }
            }

            return totalHeight;
        }

        private LayoutMeasurement MeasureWithCache(IMeasurable component, LayoutMeasureContext context)
        {
            if (!_layoutOptions.EnableMeasurementCaching)
            {
                return MeasureCore(component, context);
            }

            int widthKey = Quantize(context.AvailableWidth);
            int heightKey = Quantize(context.AvailableHeight);
            var key = new CacheKey(component, widthKey, heightKey);

            if (_measurementCache.TryGetValue(key, out var cached))
            {
                Trace($"Cache hit for {component.GetType().Name} ({widthKey},{heightKey})");
                return cached;
            }

            var measurement = MeasureCore(component, context);
            if (measurement.Result == LayoutResultKind.Full && measurement.Remainder == null)
            {
                _measurementCache[key] = measurement;
                Trace($"Cached measurement for {component.GetType().Name} ({widthKey},{heightKey})");
            }
            return measurement;
        }

        private LayoutMeasurement MeasureCore(IMeasurable component, LayoutMeasureContext context)
        {
            if (_profilerEnabled)
            {
                var sw = Stopwatch.StartNew();
                var measurement = component.Measure(context);
                sw.Stop();
                _profilerSession!.RecordMeasurement(component.GetType(), sw.Elapsed.TotalMilliseconds);
                return measurement;
            }

            return component.Measure(context);
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

        internal void ApplyTextDefaults(TextElement element)
        {
            _textDefaults.ApplyTo(element);
            element.FlowDirection = _textDefaults.FlowDirection;
        }

        internal void ApplyRichTextDefaults(RichTextElement element)
        {
            _textDefaults.ApplyTo(element);
            element.FlowDirection = _textDefaults.FlowDirection;
        }

        internal void ApplyListDefaults(ListElement element)
        {
            _textDefaults.ApplyTo(element);
            element.FlowDirection = _textDefaults.FlowDirection;
        }

        internal void ApplyRunDefaults(RichRun run)
        {
            _textDefaults.ApplyTo(run);
        }

        internal FlowDirection CurrentFlowDirection => _textDefaults.FlowDirection;

        internal void ApplyTableDefaults(TableElement table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            _textDefaults.ApplyTo(table.DefaultTextStyle);
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

        internal float AddCanvas(CanvasElement canvas)
        {
            var component = new CanvasComponent(canvas, _defaultSpacing);
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





















