using System;
using System.Collections.Generic;
using System.Linq;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using TableModels = PdfBuilder.Elements.Table;

namespace PdfBuilder.Document.Layout
{
    internal static class LayoutSplitUtils
    {
        public static TextElement CloneText(TextElement source, string? overrideText = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var clone = new TextElement(source.Text, source.X, source.Y)
            {
                FontSize = source.FontSize,
                FontFamily = source.FontFamily,
                Bold = source.Bold,
                Italic = source.Italic,
                Underline = source.Underline,
                Strikethrough = source.Strikethrough,
                Overline = source.Overline,
                SmallCaps = source.SmallCaps,
                Monospace = source.Monospace,
                Color = source.Color,
                Opacity = source.Opacity,
                BackgroundColor = source.BackgroundColor,
                BackgroundBorderColor = source.BackgroundBorderColor,
                BackgroundBorderWidth = source.BackgroundBorderWidth,
                BackgroundCornerRadius = source.BackgroundCornerRadius,
                BackgroundCornerRadiusTopLeft = source.BackgroundCornerRadiusTopLeft,
                BackgroundCornerRadiusTopRight = source.BackgroundCornerRadiusTopRight,
                BackgroundCornerRadiusBottomLeft = source.BackgroundCornerRadiusBottomLeft,
                BackgroundCornerRadiusBottomRight = source.BackgroundCornerRadiusBottomRight,
                BackgroundShadowOffsetX = source.BackgroundShadowOffsetX,
                BackgroundShadowOffsetY = source.BackgroundShadowOffsetY,
                BackgroundShadowBlur = source.BackgroundShadowBlur,
                BackgroundShadowColor = source.BackgroundShadowColor,
                MarginTop = source.MarginTop,
                MarginBottom = source.MarginBottom,
                MarginLeft = source.MarginLeft,
                MarginRight = source.MarginRight,
                PaddingTop = source.PaddingTop,
                PaddingBottom = source.PaddingBottom,
                PaddingLeft = source.PaddingLeft,
                PaddingRight = source.PaddingRight,
                MaxWidth = source.MaxWidth,
                LineHeight = source.LineHeight,
                LetterSpacing = source.LetterSpacing,
                WordSpacing = source.WordSpacing,
                Wrapping = source.Wrapping,
                EllipsisWhenConstrained = source.EllipsisWhenConstrained,
                MaximumLines = source.MaximumLines,
                Rotation = source.Rotation,
                Alignment = source.Alignment,
                FlowDirection = source.FlowDirection,
                Direction = source.Direction,
                BaselineOffset = source.BaselineOffset,
                Transform = source.Transform,
                DecorationColor = source.DecorationColor,
                DecorationThickness = source.DecorationThickness,
                DecorationStyle = source.DecorationStyle,
                KeepWithNext = source.KeepWithNext,
                AvoidBreakInside = source.AvoidBreakInside,
                WidowLines = source.WidowLines,
                OrphanLines = source.OrphanLines,
                PageTextTemplate = source.PageTextTemplate,
                PageReferenceAnchorId = source.PageReferenceAnchorId,
                PageReferenceFormat = source.PageReferenceFormat,
                PageReferencePendingText = source.PageReferencePendingText,
                ThemeStyleName = source.ThemeStyleName,
                CanonicalStyleOverrides = source.CanonicalStyleOverrides?.Clone(),
                ShapedLayout = source.ShapedLayout,
                ShapedStartLine = source.ShapedStartLine,
                ShapedLineCount = source.ShapedLineCount
            };

            if (overrideText != null)
                clone.Text = overrideText;

            if (source.Spans.Count > 0)
            {
                clone.Spans.Clear();
                foreach (var span in source.Spans)
                    clone.Spans.Add(span.Clone());
            }

            return clone;
        }

