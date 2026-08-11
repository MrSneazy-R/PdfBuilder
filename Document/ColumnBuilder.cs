using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using PdfBuilder.Document.Layout;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Elements.Table;
using PdfBuilder.Models;
using PdfBuilder.Writer;

namespace PdfBuilder.Document
{
    public class ColumnBuilder
    {
        // Current page and geometry (mutable so we can page/column-break)
        private PdfPage _page;
        private readonly float _defaultSpacing;
        private readonly float _margin;
        private readonly LayoutOptions _layoutOptions = new();
        private TextStyleDefaults _textDefaults;
        private readonly Func<PdfPage, FlowColumn[]>? _customColumnFactory;
        private readonly Dictionary<CacheKey, LayoutMeasurement> _measurementCache = new();
        private readonly HashSet<string> _showOnceKeys = new(StringComparer.Ordinal);
        private readonly PdfDocument? _document;
        private readonly PaginationRegistry? _pagination;
        private readonly LayoutProfilerSession? _profilerSession;
        private readonly bool _profilerEnabled;
        private readonly List<Type> _componentPath = new();

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
            _layoutOptions = layoutOptions ?? _page.LayoutOptions ?? new LayoutOptions();
            _textDefaults = (textDefaults ?? _page.TextDefaults ?? new TextStyleDefaults()).Clone();
            _customColumnFactory = columnFactory;
            _document = document ?? _page.Owner;
            _pagination = _page.Pagination ?? _document?.Pagination;
            _profilerSession = _page.ProfilerSession ?? _document?.ProfilerSession;
            _profilerEnabled = _layoutOptions.Profiler.Enabled && _profilerSession != null;

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

        private bool IsStructuredTracingEnabled => _layoutOptions.Diagnostics.EnableLayoutTrace && _document != null;

        private string ComponentName(IMeasurable? component)
        {
            return component is DebugLabelComponent labeled
                ? labeled.Label
                : component is IPageAwareMeasurable pageAware
                    ? pageAware.DiagnosticPath
                : component?.GetType().Name ?? "unknown";
        }

        private string ComponentPath(IMeasurable? component)
        {
            return $"Document > Page[{_pageSequence}] > Content > {ComponentName(component)}";
        }

        private void RecordTrace(
            string @event,
            IMeasurable? component,
            FlowColumn? column = null,
            LayoutMeasurement? measurement = null,
            double elapsedMilliseconds = 0d,
            bool cacheHit = false,
            string? warning = null)
        {
            if (!IsStructuredTracingEnabled)
                return;

            var flow = column ?? CurrentColumn;
            _document!.LayoutTrace.Record(new PdfLayoutTraceEntry
            {
                Event = @event,
                ComponentPath = ComponentPath(component),
                Component = ComponentName(component),
                PageNumber = _pageSequence,
                ColumnIndex = flow.Index,
                AvailableWidth = flow.Width,
                AvailableHeight = flow.Available,
                Result = measurement?.Result.ToString(),
                HasRemainder = measurement?.Remainder != null,
                ElapsedMilliseconds = elapsedMilliseconds,
                CacheHit = cacheHit,
                Warning = warning
            });
        }

