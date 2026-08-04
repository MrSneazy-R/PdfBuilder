using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document;

/// <summary>Composes reusable PDF content into a canonical container.</summary>
public interface IPdfComponent
{
    /// <summary>Composes this component.</summary>
    void Compose(IContainer container);
}

/// <summary>Composes reusable PDF content from a caller-owned model.</summary>
/// <typeparam name="TModel">The model type supplied by the caller.</typeparam>
public interface IPdfComponent<in TModel>
{
    /// <summary>Composes this component without mutating <paramref name="model"/>.</summary>
    void Compose(IContainer container, TModel model);
}

/// <summary>Base class for typed, side-effect-free document templates.</summary>
/// <typeparam name="TModel">The model type supplied to composition.</typeparam>
public abstract class PdfTemplate<TModel>
{
    /// <summary>Composes a document from <paramref name="model"/>.</summary>
    public abstract void Compose(IDocumentDescriptor document, TModel model);

    /// <summary>Creates a document from the supplied model.</summary>
    public PdfDocument Create(TModel model) => PdfDocument.Create(document => Compose(document, model));

    /// <summary>Generates PDF bytes from the supplied model.</summary>
    public byte[] GenerateBytes(TModel model, CancellationToken cancellationToken = default) => Create(model).GenerateBytes(cancellationToken);

    /// <summary>Generates a PDF into <paramref name="destination"/> from the supplied model.</summary>
    public void Generate(Stream destination, TModel model, CancellationToken cancellationToken = default)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        Create(model).Generate(destination, cancellationToken);
    }
}

/// <summary>Document-scoped theme values shared by canonical composition.</summary>
public sealed class DocumentTheme
{
    private readonly Dictionary<string, string> _colors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextStyle> _styles = new(StringComparer.Ordinal);

    /// <summary>Gets the default text style applied to canonical pages.</summary>
    public TextStyle DefaultTextStyle { get; } = new();
    /// <summary>Gets default page settings reserved for canonical page defaults.</summary>
    public PageTheme Page { get; } = new();
    /// <summary>Gets named spacing values.</summary>
    public SpacingTheme Spacing { get; } = new();

    /// <summary>Configures the default text style.</summary>
    /// <summary>Configures the default text style.</summary>
    public void ConfigureDefaultTextStyle(Action<TextStyle> configure) { ArgumentNullException.ThrowIfNull(configure); configure(DefaultTextStyle); }
    /// <summary>Registers a named colour.</summary>
    public void Color(string name, string color) { _colors[ValidateName(name)] = ValidateColor(color); }
    /// <summary>Registers a named text style.</summary>
    public void TextStyle(string name, Action<TextStyle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var style = new TextStyle(); configure(style); _styles[ValidateName(name)] = style.Clone();
    }
    /// <summary>Registers a named spacing value in points.</summary>
    /// <summary>Registers a named spacing value in points.</summary>
    public void SetSpacing(string name, float value) => Spacing.Set(name, value);

    internal bool TryGetColor(string name, out string color) => _colors.TryGetValue(name, out color!);
    internal bool TryGetTextStyle(string name, out TextStyle style) => _styles.TryGetValue(name, out style!);
    private static string ValidateName(string name) => string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A name is required.", nameof(name)) : name;
    private static string ValidateColor(string color) => string.IsNullOrWhiteSpace(color) ? throw new ArgumentException("A colour is required.", nameof(color)) : color;
}

/// <summary>Shared page defaults for a document theme.</summary>
public sealed class PageTheme { }

/// <summary>Stores named spacing values in points.</summary>
public sealed class SpacingTheme
{
    private readonly Dictionary<string, float> _values = new(StringComparer.Ordinal);
    /// <summary>Sets a named spacing value.</summary>
    public void Set(string name, float value)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A name is required.", nameof(name));
        if (value < 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        _values[name] = value;
    }
    /// <summary>Gets a named spacing value.</summary>
    public float Get(string name) => _values.TryGetValue(name, out var value) ? value : throw new KeyNotFoundException($"Spacing '{name}' is not defined.");
}

/// <summary>Reusable canonical text style data.</summary>
public sealed class TextStyle
{
    internal string? Family { get; private set; }
    internal float? Size { get; private set; }
    internal bool IsBold { get; private set; }
    internal string? TextColor { get; private set; }
    /// <summary>Sets the font family.</summary>
    public TextStyle FontFamily(string family) { Family = string.IsNullOrWhiteSpace(family) ? throw new ArgumentException("A font family is required.", nameof(family)) : family; return this; }
    /// <summary>Sets the font size in points.</summary>
    public TextStyle FontSize(float size) { Size = size <= 0f ? throw new ArgumentOutOfRangeException(nameof(size)) : size; return this; }
    /// <summary>Uses a bold font style.</summary>
    public TextStyle Bold() { IsBold = true; return this; }
    /// <summary>Sets a text colour or named theme colour.</summary>
    public TextStyle Color(string color) { TextColor = string.IsNullOrWhiteSpace(color) ? throw new ArgumentException("A colour is required.", nameof(color)) : color; return this; }
    internal TextStyle Clone() => new() { Family = Family, Size = Size, IsBold = IsBold, TextColor = TextColor };
    internal void Apply(TextStyleDefaults defaults, DocumentTheme? theme)
    {
        if (Family != null) defaults.FontFamily = Family; if (Size.HasValue) defaults.FontSize = Size; if (IsBold) defaults.Bold = true;
    }
    internal void Apply(TextElement element, DocumentTheme? theme)
    {
        if (Family != null) element.FontFamily = Family; if (Size.HasValue) element.FontSize = Size.Value; if (IsBold) element.Bold = true;
        if (TextColor != null) element.Color = theme != null && theme.TryGetColor(TextColor, out var color) ? color : TextColor;
    }
}
