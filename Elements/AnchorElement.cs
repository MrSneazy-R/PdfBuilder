using PdfBuilder.Document;

public sealed class AnchorElement : PdfElement
{
    public string Id { get; set; } = "";           // unique anchor name
    public string? Title { get; set; }             // for outline/TOC
    public int Level { get; set; } = 1;            // 1..6 typical

    public AnchorElement(string id, float x, float y) : base(x, y) { Id = id; }
}