        private void ResolveHeaderFooterBands(PdfPage page, out float headerH, out float footerH)
        {
            headerH = 0f; footerH = 0f;
            if (_hfForPage != null)
            {
                var hf = _hfForPage(page);
                if (hf != null)
                {
                    int totalPagesHint = page.Owner?.CompositionTotalPagesHint ?? 0;
                    int pageNumber = Math.Max(1, page.CompositionPageNumber);
                    bool useCanonicalVisibility = totalPagesHint > 0;
                    bool headerVisible = !useCanonicalVisibility || hf.IsHeaderVisible(pageNumber, Math.Max(pageNumber, totalPagesHint));
                    bool footerVisible = !useCanonicalVisibility || hf.IsFooterVisible(pageNumber, Math.Max(pageNumber, totalPagesHint));
                    headerH = headerVisible && (hf.HeaderLayout != null || !string.IsNullOrWhiteSpace(hf.HeaderTemplate))
                        ? Math.Max(0f, hf.HeaderHeight)
                        : 0f;
                    footerH = footerVisible && (hf.FooterLayout != null || !string.IsNullOrWhiteSpace(hf.FooterTemplate))
                        ? Math.Max(0f, hf.FooterHeight)
                        : 0f;
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
            RecordTrace("page-transition", null, CurrentColumn);
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
                RecordTrace("column-transition", null, CurrentColumn);
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
            _page.AddElement(new UnderlineElement(x, useY)
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

        private PdfLayoutException CreatePlacementException(
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

            var context = new PdfLayoutFailureContext
            {
                ComponentPath = ComponentPath(component),
                Component = ComponentName(component),
                PageNumber = _pageSequence,
                ColumnIndex = column.Index,
                AvailableWidth = column.Width,
                AvailableHeight = column.Available,
                RequestedWidth = measurement?.UsedWidth,
                RequestedHeight = measurement?.ReservedHeight,
                MeasuredWidth = measurement?.UsedWidth,
                MeasuredHeight = measurement?.ReservedHeight,
                BreakPolicy = measurement?.AvoidBreakInside == true ? "keep-together" : "allow-break",
                LayoutIterationCount = attempts,
                StyleConstraints = new Dictionary<string, string>
                {
                    ["result"] = measurement?.Result.ToString() ?? "unknown",
                    ["hasRemainder"] = (measurement?.Remainder != null).ToString()
                },
                SuggestedActions = new[]
                {
                    "Reduce the requested size or remove a conflicting minimum constraint.",
                    "Allow the component to break across pages, or increase the available page area.",
                    "Use DebugLabel on the enclosing canonical container to make this path domain-specific."
                }
            };
            RecordTrace("warning", component, column, measurement, warning: reason);
            return new PdfLayoutException($"{reason} Details: {string.Join(", ", details)}.", context);
        }

        internal float AddComponent(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            if (_profilerEnabled)
                _profilerSession!.EnsureComponent(component.GetType());

            float totalHeight = 0f;
            IMeasurable? current = component;
            int configuredLimit = _document == null
                ? _layoutOptions.Diagnostics.LayoutIterationLimit
                : Math.Min(_document.RenderLimits.MaximumLayoutIterations, _layoutOptions.Diagnostics.LayoutIterationLimit);
            int guardLimit = Math.Max(1, configuredLimit);
            int guard = 0;

            while (current != null)
            {
                if (current is PageBreakComponent)
                {
                    PageBreak();
                    return totalHeight;
                }
                var column = CurrentColumn;
                var measureContext = new LayoutMeasureContext(_page, column, _layoutOptions);
                var measurement = MeasureWithCache(current, measureContext);
                RecordTrace("measure-result", current, column, measurement);

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
                try
                {
                    if (_profilerEnabled)
                    {
                        var drawTimer = Stopwatch.StartNew();
                        current.Draw(drawContext, measurement);
                        drawTimer.Stop();
                        _profilerSession!.RecordDraw(current.GetType(), drawTimer.Elapsed.TotalMilliseconds);
                        RecordTrace("draw", current, column, measurement, drawTimer.Elapsed.TotalMilliseconds);
                    }
                    else
                    {
                        current.Draw(drawContext, measurement);
                        RecordTrace("draw", current, column, measurement);
                    }
                }
                catch (PdfCompositionException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new PdfDrawingException($"Drawing failed for {ComponentPath(current)}.", exception);
                }

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
                    if (reserved > 0f)
                    {
                        // A partial component made measurable progress. It may legitimately
                        // continue across many pages (for example, a thousand-row table).
                        guard = 0;
                    }
                    else if (++guard > guardLimit)
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
            if (!_layoutOptions.EnableMeasurementCaching || component is IPageAwareMeasurable)
            {
                return MeasureCore(component, context);
            }

            int widthKey = Quantize(context.AvailableWidth);
            int heightKey = Quantize(context.AvailableHeight);
            var key = new CacheKey(component, widthKey, heightKey);

            if (_measurementCache.TryGetValue(key, out var cached))
            {
                Trace($"Cache hit for {component.GetType().Name} ({widthKey},{heightKey})");
                RecordTrace("measure-cache-hit", component, context.Column, cached, cacheHit: true);
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
                RecordTrace("measure", component, context.Column, measurement, sw.Elapsed.TotalMilliseconds);
                return measurement;
            }

            var result = component.Measure(context);
            RecordTrace("measure", component, context.Column, result);
            return result;
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
            element.Color = ResolveThemeColor(element.Color);
        }

        internal void ApplyNamedTextStyle(TextElement element, string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                throw new ArgumentException("A named text style is required.", nameof(styleName));
            if (!_page.Theme.TryGetTextStyle(styleName, out var style))
                throw new KeyNotFoundException($"Theme text style '{styleName}' is not defined.");
            style.ApplyTo(element);
            element.Color = ResolveThemeColor(element.Color);
        }

        internal string ResolveThemeColor(string value) => _page.Theme.ResolveColor(value);

        internal void ComposeComponent(Type componentType, Action compose)
        {
            if (componentType == null) throw new ArgumentNullException(nameof(componentType));
            if (compose == null) throw new ArgumentNullException(nameof(compose));

            string name = componentType.FullName ?? componentType.Name;
            int cycleStart = _componentPath.IndexOf(componentType);
            if (cycleStart >= 0)
            {
                string cycle = string.Join(" -> ", _componentPath.Skip(cycleStart).Select(type => type.Name).Append(componentType.Name));
                throw new PdfComponentCompositionException(
                    $"Circular PDF component composition detected: {cycle}. Remove the recursive component reference.",
                    string.Join(" -> ", _componentPath.Select(type => type.Name).Append(componentType.Name)));
            }

            if (_componentPath.Count >= 64)
            {
                string path = string.Join(" -> ", _componentPath.Select(type => type.Name).Append(componentType.Name));
                throw new PdfComponentCompositionException(
                    "PDF component nesting exceeded the configured safety limit of 64. Flatten the component hierarchy.",
                    path);
            }

            _componentPath.Add(componentType);
            try
            {
                compose();
            }
            catch (PdfComponentCompositionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                string path = string.Join(" -> ", _componentPath.Select(type => type.Name));
                throw new PdfComponentCompositionException($"Component '{name}' failed while composing at '{path}'.", path, exception);
            }
            finally
            {
                _componentPath.RemoveAt(_componentPath.Count - 1);
            }
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

            foreach (TableCell cell in table.Rows.SelectMany(row => row.Cells))
            {
                if (string.IsNullOrWhiteSpace(cell.ThemeStyleName))
                    continue;

                if (!_page.Theme.TryGetTextStyle(cell.ThemeStyleName, out var style))
                    throw new KeyNotFoundException($"Theme text style '{cell.ThemeStyleName}' is not defined.");

                if (!string.IsNullOrWhiteSpace(style.Color))
                    style.Color = ResolveThemeColor(style.Color);
                if (!string.IsNullOrWhiteSpace(style.BackgroundColor))
                    style.BackgroundColor = ResolveThemeColor(style.BackgroundColor);
                if (!string.IsNullOrWhiteSpace(style.DecorationColor))
                    style.DecorationColor = ResolveThemeColor(style.DecorationColor);

                cell.TextStyle ??= new PdfBuilder.Elements.Table.TextStyle();
                style.ApplyTo(cell.TextStyle);
                if (style.MaximumLines.HasValue) cell.MaxLines = style.MaximumLines;

                ApplyCanonicalTableOverrides(cell);
            }

            foreach (TableCell cell in table.Rows.SelectMany(row => row.Cells).Where(cell => string.IsNullOrWhiteSpace(cell.ThemeStyleName)))
            {
                if (cell.CanonicalStyleOverrides == null)
                    continue;
                cell.TextStyle ??= new PdfBuilder.Elements.Table.TextStyle();
                ApplyCanonicalTableOverrides(cell);
            }

            void ApplyCanonicalTableOverrides(TableCell cell)
            {
                if (cell.CanonicalStyleOverrides == null || cell.TextStyle == null) return;
                var direct = cell.CanonicalStyleOverrides.Clone();
                if (!string.IsNullOrWhiteSpace(direct.Color)) direct.Color = ResolveThemeColor(direct.Color);
                if (!string.IsNullOrWhiteSpace(direct.BackgroundColor)) direct.BackgroundColor = ResolveThemeColor(direct.BackgroundColor);
                if (!string.IsNullOrWhiteSpace(direct.DecorationColor)) direct.DecorationColor = ResolveThemeColor(direct.DecorationColor);
                direct.ApplyTo(cell.TextStyle);
                if (direct.MaximumLines.HasValue) cell.MaxLines = direct.MaximumLines;
            }
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





















