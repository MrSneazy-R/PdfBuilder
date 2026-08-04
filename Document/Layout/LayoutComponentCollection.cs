using System;
using System.Collections.Generic;
using PdfBuilder.Document;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout
{
    public sealed class LayoutComponentCollection
    {
        private readonly ColumnBuilder _owner;
        private readonly List<IMeasurable> _components = new();

        internal LayoutComponentCollection(ColumnBuilder owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        internal IReadOnlyList<IMeasurable> Components => _components;

        internal IMeasurable BuildComposite(Action<LayoutComponentCollection> configure, string? caller = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var childCollection = new LayoutComponentCollection(_owner);
            configure(childCollection);

            if (childCollection._components.Count == 0)
                throw new InvalidOperationException($"The {caller ?? "composite"} requires at least one child component.");

            if (childCollection._components.Count == 1)
                return childCollection._components[0];

            var column = new ColumnComponent { Spacing = _owner.DefaultSpacing };
            foreach (var child in childCollection._components)
                column.Add(child);
            return column;
        }

        internal IEnumerable<IMeasurable> BuildMany(Action<LayoutComponentCollection> configure, string? caller = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var childCollection = new LayoutComponentCollection(_owner);
            configure(childCollection);
            if (childCollection._components.Count == 0)
                throw new InvalidOperationException($"The {caller ?? "builder"} requires at least one child component.");
            return childCollection._components;
        }

        public LayoutComponentCollection Add(IMeasurable component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            _components.Add(component);
            return this;
        }

        public LayoutComponentCollection Component(IMeasurable component) => Add(component);

        public LayoutComponentCollection Component(
            Func<LayoutMeasureContext, LayoutMeasurement> measure,
            Action<LayoutDrawContext, LayoutMeasurement> draw)
        {
            if (measure == null) throw new ArgumentNullException(nameof(measure));
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            _components.Add(new DelegateComponent(measure, draw));
            return this;
        }

        public LayoutComponentCollection ShowOnce(string key, Action<LayoutComponentCollection> configure)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("ShowOnce key cannot be null or whitespace.", nameof(key));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            if (_owner.TryConsumeShowOnce(key))
            {
                configure(this);
            }

            return this;
        }

        internal LayoutComponentCollection PageBreak()
        {
            _components.Add(new PageBreakComponent());
            return this;
        }

        internal LayoutComponentCollection EnsureSpace(float minimumHeight, Action<LayoutComponentCollection> configure)
        {
            if (minimumHeight < 0f || float.IsNaN(minimumHeight) || float.IsInfinity(minimumHeight)) throw new ArgumentOutOfRangeException(nameof(minimumHeight));
            _components.Add(new EnsureSpaceComponent(BuildComposite(configure, nameof(EnsureSpace)), minimumHeight));
            return this;
        }

        internal LayoutComponentCollection KeepTogether(Action<LayoutComponentCollection> configure)
        {
            _components.Add(new KeepTogetherComponent(BuildComposite(configure, nameof(KeepTogether))));
            return this;
        }

        public LayoutComponentCollection When(bool condition, Action<LayoutComponentCollection> whenTrue, Action<LayoutComponentCollection>? whenFalse = null)
        {
            if (whenTrue == null && whenFalse == null)
                throw new ArgumentNullException(nameof(whenTrue), "At least one branch must be provided.");

            if (condition)
                whenTrue?.Invoke(this);
            else
                whenFalse?.Invoke(this);

            return this;
        }

        public LayoutComponentCollection Repeat(int count, Action<int, LayoutComponentCollection> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            if (count <= 0)
                return this;

            for (int i = 0; i < count; i++)
            {
                var childCollection = new LayoutComponentCollection(_owner);
                configure(i, childCollection);
                _components.AddRange(childCollection._components);
            }

            return this;
        }

        public LayoutComponentCollection RepeatEach<T>(IEnumerable<T> source, Action<T, int, LayoutComponentCollection> configure)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            int index = 0;
            foreach (var item in source)
            {
                var childCollection = new LayoutComponentCollection(_owner);
                configure(item, index++, childCollection);
                _components.AddRange(childCollection._components);
            }

            return this;
        }

        public LayoutComponentCollection Text(string content, Action<TextElement>? configure = null)
        {
            var flow = _owner.GetFlow();
            var element = new TextElement(content ?? string.Empty, flow.X, flow.Y)
            {
                MaxWidth = flow.Width
            };
            _owner.ApplyTextDefaults(element);
            element.FlowDirection = _owner.CurrentFlowDirection;
            configure?.Invoke(element);
            _components.Add(new TextComponent(element, _owner.DefaultSpacing));
            return this;
        }

        /// <summary>Adds a shaped rich-text paragraph to the current layout flow.</summary>
        internal LayoutComponentCollection RichText(Action<RichTextElement> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var flow = _owner.GetFlow();
            var element = new RichTextElement(flow.X, flow.Y)
            {
                MaxWidth = flow.Width
            };
            _owner.ApplyRichTextDefaults(element);
            element.FlowDirection = _owner.CurrentFlowDirection;
            configure(element);
            _components.Add(new RichTextComponent(element, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection List(Action<ListElement> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var flow = _owner.GetFlow();
            var element = new ListElement(flow.X, flow.Y)
            {
                MaxWidth = flow.Width
            };
            _owner.ApplyListDefaults(element);
            configure(element);
            _components.Add(new ListComponent(element, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection Column(Action<LayoutComponentCollection> configure, float spacing = 8f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var column = new ColumnComponent { Spacing = spacing };
            foreach (var child in BuildMany(configure, nameof(Column)))
                column.Add(child);
            _components.Add(column);
            return this;
        }

        public LayoutComponentCollection Column(Action<ColumnComponentBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new ColumnComponentBuilder(this);
            configure(builder);
            _components.Add(builder.Build());
            return this;
        }

        public LayoutComponentCollection Row(Action<LayoutComponentCollection> configure, float gap = 12f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var row = new RowComponent { Gap = gap };
            foreach (var child in BuildMany(configure, nameof(Row)))
                row.Add(child, RowComponent.RowWidthSpec.Even());
            _components.Add(row);
            return this;
        }

        public LayoutComponentCollection Row(Action<RowComponentBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new RowComponentBuilder(this);
            configure(builder);
            _components.Add(builder.Build());
            return this;
        }

        public LayoutComponentCollection Stack(Action<LayoutComponentCollection> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var stack = new StackComponent();
            foreach (var child in BuildMany(configure, nameof(Stack)))
                stack.Add(child);
            _components.Add(stack);
            return this;
        }

        public LayoutComponentCollection Stack(Action<StackComponentBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new StackComponentBuilder(this);
            configure(builder);
            _components.Add(builder.Build());
            return this;
        }

        public LayoutComponentCollection Grid(int columns, Action<LayoutComponentCollection> configure, float rowGap = 12f, float columnGap = 12f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var grid = new GridComponent
            {
                Columns = Math.Max(1, columns),
                RowGap = rowGap,
                ColumnGap = columnGap
            };
            foreach (var child in BuildMany(configure, nameof(Grid)))
                grid.Add(child);
            _components.Add(grid);
            return this;
        }

        public LayoutComponentCollection Grid(Action<GridComponentBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new GridComponentBuilder(this);
            configure(builder);
            _components.Add(builder.Build());
            return this;
        }

        public LayoutComponentCollection Padding(float uniform, Action<LayoutComponentCollection> configure) =>
            Padding(uniform, uniform, uniform, uniform, configure);

        public LayoutComponentCollection Padding(float left, float top, float right, float bottom, Action<LayoutComponentCollection> configure)
        {
            var child = BuildComposite(configure, nameof(Padding));
            var component = new PaddingComponent(child, new PaddingValues(left, top, right, bottom));
            _components.Add(component);
            return this;
        }

        public LayoutComponentCollection DefaultTextStyle(Action<TextStyleDefaults> configure)
        {
            _owner.DefaultTextStyle(configure);
            return this;
        }

        public LayoutComponentCollection Align(
            LayoutHorizontalAlignment horizontal,
            LayoutVerticalAlignment vertical,
            Action<LayoutComponentCollection> configure,
            float? minHeight = null)
        {
            var child = BuildComposite(configure, nameof(Align));
            var component = new AlignComponent(child, horizontal, vertical, minHeight);
            _components.Add(component);
            return this;
        }

        public LayoutComponentCollection Layer(Action<LayerBuilder> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new LayerBuilder(this);
            configure(builder);
            var component = builder.Build();
            _components.Add(component);
            return this;
        }

        public LayoutComponentCollection Decorate(Action<DecorationBuilder> configure, Action<LayoutComponentCollection> content)
        {
            var child = BuildComposite(content, nameof(Decorate));
            var builder = new DecorationBuilder();
            configure?.Invoke(builder);
            var component = builder.Build(child);
            _components.Add(component);
            return this;
        }

        public LayoutComponentCollection Border(Action<BorderOptions>? configure, Action<LayoutComponentCollection> content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var options = new BorderOptions();
            configure?.Invoke(options);

            return Decorate(deco =>
            {
                deco.Background(ctx =>
                {
                    var rect = ctx.Rect;
                    var element = new SolidRectElement(rect.X, rect.Bottom, rect.Width, rect.Height)
                    {
                        StrokeColor = options.StrokeColor,
                        StrokeWidth = options.StrokeWidth,
                        DashPattern = options.DashPattern,
                        FillColor = options.FillColor,
                        Opacity = options.Opacity
                    };
                    ctx.Page.AddElement(element);
                });
            }, content);
        }

        public LayoutComponentCollection Background(string color, Action<LayoutComponentCollection> content, float opacity = 1f)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrWhiteSpace(color))
                throw new ArgumentException("Background color cannot be empty.", nameof(color));

            return Decorate(deco =>
            {
                deco.Background(ctx =>
                {
                    var rect = ctx.Rect;
                    var element = new SolidRectElement(rect.X, rect.Bottom, rect.Width, rect.Height)
                    {
                        FillColor = color,
                        StrokeWidth = 0f,
                        StrokeColor = null,
                        Opacity = opacity
                    };
                    ctx.Page.AddElement(element);
                });
            }, content);
        }

        public LayoutComponentCollection Absolute(float offsetX, float offsetY, Action<LayoutComponentCollection> content)
        {
            var child = BuildComposite(content, nameof(Absolute));
            _components.Add(new AbsoluteComponent(child, offsetX, offsetY));
            return this;
        }

        public LayoutComponentCollection FlexRow(Action<FlexRowBuilder> configure, float gap = 12f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new FlexRowBuilder(this) { Gap = gap };
            configure(builder);
            _components.Add(builder.Build());
            return this;
        }

        public LayoutComponentCollection FlexColumn(Action<FlexColumnBuilder> configure, float spacing = 8f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new FlexColumnBuilder(this) { Spacing = spacing };
            configure(builder);
            _components.Add(builder.Build());
            return this;
        }

        public LayoutComponentCollection Bookmark(string id, string title, int level = 1)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Bookmark id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Bookmark title cannot be empty.", nameof(title));

            _owner.Anchor(id).Title(title).Level(level).Add();
            return this;
        }

        public LayoutComponentCollection Hyperlink(string text, string url, Action<RichRun>? configure = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Link text cannot be empty.", nameof(text));
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Link url cannot be empty.", nameof(url));

            var flow = _owner.GetFlow();
            var rich = new RichTextElement(flow.X, flow.Y)
            {
                MaxWidth = flow.Width
            };
            _owner.ApplyRichTextDefaults(rich);

            var run = new RichRun
            {
                Text = text,
                LinkUrl = url,
                FontFamily = rich.FontFamily,
                FontSize = rich.FontSize
            };
            _owner.ApplyRunDefaults(run);
            configure?.Invoke(run);
            rich.Runs.Add(run);

            _components.Add(new RichTextComponent(rich, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection Size(
            Action<ContentComposer> configure,
            float? minWidth = null,
            float? maxWidth = null,
            float? width = null,
            float? minHeight = null,
            float? maxHeight = null,
            float? height = null,
            float? aspectRatio = null,
            bool fillWidth = false,
            bool fillHeight = false,
            bool shrinkWidth = false,
            bool shrinkHeight = false)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var child = BuildComposite(inner =>
            {
                var composer = new ContentComposer(inner);
                configure(composer);
            }, nameof(Size));

            float? ratio = aspectRatio.HasValue && aspectRatio.Value > 0f ? aspectRatio : null;
            _components.Add(new SizedComponent(child, minWidth, maxWidth, width, minHeight, maxHeight, height, ratio, fillWidth, fillHeight, shrinkWidth, shrinkHeight));
            return this;
        }

        public LayoutComponentCollection Extend(Action<ContentComposer> configure) =>
            Size(configure, fillWidth: true, fillHeight: true);

        public LayoutComponentCollection ExtendHeight(Action<ContentComposer> configure) =>
            Size(configure, fillHeight: true);

        public LayoutComponentCollection ExtendWidth(Action<ContentComposer> configure) =>
            Size(configure, fillWidth: true);

        public LayoutComponentCollection Shrink(Action<ContentComposer> configure) =>
            Size(configure, shrinkWidth: true, shrinkHeight: true);

        public LayoutComponentCollection ShrinkHeight(Action<ContentComposer> configure) =>
            Size(configure, shrinkHeight: true);

        public LayoutComponentCollection ShrinkWidth(Action<ContentComposer> configure) =>
            Size(configure, shrinkWidth: true);

        public LayoutComponentCollection MinHeight(float value, Action<ContentComposer> configure)
            => Size(configure, minHeight: value);

        public LayoutComponentCollection MaxHeight(float value, Action<ContentComposer> configure)
            => Size(configure, maxHeight: value);

        public LayoutComponentCollection Height(float value, Action<ContentComposer> configure)
            => Size(configure, height: value);

        public LayoutComponentCollection MinWidth(float value, Action<ContentComposer> configure)
            => Size(configure, minWidth: value);

        public LayoutComponentCollection MaxWidth(float value, Action<ContentComposer> configure)
            => Size(configure, maxWidth: value);

        public LayoutComponentCollection Width(float value, Action<ContentComposer> configure)
            => Size(configure, width: value);

        public LayoutComponentCollection AspectRatio(float ratio, Action<ContentComposer> configure)
            => Size(configure, aspectRatio: ratio);

        public LayoutComponentCollection Relative(Action<RelativeBuilder> configure, float spacing = 0f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new RelativeBuilder(this) { Spacing = spacing };
            configure(builder);
            _components.Add(builder.Build());
            return this;
        }

        public LayoutComponentCollection Image(byte[] data, float width, float height, Action<ImageElement>? configure = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var flow = _owner.GetFlow();
            var element = new ImageElement(data, flow.X, flow.Y, Math.Max(0f, width), Math.Max(0f, height));
            configure?.Invoke(element);
            element.X = flow.X;
            element.Y = flow.Y;
            _components.Add(new ImageComponent(element, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection Canvas(float width, float height, Action<CanvasBuilder> draw, Action<CanvasElement>? configure = null)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            var flow = _owner.GetFlow();
            var element = new CanvasElement(flow.X, flow.Y, Math.Max(0f, width), Math.Max(0f, height));
            configure?.Invoke(element);
            var canvasBuilder = new CanvasBuilder(element);
            draw(canvasBuilder);
            _components.Add(new CanvasComponent(element, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection Barcode(string value, BarcodeKind kind = BarcodeKind.QrCode, float moduleSize = 2f, int quietZone = 4, Action<BarcodeElement>? configure = null)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Barcode value cannot be null or whitespace.", nameof(value));
            var flow = _owner.GetFlow();
            var element = new BarcodeElement(value, kind, moduleSize, quietZone)
            {
                X = flow.X,
                Y = flow.Y
            };
            configure?.Invoke(element);
            element.X = flow.X;
            element.Y = flow.Y;
            _components.Add(new CanvasComponent(element, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection Svg(float width, float height, Action<SvgElement> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var flow = _owner.GetFlow();
            var element = new SvgElement(string.Empty, flow.X, flow.Y, Math.Max(0f, width), Math.Max(0f, height));
            configure(element);
            element.X = flow.X;
            element.Y = flow.Y;
            element.Refresh();
            _components.Add(new ImageComponent(element, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection Dynamic<T>(IEnumerable<T> source, Action<T, LayoutComponentCollection> configure)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            foreach (var item in source)
            {
                var childCollection = new LayoutComponentCollection(_owner);
                configure(item, childCollection);
                _components.AddRange(childCollection._components);
            }

            return this;
        }

        public sealed class LayerBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly LayerComponent _component = new LayerComponent();

            internal LayerBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public LayerBuilder Background(Action<LayoutComponentCollection> configure)
            {
                foreach (var child in _owner.BuildMany(configure, "Layer.Background"))
                    _component.AddBackground(child);
                return this;
            }

            public LayerBuilder Content(Action<LayoutComponentCollection> configure)
            {
                foreach (var child in _owner.BuildMany(configure, "Layer.Content"))
                    _component.AddContent(child);
                return this;
            }

            public LayerBuilder Foreground(Action<LayoutComponentCollection> configure)
            {
                foreach (var child in _owner.BuildMany(configure, "Layer.Foreground"))
                    _component.AddForeground(child);
                return this;
            }

            internal LayerComponent Build() => _component;
        }

        public sealed class DecorationBuilder
        {
            private Action<DecorationDrawContext>? _before;
            private Action<DecorationDrawContext>? _after;

            public DecorationBuilder Background(Action<DecorationDrawContext> action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                _before += action;
                return this;
            }

            public DecorationBuilder Foreground(Action<DecorationDrawContext> action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                _after += action;
                return this;
            }

            internal DecorationComponent Build(IMeasurable child) => new DecorationComponent(child, _before, _after);
        }

        public sealed class RelativeBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly List<(float Weight, IMeasurable Component)> _items = new();

            internal RelativeBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public float Spacing { get; set; }

            public RelativeBuilder Item(float weight, Action<LayoutComponentCollection> configure)
            {
                var child = _owner.BuildComposite(configure, "Relative.Item");
                _items.Add((weight, child));
                return this;
            }

            internal RelativeComponent Build()
            {
                if (_items.Count == 0)
                    throw new InvalidOperationException("Relative requires at least one item.");

                var component = new RelativeComponent { Spacing = Spacing };
                foreach (var (weight, child) in _items)
                    component.Add(weight, child);
                return component;
            }
        }

        public sealed class FlexRowBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly List<(float Weight, IMeasurable Component)> _items = new();

            internal FlexRowBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public float Gap { get; set; }

            public FlexRowBuilder Cell(float weight, Action<LayoutComponentCollection> configure)
            {
                var child = _owner.BuildComposite(configure, "FlexRow.Cell");
                _items.Add((weight, child));
                return this;
            }

            internal FlexRowComponent Build()
            {
                if (_items.Count == 0)
                    throw new InvalidOperationException("FlexRow requires at least one cell.");

                var component = new FlexRowComponent { Gap = Gap };
                foreach (var (weight, child) in _items)
                    component.Add(weight, child);
                return component;
            }
        }

        public sealed class FlexColumnBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly List<(float Weight, IMeasurable Component)> _items = new();

            internal FlexColumnBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public float Spacing { get; set; }

            public FlexColumnBuilder Cell(float weight, Action<LayoutComponentCollection> configure)
            {
                var child = _owner.BuildComposite(configure, "FlexColumn.Cell");
                _items.Add((weight, child));
                return this;
            }

            internal RelativeComponent Build()
            {
                if (_items.Count == 0)
                    throw new InvalidOperationException("FlexColumn requires at least one cell.");

                var component = new RelativeComponent { Spacing = Spacing };
                foreach (var (weight, child) in _items)
                    component.Add(weight, child);
                return component;
            }
        }

        public sealed class ColumnComponentBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly ColumnComponent _column = new ColumnComponent();
            private bool _hasItems;

            internal ColumnComponentBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public ColumnComponentBuilder Spacing(float value)
            {
                _column.Spacing = Math.Max(0f, value);
                return this;
            }

            public ColumnComponentBuilder Item(Action<ContentComposer> configure)
            {
                if (configure == null) throw new ArgumentNullException(nameof(configure));
                var child = _owner.BuildComposite(collection =>
                {
                    var composer = new ContentComposer(collection);
                    configure(composer);
                }, "Column.Item");
                _column.Add(child);
                _hasItems = true;
                return this;
            }

            internal ColumnComponent Build()
            {
                if (!_hasItems)
                    throw new InvalidOperationException("Column requires at least one item.");
                return _column;
            }
        }

        public sealed class RowComponentBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly RowComponent _row = new RowComponent();
            private bool _hasItems;

            internal RowComponentBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public RowComponentBuilder Gap(float value)
            {
                _row.Gap = Math.Max(0f, value);
                return this;
            }

            public RowComponentBuilder Item(Action<ContentComposer> configure)
            {
                if (configure == null) throw new ArgumentNullException(nameof(configure));
                var child = _owner.BuildComposite(collection =>
                {
                    var composer = new ContentComposer(collection);
                    configure(composer);
                }, "Row.Item");
                _row.Add(child, RowComponent.RowWidthSpec.Even());
                _hasItems = true;
                return this;
            }

            public RowComponentBuilder Even(Action<ContentComposer> configure)
            {
                return Item(configure);
            }

            public RowComponentBuilder Constant(float width, Action<ContentComposer> configure)
            {
                if (configure == null) throw new ArgumentNullException(nameof(configure));
                var child = _owner.BuildComposite(collection =>
                {
                    var composer = new ContentComposer(collection);
                    configure(composer);
                }, "Row.Constant");
                _row.Add(child, RowComponent.RowWidthSpec.Fixed(Math.Max(0f, width)));
                _hasItems = true;
                return this;
            }

            public RowComponentBuilder Auto(Action<ContentComposer> configure)
            {
                if (configure == null) throw new ArgumentNullException(nameof(configure));
                var child = _owner.BuildComposite(collection =>
                {
                    var composer = new ContentComposer(collection);
                    configure(composer);
                }, "Row.Auto");
                _row.Add(child, RowComponent.RowWidthSpec.Auto());
                _hasItems = true;
                return this;
            }

            public RowComponentBuilder Relative(float weight, Action<ContentComposer> configure)
            {
                if (configure == null) throw new ArgumentNullException(nameof(configure));
                var child = _owner.BuildComposite(collection =>
                {
                    var composer = new ContentComposer(collection);
                    configure(composer);
                }, "Row.Relative");
                float normalized = weight <= 0f ? 1f : weight;
                _row.Add(child, RowComponent.RowWidthSpec.Relative(normalized));
                _hasItems = true;
                return this;
            }

            internal RowComponent Build()
            {
                if (!_hasItems)
                    throw new InvalidOperationException("Row requires at least one item.");
                return _row;
            }
        }

        public sealed class StackComponentBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly StackComponent _stack = new StackComponent();
            private bool _hasItems;

            internal StackComponentBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public StackComponentBuilder Item(Action<ContentComposer> configure)
            {
                if (configure == null) throw new ArgumentNullException(nameof(configure));
                var child = _owner.BuildComposite(collection =>
                {
                    var composer = new ContentComposer(collection);
                    configure(composer);
                }, "Stack.Item");
                _stack.Add(child);
                _hasItems = true;
                return this;
            }

            internal StackComponent Build()
            {
                if (!_hasItems)
                    throw new InvalidOperationException("Stack requires at least one item.");
                return _stack;
            }
        }

        public sealed class GridComponentBuilder
        {
            private readonly LayoutComponentCollection _owner;
            private readonly GridComponent _grid = new GridComponent();
            private bool _hasItems;

            internal GridComponentBuilder(LayoutComponentCollection owner)
            {
                _owner = owner;
            }

            public GridComponentBuilder Columns(int value)
            {
                _grid.Columns = Math.Max(1, value);
                return this;
            }

            public GridComponentBuilder RowGap(float value)
            {
                _grid.RowGap = Math.Max(0f, value);
                return this;
            }

            public GridComponentBuilder ColumnGap(float value)
            {
                _grid.ColumnGap = Math.Max(0f, value);
                return this;
            }

            public GridComponentBuilder Item(Action<ContentComposer> configure)
            {
                if (configure == null) throw new ArgumentNullException(nameof(configure));
                var child = _owner.BuildComposite(collection =>
                {
                    var composer = new ContentComposer(collection);
                    configure(composer);
                }, "Grid.Item");
                _grid.Add(child);
                _hasItems = true;
                return this;
            }

            internal GridComponent Build()
            {
                if (!_hasItems)
                    throw new InvalidOperationException("Grid requires at least one item.");
                return _grid;
            }
        }

        public sealed class BorderOptions
        {
            public string StrokeColor { get; set; } = "#000000";
            public float StrokeWidth { get; set; } = 1f;
            public float[]? DashPattern { get; set; }
            public string? FillColor { get; set; }
            public float Opacity { get; set; } = 1f;
        }
    }
}



