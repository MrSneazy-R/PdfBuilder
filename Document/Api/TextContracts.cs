using PdfBuilder.Models;

namespace PdfBuilder.Document;

/// <summary>Configures reusable text style settings.</summary>
public interface ITextStyleDescriptor
{
    /// <summary>Sets the font family.</summary>
    ITextStyleDescriptor FontFamily(string family);
    /// <summary>Sets the font size in points.</summary>
    ITextStyleDescriptor FontSize(float size);
    /// <summary>Uses a bold font style.</summary>
    ITextStyleDescriptor Bold();
    /// <summary>Uses an italic font style.</summary>
    ITextStyleDescriptor Italic();
    /// <summary>Sets the text colour or a named colour token.</summary>
    ITextStyleDescriptor Color(string color);
    /// <summary>Sets an optional text highlight/background colour.</summary>
    ITextStyleDescriptor Highlight(string color);
    /// <summary>Sets the line-height multiplier.</summary>
    ITextStyleDescriptor LineHeight(float value);
    /// <summary>Sets extra spacing between glyphs in points.</summary>
    ITextStyleDescriptor LetterSpacing(float value);
    /// <summary>Sets extra spacing for whitespace glyphs in points.</summary>
    ITextStyleDescriptor WordSpacing(float value);
    /// <summary>Draws an underline.</summary>
    ITextStyleDescriptor Underline();
    /// <summary>Draws a strikethrough.</summary>
    ITextStyleDescriptor Strikethrough();
    /// <summary>Draws an overline where the active renderer supports it.</summary>
    ITextStyleDescriptor Overline();
    /// <summary>Sets decoration colour, thickness, and stroke style.</summary>
    ITextStyleDescriptor Decoration(string? color = null, float? thickness = null, TextDecorationStyle style = TextDecorationStyle.Solid);
    /// <summary>Raises text relative to the paragraph baseline.</summary>
    ITextStyleDescriptor Superscript();
    /// <summary>Lowers text relative to the paragraph baseline.</summary>
    ITextStyleDescriptor Subscript();
    /// <summary>Aligns text to the left.</summary>
    ITextStyleDescriptor AlignLeft();
    /// <summary>Centres text.</summary>
    ITextStyleDescriptor AlignCenter();
    /// <summary>Aligns text to the right.</summary>
    ITextStyleDescriptor AlignRight();
    /// <summary>Justifies non-final wrapped lines.</summary>
    ITextStyleDescriptor Justify();
    /// <summary>Selects automatic, left-to-right, or right-to-left direction.</summary>
    ITextStyleDescriptor Direction(TextDirection direction);
    /// <summary>Enables ordinary wrapping.</summary>
    ITextStyleDescriptor Wrap();
    /// <summary>Disables automatic wrapping.</summary>
    ITextStyleDescriptor NoWrap();
    /// <summary>Enables wrapping with hyphenation of overlong words.</summary>
    ITextStyleDescriptor Hyphenate();
    /// <summary>Ellipsizes constrained final text.</summary>
    ITextStyleDescriptor Ellipsis();
    /// <summary>Limits the visible paragraph to a maximum number of lines.</summary>
    ITextStyleDescriptor MaximumLines(int value);
    /// <summary>Sets the ordered fallback-font chain.</summary>
    ITextStyleDescriptor FallbackFonts(params string[] families);
}

/// <summary>Configures text content added to a container.</summary>
public interface ITextDescriptor : ITextStyleDescriptor
{
    /// <summary>Applies a named text style from the current document theme.</summary>
    ITextDescriptor Style(string name);
}

/// <summary>Configures one canonical rich-text paragraph.</summary>
public interface IRichTextDescriptor
{
    /// <summary>Returns the paragraph/default span style.</summary>
    ITextDescriptor DefaultStyle();
    /// <summary>Adds an independently styled span.</summary>
    ITextDescriptor Span(string text);
    /// <summary>Adds an independently styled span linked to an allowed external URI.</summary>
    ITextDescriptor ExternalLink(string text, string uri);
    /// <summary>Adds an independently styled span linked to an internal anchor.</summary>
    ITextDescriptor InternalLink(string text, string anchorId);
}
