namespace PdfBuilder.Document
{
    public sealed class AnchorBuilder
    {
        private readonly ColumnBuilder _col;
        private readonly AnchorElement _a;
        public AnchorBuilder(ColumnBuilder col, string id, float x, float y) { _col = col; _a = new AnchorElement(id, x, y); }
        public AnchorBuilder Title(string t) { _a.Title = t; return this; }
        public AnchorBuilder Level(int lvl) { _a.Level = lvl; return this; }
        public ColumnBuilder Add() { _col.AddAnchor(_a); return _col; }
    }
}
