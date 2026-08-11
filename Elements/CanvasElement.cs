using System.Collections.Generic;
using PdfBuilder.Document;

public class CanvasElement : PdfElement
{
    private readonly Dictionary<CanvasLayer, IList<string>> _layerCommands = new()
    {
        [CanvasLayer.Background] = new List<string>(),
        [CanvasLayer.Content] = new List<string>(),
        [CanvasLayer.Foreground] = new List<string>()
    };

    public CanvasElement(float x, float y, float width, float height) : base(x, y)
    {
        Width = width;
        Height = height;
    }

    public float Width { get; set; }
    public float Height { get; set; }

    public float? MarginTop { get; set; }
    public float? MarginBottom { get; set; }
    public float? MarginLeft { get; set; }
    public float? MarginRight { get; set; }

    public bool AvoidBreakInside { get; set; }

    /// <summary>Legacy content-layer commands. Prefer the canonical canvas API.</summary>
    public IList<string> Commands => _layerCommands[CanvasLayer.Content];

    internal IList<string> CommandsFor(CanvasLayer layer) => _layerCommands[layer];

    internal IEnumerable<string> EnumerateCommands()
    {
        foreach (CanvasLayer layer in new[] { CanvasLayer.Background, CanvasLayer.Content, CanvasLayer.Foreground })
            foreach (string command in _layerCommands[layer])
                yield return command;
    }

    internal int CommandCount => _layerCommands.Values.Sum(commands => commands.Count);

    internal int CommandBytes => _layerCommands.Values
        .SelectMany(commands => commands)
        .Sum(command => System.Text.Encoding.UTF8.GetByteCount(command));

    internal int MaximumEffectStepsUsed { get; set; }

    internal void ClearCommands()
    {
        foreach (IList<string> commands in _layerCommands.Values)
            commands.Clear();
        MaximumEffectStepsUsed = 0;
    }
}