        public static ListElement CloneList(ListElement source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var clone = new ListElement(source.X, source.Y)
            {
                Marker = source.Marker,
                IndentPerLevel = source.IndentPerLevel,
                BulletGap = source.BulletGap,
                ItemSpacing = source.ItemSpacing,
                LineHeight = source.LineHeight,
                FontFamily = source.FontFamily,
                FontSize = source.FontSize,
                Color = source.Color,
                MarginTop = source.MarginTop,
                MarginBottom = source.MarginBottom,
                MaxWidth = source.MaxWidth,
                KeepWithNext = source.KeepWithNext,
                AvoidBreakInside = source.AvoidBreakInside
            };

            foreach (var item in source.Items)
                clone.Items.Add(CloneListItem(item));

            return clone;
        }

        public static RichTextElement CloneRichText(RichTextElement source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var clone = new RichTextElement(source.X, source.Y)
            {
                FontFamily = source.FontFamily,
                FontSize = source.FontSize,
                LineHeight = source.LineHeight,
                Alignment = source.Alignment,
                Color = source.Color,
                BackgroundColor = source.BackgroundColor,
                FallbackFonts = source.FallbackFonts?.ToList(),
                Wrapping = source.Wrapping,
                EllipsisWhenConstrained = source.EllipsisWhenConstrained,
                MaximumLines = source.MaximumLines,
                MarginTop = source.MarginTop,
                MarginBottom = source.MarginBottom,
                MarginLeft = source.MarginLeft,
                MarginRight = source.MarginRight,
                PaddingTop = source.PaddingTop,
                PaddingBottom = source.PaddingBottom,
                PaddingLeft = source.PaddingLeft,
                PaddingRight = source.PaddingRight,
                MaxWidth = source.MaxWidth,
                Rotation = source.Rotation,
                FlowDirection = source.FlowDirection,
                Direction = source.Direction,
                KeepWithNext = source.KeepWithNext,
                AvoidBreakInside = source.AvoidBreakInside,
                ShapedLayout = source.ShapedLayout,
                ShapedLayoutWidth = source.ShapedLayoutWidth,
                ShapedStartLine = source.ShapedStartLine,
                ShapedLineCount = source.ShapedLineCount
            };
            foreach (var run in source.Runs)
            {
                clone.Runs.Add(new RichRun
                {
                    Text = run.Text,
                    FontFamily = run.FontFamily,
                    FontSize = run.FontSize,
                    Bold = run.Bold,
                    Italic = run.Italic,
                    Monospace = run.Monospace,
                    Underline = run.Underline,
                    Strikethrough = run.Strikethrough,
                    SmallCaps = run.SmallCaps,
                    Color = run.Color,
                    BackgroundColor = run.BackgroundColor,
                    FallbackFonts = run.FallbackFonts?.ToList(),
                    LetterSpacing = run.LetterSpacing,
                    WordSpacing = run.WordSpacing,
                    Transform = run.Transform,
                    Overline = run.Overline,
                    DecorationColor = run.DecorationColor,
                    DecorationThickness = run.DecorationThickness,
                    DecorationStyle = run.DecorationStyle,
                    Superscript = run.Superscript,
                    Subscript = run.Subscript,
                    LinkUrl = run.LinkUrl,
                    LinkAnchor = run.LinkAnchor
                });
            }
            return clone;
        }

        public static TableElement CloneTable(TableElement source)
        {
            var clone = CloneTableStructure(source);

            foreach (var row in source.Rows)
            {
                source.LayoutDiagnostics?.RecordTableRowClone();
                clone.Rows.Add(CloneRow(row));
            }

            return clone;
        }

