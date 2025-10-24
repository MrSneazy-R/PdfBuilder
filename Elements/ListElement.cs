using PdfBuilder.Document;
using PdfBuilder.Models;
using System.Collections.Generic;

public sealed class ListElement : PdfElement
{
    public ListMarker Marker { get; set; } = ListMarker.Bullet;
    public float IndentPerLevel { get; set; } = 18f;   // left indent added for each level
    public float BulletGap { get; set; } = 6f;         // space between marker and text
    public float ItemSpacing { get; set; } = 4f;       // vertical gap between items
    public float LineHeight { get; set; } = 1.25f;

    public string FontFamily { get; set; } = "Helvetica";
    public float FontSize { get; set; } = 11f;
    public string Color { get; set; } = "#000";
    public FlowDirection FlowDirection { get; set; } = FlowDirection.LeftToRight;

    public List<ListItem> Items { get; } = new();

    public float? MarginTop { get; set; }
    public float? MarginBottom { get; set; }

    public float? MaxWidth { get; set; } = null;        // width allocated to the whole list
    public ListElement(float x, float y) : base(x, y) { }

    public bool KeepWithNext { get; set; } = false;
    public bool AvoidBreakInside { get; set; } = true;

}
