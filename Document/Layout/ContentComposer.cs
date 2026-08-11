using System;
using System.Collections.Generic;
using PdfBuilder.Document.Layout.Components;
using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout
{
    /// <summary>
    /// Fluent wrapper over <see cref="LayoutComponentCollection"/> for more expressive DSL-style content composition.
    /// </summary>
    public sealed class ContentComposer
    {
        private readonly LayoutComponentCollection _collection;

        internal ContentComposer(LayoutComponentCollection collection)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public ContentComposer Component(IMeasurable component)
        {
            _collection.Component(component);
            return this;
        }

        internal ContentComposer DebugLabel(string label, Action<ContentComposer> configure)
        {
            _collection.DebugLabel(label, inner => configure(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Component(
            Func<LayoutMeasureContext, LayoutMeasurement> measure,
            Action<LayoutDrawContext, LayoutMeasurement> draw)
        {
            _collection.Component(measure, draw);
            return this;
        }

        public ContentComposer ShowOnce(string key, Action<ContentComposer> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            _collection.ShowOnce(key, inner => configure(new ContentComposer(inner)));
            return this;
        }

        /// <summary>Forces the next composed component onto a new page.</summary>
        internal ContentComposer PageBreak()
        {
            _collection.PageBreak();
            return this;
        }

        internal ContentComposer EnsureSpace(float minimumHeight, Action<ContentComposer> configure)
        {
            _collection.EnsureSpace(minimumHeight, inner => configure(new ContentComposer(inner)));
            return this;
        }

        internal ContentComposer KeepTogether(Action<ContentComposer> configure)
        {
            _collection.KeepTogether(inner => configure(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Text(string content, Action<TextElement>? configure = null)
        {
            _collection.Text(content, configure);
            return this;
        }

        public ContentComposer RichText(Action<RichTextElement> configure)
        {
            _collection.RichText(configure);
            return this;
        }

        public ContentComposer List(Action<ListElement> configure)
        {
            _collection.List(configure);
            return this;
        }

        internal ContentComposer Table(TableElement table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            _collection.Owner.ApplyTableDefaults(table);
            _collection.Component(new TableComponent(table));
            return this;
        }

        public ContentComposer Column(Action<LayoutComponentCollection.ColumnComponentBuilder> configure)
        {
            _collection.Column(configure);
            return this;
        }

        public ContentComposer Column(Action<LayoutComponentCollection> configure, float spacing = 8f)
        {
            _collection.Column(configure, spacing);
            return this;
        }

        public ContentComposer Row(Action<LayoutComponentCollection.RowComponentBuilder> configure)
        {
            _collection.Row(configure);
            return this;
        }

        public ContentComposer Row(Action<LayoutComponentCollection> configure, float gap = 12f)
        {
            _collection.Row(configure, gap);
            return this;
        }

        public ContentComposer Stack(Action<LayoutComponentCollection.StackComponentBuilder> configure)
        {
            _collection.Stack(configure);
            return this;
        }

        public ContentComposer Stack(Action<LayoutComponentCollection> configure)
        {
            _collection.Stack(configure);
            return this;
        }

        public ContentComposer Grid(Action<LayoutComponentCollection.GridComponentBuilder> configure)
        {
            _collection.Grid(configure);
            return this;
        }

        public ContentComposer Grid(int columns, Action<LayoutComponentCollection> configure, float rowGap = 12f, float columnGap = 12f)
        {
            _collection.Grid(columns, configure, rowGap, columnGap);
            return this;
        }

        public ContentComposer Padding(float uniform, Action<ContentComposer> configure)
        {
            _collection.Padding(uniform, inner => configure(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Padding(float left, float top, float right, float bottom, Action<ContentComposer> configure)
        {
            _collection.Padding(left, top, right, bottom, inner => configure(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Align(LayoutHorizontalAlignment horizontal, LayoutVerticalAlignment vertical, Action<ContentComposer> configure, float? minHeight = null)
        {
            _collection.Align(horizontal, vertical, inner => configure(new ContentComposer(inner)), minHeight);
            return this;
        }

        public ContentComposer Absolute(float offsetX, float offsetY, Action<ContentComposer> content)
        {
            _collection.Absolute(offsetX, offsetY, inner => content(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Layer(Action<LayoutComponentCollection.LayerBuilder> configure)
        {
            _collection.Layer(configure);
            return this;
        }

        public ContentComposer Decorate(Action<LayoutComponentCollection.DecorationBuilder> configure, Action<ContentComposer> content)
        {
            _collection.Decorate(configure, inner => content(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Border(Action<LayoutComponentCollection.BorderOptions>? configure, Action<ContentComposer> content)
        {
            _collection.Border(configure, inner => content(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Background(string color, Action<ContentComposer> content, float opacity = 1f)
        {
            _collection.Background(color, inner => content(new ContentComposer(inner)), opacity);
            return this;
        }

        public ContentComposer When(bool condition, Action<ContentComposer> whenTrue, Action<ContentComposer>? whenFalse = null)
        {
            _collection.When(condition,
                inner => whenTrue(new ContentComposer(inner)),
                whenFalse == null ? null : inner => whenFalse(new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Repeat<T>(IEnumerable<T> source, Action<T, int, ContentComposer> configure)
        {
            _collection.RepeatEach(source, (item, index, inner) => configure(item, index, new ContentComposer(inner)));
            return this;
        }

        public ContentComposer Repeat(int count, Action<int, ContentComposer> configure)
        {
            _collection.Repeat(count, (index, inner) => configure(index, new ContentComposer(inner)));
            return this;
        }

        public ContentComposer DefaultTextStyle(Action<TextStyleDefaults> configure)
        {
            _collection.DefaultTextStyle(configure);
            return this;
        }

        public ContentComposer Size(
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
            _collection.Size(
                configure,
                minWidth,
                maxWidth,
                width,
                minHeight,
                maxHeight,
                height,
                aspectRatio,
                fillWidth,
                fillHeight,
                shrinkWidth,
                shrinkHeight);
            return this;
        }

        public ContentComposer MinHeight(float value, Action<ContentComposer> configure)
        {
            _collection.MinHeight(value, configure);
            return this;
        }

        public ContentComposer MaxHeight(float value, Action<ContentComposer> configure)
        {
            _collection.MaxHeight(value, configure);
            return this;
        }

        public ContentComposer Height(float value, Action<ContentComposer> configure)
        {
            _collection.Height(value, configure);
            return this;
        }

        public ContentComposer MinWidth(float value, Action<ContentComposer> configure)
        {
            _collection.MinWidth(value, configure);
            return this;
        }

        public ContentComposer MaxWidth(float value, Action<ContentComposer> configure)
        {
            _collection.MaxWidth(value, configure);
            return this;
        }

        public ContentComposer Width(float value, Action<ContentComposer> configure)
        {
            _collection.Width(value, configure);
            return this;
        }

        public ContentComposer AspectRatio(float ratio, Action<ContentComposer> configure)
        {
            _collection.AspectRatio(ratio, configure);
            return this;
        }

        public ContentComposer Extend(Action<ContentComposer> configure)
        {
            _collection.Size(configure, fillWidth: true, fillHeight: true);
            return this;
        }

        public ContentComposer ExtendHeight(Action<ContentComposer> configure)
        {
            _collection.Size(configure, fillHeight: true);
            return this;
        }

        public ContentComposer ExtendWidth(Action<ContentComposer> configure)
        {
            _collection.Size(configure, fillWidth: true);
            return this;
        }

        public ContentComposer Shrink(Action<ContentComposer> configure)
        {
            _collection.Size(configure, shrinkWidth: true, shrinkHeight: true);
            return this;
        }

        public ContentComposer ShrinkHeight(Action<ContentComposer> configure)
        {
            _collection.Size(configure, shrinkHeight: true);
            return this;
        }

        public ContentComposer ShrinkWidth(Action<ContentComposer> configure)
        {
            _collection.Size(configure, shrinkWidth: true);
            return this;
        }

        public ContentComposer Image(byte[] data, float width, float height, Action<ImageElement>? configure = null)
        {
            _collection.Image(data, width, height, configure);
            return this;
        }

        public ContentComposer Canvas(float width, float height, Action<CanvasBuilder> draw, Action<CanvasElement>? configure = null)
        {
            _collection.Canvas(width, height, draw, configure);
            return this;
        }

        public ContentComposer Barcode(string value, BarcodeKind kind = BarcodeKind.QrCode, float moduleSize = 2f, int quietZone = 4, Action<BarcodeElement>? configure = null)
        {
            _collection.Barcode(value, kind, moduleSize, quietZone, configure);
            return this;
        }

        public ContentComposer Svg(float width, float height, Action<SvgElement> configure)
        {
            _collection.Svg(width, height, configure);
            return this;
        }
    }
}