        public static TableElement CloneTableStructure(TableElement source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            source.LayoutDiagnostics?.RecordTableClone();

            var clone = new TableElement(source.X, source.Y)
            {
                TableWidth = source.TableWidth,
                CaptionText = source.CaptionText,
                CaptionAlign = source.CaptionAlign,
                HeaderBackground = source.HeaderBackground,
                AltRowBackground = source.AltRowBackground,
                AltRowEvery = source.AltRowEvery,
                AltRowStartIndex = source.AltRowStartIndex,
                BorderColor = source.BorderColor,
                BorderWidth = source.BorderWidth,
                BorderStyle = source.BorderStyle,
                CellPadding = source.CellPadding,
                DefaultFont = source.DefaultFont,
                DefaultFontSize = source.DefaultFontSize,
                BorderCollapse = source.BorderCollapse,
                OuterBorder = CloneBorderStyle(source.OuterBorder),
                InnerBorder = CloneBorderStyle(source.InnerBorder),
                RowBanding = CloneRowBanding(source.RowBanding),
                ColumnBanding = CloneColumnBanding(source.ColumnBanding),
                DefaultTextStyle = CloneTextStyle(source.DefaultTextStyle),
                OuterCornerRadiusTopLeft = source.OuterCornerRadiusTopLeft,
                OuterCornerRadiusTopRight = source.OuterCornerRadiusTopRight,
                OuterCornerRadiusBottomRight = source.OuterCornerRadiusBottomRight,
                OuterCornerRadiusBottomLeft = source.OuterCornerRadiusBottomLeft,
                RowBandOffset = source.RowBandOffset,
                ResolvedColumnWidths = source.ResolvedColumnWidths?.ToArray(),
                FooterRepeatMode = source.FooterRepeatMode,
                EnablePageBreaks = source.EnablePageBreaks,
                RepeatHeaders = source.RepeatHeaders,
                MinRowsAtPageStart = source.MinRowsAtPageStart,
                MinRowsAtPageEnd = source.MinRowsAtPageEnd,
                AllowRowSplitting = source.AllowRowSplitting,
                PageTopY = source.PageTopY,
                PageBottomY = source.PageBottomY,
                OnPageBreak = source.OnPageBreak,
                HeaderRowCount = source.HeaderRowCount,
                ResolveBorderConflicts = source.ResolveBorderConflicts,
                DrawOuterFrame = source.DrawOuterFrame,
                OuterFrameColor = source.OuterFrameColor,
                OuterFrameWidth = source.OuterFrameWidth,
                OverflowPolicy = source.OverflowPolicy,
                AutoSizeColumns = source.AutoSizeColumns,
                KeepWithNext = source.KeepWithNext,
                AvoidBreakInside = source.AvoidBreakInside
            };
            clone.LayoutDiagnostics = source.LayoutDiagnostics;

            clone.ColumnWidths.AddRange(source.ColumnWidths);
            foreach (var definition in source.ColumnDefinitions)
                clone.ColumnDefinitions.Add(definition.Clone());

            foreach (var style in source.ColumnStyles)
            {
                clone.ColumnStyles.Add(new TableColumnStyle
                {
                    Index = style.Index,
                    HAlign = style.HAlign,
                    VAlign = style.VAlign,
                    Font = style.Font,
                    FontSize = style.FontSize,
                    TextColor = style.TextColor,
                    Background = style.Background,
                    PaddingTop = style.PaddingTop,
                    PaddingRight = style.PaddingRight,
                    PaddingBottom = style.PaddingBottom,
                    PaddingLeft = style.PaddingLeft,
                    OverrideWidth = style.OverrideWidth
                });
            }

            return clone;
        }

        public static TableElement CloneTableWithRows(TableElement source, IReadOnlyList<TableRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var clone = CloneTableStructure(source);
            foreach (TableRow row in rows)
            {
                source.LayoutDiagnostics?.RecordTableRowClone();
                clone.Rows.Add(CloneRow(row));
            }
            return clone;
        }

        public static ListItem CloneListItem(ListItem item)
        {
            var clone = new ListItem();
            foreach (var run in item.Content)
                clone.Content.Add(CloneRichRun(run));
            foreach (var child in item.Children)
                clone.Children.Add(CloneListItem(child));
            return clone;
        }

        private static RichRun CloneRichRun(RichRun run)
        {
            return new RichRun
            {
                Text = run.Text,
                FontFamily = run.FontFamily,
                FontSize = run.FontSize,
                Bold = run.Bold,
                Italic = run.Italic,
                Underline = run.Underline,
                Strikethrough = run.Strikethrough,
                SmallCaps = run.SmallCaps,
                Color = run.Color,
                LinkUrl = run.LinkUrl,
                LinkAnchor = run.LinkAnchor
            };
        }

