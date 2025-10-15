using PdfBuilder.Document;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using System;
using System.Collections.Generic;

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

        private IMeasurable BuildComposite(Action<LayoutComponentCollection> configure, string? caller = null)
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

        private IEnumerable<IMeasurable> BuildMany(Action<LayoutComponentCollection> configure, string? caller = null)
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
            configure?.Invoke(element);
            _components.Add(new TextComponent(element, _owner.DefaultSpacing));
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

        public LayoutComponentCollection Row(Action<LayoutComponentCollection> configure, float gap = 12f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var row = new RowComponent { Gap = gap };
            foreach (var child in BuildMany(configure, nameof(Row)))
                row.Add(child);
            _components.Add(row);
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

        public LayoutComponentCollection Padding(float uniform, Action<LayoutComponentCollection> configure) =>
            Padding(uniform, uniform, uniform, uniform, configure);

        public LayoutComponentCollection Padding(float left, float top, float right, float bottom, Action<LayoutComponentCollection> configure)
        {
            var child = BuildComposite(configure, nameof(Padding));
            var component = new PaddingComponent(child, new PaddingValues(left, top, right, bottom));
            _components.Add(component);
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
                    var element = new DebugRectangleElement(rect.X, rect.Bottom, rect.Width, rect.Height)
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
                    var element = new DebugRectangleElement(rect.X, rect.Bottom, rect.Width, rect.Height)
                    {
                        FillColor = color,
                        StrokeWidth = 0f,
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
                MaxWidth = flow.Width,
                FontFamily = "Helvetica",
                FontSize = 11f
            };

            var run = new RichRun
            {
                Text = text,
                LinkUrl = url
            };
            configure?.Invoke(run);
            rich.Runs.Add(run);

            _components.Add(new RichTextComponent(rich, _owner.DefaultSpacing));
            return this;
        }

        public LayoutComponentCollection Relative(Action<RelativeBuilder> configure, float spacing = 0f)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var builder = new RelativeBuilder(this) { Spacing = spacing };
            configure(builder);
            _components.Add(builder.Build());
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


