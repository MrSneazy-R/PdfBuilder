using PdfBuilder.Models;

namespace PdfBuilder.Document;

/// <summary>Configures a canonical vector chart.</summary>
public interface IChartDescriptor
{
    /// <summary>Sets the chart size in points.</summary>
    void Size(float width, float height);
    /// <summary>Sets the chart title.</summary>
    void Title(string value);
    /// <summary>Applies common typography to chart axis and legend labels where supported.</summary>
    void LabelStyle(Action<ITextStyleDescriptor> configure);
    /// <summary>Adds a line series with values plotted against ordinal positions.</summary>
    void Line(string name, IEnumerable<float> values, PdfColor color, float strokeWidth = 1f);
    /// <summary>Adds a bar series with values plotted against ordinal positions.</summary>
    void Bars(string name, IEnumerable<float> values, PdfColor color);
}