        private static TableRow CloneRow(TableRow row)
        {
            var clone = new TableRow
            {
                BackgroundColor = row.BackgroundColor,
                RowHeight = row.RowHeight,
                IsHeader = row.IsHeader,
                IsFooter = row.IsFooter,
                ExplicitRowIndex = row.ExplicitRowIndex,
                BandIndex = row.BandIndex,
                AllowSplit = row.AllowSplit,
                SemanticDescriptor = row.SemanticDescriptor,
                KeepWithNext = row.KeepWithNext,
                ThickTopBorder = row.ThickTopBorder,
                ThickBottomBorder = row.ThickBottomBorder,
                ThickBorderWidth = row.ThickBorderWidth,
                ThickBorderColor = row.ThickBorderColor
            };

            foreach (var cell in row.Cells)
                clone.Cells.Add(CloneCell(cell));

            return clone;
        }

        private static TableCell CloneCell(TableCell cell)
        {
            var clone = new TableCell
            {
                Text = cell.Text,
                ThemeStyleName = cell.ThemeStyleName,
                TextStyle = cell.TextStyle == null ? null : CloneTextStyle(cell.TextStyle),
                Font = cell.Font,
                FontSize = cell.FontSize,
                TextColor = cell.TextColor,
                Bold = cell.Bold,
                Italic = cell.Italic,
                Underline = cell.Underline,
                Strikethrough = cell.Strikethrough,
                Overline = cell.Overline,
                SmallCaps = cell.SmallCaps,
                LineHeight = cell.LineHeight,
                MaxLines = cell.MaxLines,
                WordBreak = cell.WordBreak,
                RotationDegrees = cell.RotationDegrees,
                HorizontalAlign = cell.HorizontalAlign,
                VerticalAlign = cell.VerticalAlign,
                BackgroundColor = cell.BackgroundColor,
                CornerRadius = cell.CornerRadius,
                CornerRadiusTopLeft = cell.CornerRadiusTopLeft,
                CornerRadiusTopRight = cell.CornerRadiusTopRight,
                CornerRadiusBottomRight = cell.CornerRadiusBottomRight,
                CornerRadiusBottomLeft = cell.CornerRadiusBottomLeft,
                BorderColor = cell.BorderColor,
                BorderWidth = cell.BorderWidth,
                BorderStyle = cell.BorderStyle == null ? null : CloneBorderStyle(cell.BorderStyle),
                BorderTop = cell.BorderTop,
                BorderBottom = cell.BorderBottom,
                BorderLeft = cell.BorderLeft,
                BorderRight = cell.BorderRight,
                BorderColorTop = cell.BorderColorTop,
                BorderColorRight = cell.BorderColorRight,
                BorderColorBottom = cell.BorderColorBottom,
                BorderColorLeft = cell.BorderColorLeft,
                BorderWidthTop = cell.BorderWidthTop,
                BorderWidthRight = cell.BorderWidthRight,
                BorderWidthBottom = cell.BorderWidthBottom,
                BorderWidthLeft = cell.BorderWidthLeft,
                BorderStyleTop = cell.BorderStyleTop == null ? null : CloneBorderStyle(cell.BorderStyleTop),
                BorderStyleRight = cell.BorderStyleRight == null ? null : CloneBorderStyle(cell.BorderStyleRight),
                BorderStyleBottom = cell.BorderStyleBottom == null ? null : CloneBorderStyle(cell.BorderStyleBottom),
                BorderStyleLeft = cell.BorderStyleLeft == null ? null : CloneBorderStyle(cell.BorderStyleLeft),
                Padding = cell.Padding,
                PaddingTop = cell.PaddingTop,
                PaddingRight = cell.PaddingRight,
                PaddingBottom = cell.PaddingBottom,
                PaddingLeft = cell.PaddingLeft,
                ColSpan = cell.ColSpan,
                RowSpan = cell.RowSpan,
                ExplicitColumnIndex = cell.ExplicitColumnIndex,
                ContentBuilder = cell.ContentBuilder,
                ContentFactory = cell.ContentFactory,
                ContinuationContent = cell.ContinuationContent,
                PreparedSplitContent = cell.PreparedSplitContent,
                TextRuns = cell.TextRuns?.Select(CloneInlineRun).ToList() ?? new List<TableModels.InlineRun>(),
                MeasuredContent = cell.PreparedSplitContent ? cell.MeasuredContent : null,
                MeasuredContentLayout = cell.PreparedSplitContent ? cell.MeasuredContentLayout : null,
                CachedLayout = cell.HasContainerContent ? null : cell.CachedLayout,
                CachedLayoutWidth = cell.CachedLayoutWidth,
                CachedContentHeight = cell.PreparedSplitContent ? cell.CachedContentHeight : cell.HasContainerContent ? 0f : cell.CachedContentHeight
            };

            return clone;
        }

