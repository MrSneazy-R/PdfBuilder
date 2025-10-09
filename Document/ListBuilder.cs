using PdfBuilder.Models;
using System.Collections.Generic;

namespace PdfBuilder.Document
{
    public sealed class ListBuilder
    {
        private readonly ColumnBuilder _col;
        private readonly ListElement _list;
        private readonly Stack<ListItem> _stack = new();

        public ListBuilder(ColumnBuilder col, float x, float y, float defaultWidth)
        {
            _col = col;
            _list = new ListElement(x, y) { MaxWidth = defaultWidth };
        }

        public ListBuilder Marker(ListMarker m) { _list.Marker = m; return this; }
        public ListBuilder Font(string family, float size) { _list.FontFamily = family; _list.FontSize = size; return this; }
        public ListBuilder Colors(string hex) { _list.Color = hex; return this; }
        public ListBuilder Indent(float v) { _list.IndentPerLevel = v; return this; }
        public ListBuilder ItemSpacing(float v) { _list.ItemSpacing = v; return this; }
        public ListBuilder LineHeight(float v) { _list.LineHeight = v; return this; }

        public ListBuilder Item(params RichRun[] runs)
        {
            var it = new ListItem { Content = new List<RichRun>(runs) };
            if (_stack.Count == 0) _list.Items.Add(it); else _stack.Peek().Children.Add(it);
            return this;
        }

        public ListBuilder BeginNest() { _stack.Push(_list.Items.Count > 0 ? _list.Items[^1] : new ListItem()); return this; }
        public ListBuilder EndNest() { if (_stack.Count > 0) _stack.Pop(); return this; }

        public ColumnBuilder Add() { _col.AddList(_list); return _col; }
    }
}