        private static TableModels.InlineRun CloneInlineRun(TableModels.InlineRun run)
        {
            return new TableModels.InlineRun
            {
                Text = run.Text,
                Style = run.Style != null ? CloneTextStyle(run.Style) : new TableModels.TextStyle(),
                FallbackFonts = run.FallbackFonts?.ToList()
            };
        }

        private static TableModels.BorderStyle? CloneBorderStyle(TableModels.BorderStyle? style)
        {
            if (style == null) return null;
            return new TableModels.BorderStyle
            {
                Color = style.Color,
                Width = style.Width,
                DashPattern = style.DashPattern?.ToArray(),
                DashPhase = style.DashPhase,
                LineJoin = style.LineJoin,
                LineCap = style.LineCap,
                MiterLimit = style.MiterLimit
            };
        }

        private static TableModels.RowBandingSpec? CloneRowBanding(TableModels.RowBandingSpec? spec)
        {
            if (spec == null) return null;
            return new TableModels.RowBandingSpec
            {
                Step = spec.Step,
                Fills = spec.Fills != null ? spec.Fills.Select(CloneBandFill).ToList() : new List<TableModels.BandFill>(),
                BorderOverride = CloneBorderStyle(spec.BorderOverride)
            };
        }

        private static TableModels.ColumnBandingSpec? CloneColumnBanding(TableModels.ColumnBandingSpec? spec)
        {
            if (spec == null) return null;
            return new TableModels.ColumnBandingSpec
            {
                Step = spec.Step,
                Fills = spec.Fills != null ? spec.Fills.Select(CloneBandFill).ToList() : new List<TableModels.BandFill>(),
                BorderOverride = CloneBorderStyle(spec.BorderOverride)
            };
        }

        private static TableModels.BandFill CloneBandFill(TableModels.BandFill fill) =>
            new TableModels.BandFill
            {
                FillColor = fill.FillColor,
                BorderOverride = CloneBorderStyle(fill.BorderOverride)
            };

        private static TableModels.TextStyle CloneTextStyle(TableModels.TextStyle style)
        {
            return new TableModels.TextStyle
            {
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                Bold = style.Bold,
                Italic = style.Italic,
                SmallCaps = style.SmallCaps,
                TextColor = style.TextColor,
                BackgroundColor = style.BackgroundColor,
                HorizontalAlign = style.HorizontalAlign,
                VerticalAlign = style.VerticalAlign,
                LineHeight = style.LineHeight,
                LetterSpacing = style.LetterSpacing,
                WordSpacing = style.WordSpacing,
                Underline = style.Underline,
                Strikethrough = style.Strikethrough,
                Overline = style.Overline,
                DecorationColor = style.DecorationColor,
                DecorationThickness = style.DecorationThickness,
                DecorationStyle = style.DecorationStyle,
                RotationDegrees = style.RotationDegrees,
                Superscript = style.Superscript,
                Subscript = style.Subscript,
                Wrap = style.Wrap,
                FallbackFonts = style.FallbackFonts?.ToList(),
                FlowDirection = style.FlowDirection
                ,
                Direction = style.Direction
            };
        }
    }
}


