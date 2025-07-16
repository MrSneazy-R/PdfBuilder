using PdfBuilder.Elements;
using PdfBuilder.Models;

namespace PdfBuilder.Document
{
    public class ColumnBuilder
    {
        private readonly PdfPage _page;
        private float _currentY;
        private readonly float _x;
        private readonly float _width;
        private float _defaultSpacing;

        public ColumnBuilder(PdfPage page, float margin, float defaultSpacing = 8)
        {
            _page = page;
            _currentY = page.Height - margin;
            _x = margin;
            _width = page.Width - margin * 2;
            _defaultSpacing = defaultSpacing;
        }

        public float GetCurrentY() => _currentY;

        public ColumnBuilder Underline(float x, float? y, float width)
        {
            float useY = y ?? _currentY;
            _page.Elements.Add(new UnderlineElement(x, useY)
            {
                Width = width,
                Thickness = 1,
                Color = "#000000"
            });
            return this;
        }

        // ---- Fluent API entrypoint ----
        public TextBuilder Text(string content)
        {
            return new TextBuilder(this, content, _x, _currentY, _width);
        }
        public ImageBuilder Image(byte[] imageData, float x, float y, float width, float height)
        {
            return new ImageBuilder(this, imageData, x, y, width, height);
        }
        public TableBuilder Table(float x, float y, float width, float height)
        {
            return new TableBuilder(this, x, y, width, height);
        }
        // Called by TextBuilder.Add()
        public void AddText(TextElement text)
        {
            // Margins (top/bottom/left/right, default to 0 if null except top)
            float marginTop    = text.MarginTop    ?? _defaultSpacing;
            float marginBottom = text.MarginBottom ?? 0;
            float marginLeft   = text.MarginLeft   ?? 0;
            float marginRight  = text.MarginRight  ?? 0;

            // Padding (top/bottom/left/right, default to 0 if null)
            float paddingTop    = text.PaddingTop    ?? 0;
            float paddingBottom = text.PaddingBottom ?? 0;
            float paddingLeft   = text.PaddingLeft   ?? 0;
            float paddingRight  = text.PaddingRight  ?? 0;

            // Step 1: Apply top margin
            _currentY -= marginTop;

            // Step 2: Calculate maxWidth for this text block (width minus left/right margin/padding)
            float availableWidth = _width - marginLeft - marginRight;
            float textMaxWidth = (text.MaxWidth ?? availableWidth) - paddingLeft - paddingRight;
            if (textMaxWidth < 0) textMaxWidth = 0;

            // Step 3: Wrap text
            var lines = PdfLayoutUtils.WrapText(text.Text, textMaxWidth, text.FontSize);
            int lineCount = lines.Count;

            // Step 4: Calculate inner text box size
            float lineHeight = text.FontSize * text.LineHeight;
            float innerHeight = lineHeight * lineCount;

            // Step 5: Add padding
            float fullHeight = innerHeight + paddingTop + paddingBottom;

            // Step 6: Decorations (underline, strikethrough, overline may increase height)
            // TODO: tweak if you want underline to extend the box further down

            // Step 7: Calculate rotated bounding box if rotated
            float verticalSpan;
            float fullWidth = (lines.Any() ? lines.Max(line => line.Length * text.FontSize * 0.5f) : 0f) + paddingLeft + paddingRight;
            if (text.Rotation != 0f)
            {
                double theta = text.Rotation * Math.PI / 180.0;
                verticalSpan = (float)(Math.Abs(fullHeight * Math.Cos(theta)) + Math.Abs(fullWidth * Math.Sin(theta)));
            }
            else
            {
                verticalSpan = fullHeight;
            }

            // Step 8: Set final X and Y for the text block (include margins/paddings)
            text.X = _x + marginLeft + paddingLeft;
            text.Y = _currentY;

            // Step 9: Store settings back to the element for rendering
            text.MaxWidth = textMaxWidth;
            text.PaddingTop = paddingTop;
            text.PaddingBottom = paddingBottom;
            text.PaddingLeft = paddingLeft;
            text.PaddingRight = paddingRight;

            _page.AddElement(text);

            // Step 10: Step down for the next block (add bottom margin)
            _currentY -= verticalSpan + marginBottom;
        }
        // Add this to ColumnBuilder
        public void AddImage(ImageElement image)
        {
            // Margins
            float marginTop = image.MarginTop ?? _defaultSpacing;
            float marginBottom = image.MarginBottom ?? 0;
            float marginLeft = image.MarginLeft ?? 0;
            float marginRight = image.MarginRight ?? 0;

            // Padding
            float paddingTop = image.PaddingTop ?? 0;
            float paddingBottom = image.PaddingBottom ?? 0;
            float paddingLeft = image.PaddingLeft ?? 0;
            float paddingRight = image.PaddingRight ?? 0;

            // Step 1: Apply top margin
            _currentY -= marginTop;

            // Step 2: Determine available width for the image block (just like text)
            float availableWidth = _width - marginLeft - marginRight;
            float imageWidth = image.Width;
            float imageHeight = image.Height;

            // Scale if MaxWidth/MaxHeight is set
            if (image.MaxWidth.HasValue && imageWidth > image.MaxWidth.Value)
            {
                float scale = image.MaxWidth.Value / imageWidth;
                imageWidth = image.MaxWidth.Value;
                imageHeight *= scale;
            }
            if (image.MaxHeight.HasValue && imageHeight > image.MaxHeight.Value)
            {
                float scale = image.MaxHeight.Value / imageHeight;
                imageHeight = image.MaxHeight.Value;
                imageWidth *= scale;
            }

            // Step 3: Calculate bounding box (with padding)
            float blockWidth = imageWidth + paddingLeft + paddingRight;
            float blockHeight = imageHeight + paddingTop + paddingBottom;

            // Step 4: Rotation logic (for vertical span, like text)
            float verticalSpan;
            if (image.Rotation != 0f)
            {
                double theta = image.Rotation * Math.PI / 180.0;
                verticalSpan = (float)(Math.Abs(blockHeight * Math.Cos(theta)) + Math.Abs(blockWidth * Math.Sin(theta)));
            }
            else
            {
                verticalSpan = blockHeight;
            }

            // Step 5: Set X and Y for the image (inside block, left align)
            image.X = _x + marginLeft + paddingLeft;
            image.Y = _currentY;

            // Store adjusted width/height for rendering
            image.Width = imageWidth;
            image.Height = imageHeight;
            image.PaddingTop = paddingTop;
            image.PaddingBottom = paddingBottom;
            image.PaddingLeft = paddingLeft;
            image.PaddingRight = paddingRight;

            // Step 6: Add to the page
            _page.AddElement(image);

            // Step 7: Step down for the next block (add bottom margin)
            _currentY -= verticalSpan + marginBottom;
        }
        public void AddTable(TableElement table)
        {
            // Margins
            float marginTop = table.MarginTop ?? _defaultSpacing;
            float marginBottom = table.MarginBottom ?? 0;
            float marginLeft = table.MarginLeft ?? 0;
            float marginRight = table.MarginRight ?? 0;

            // Padding
            float paddingTop = table.PaddingTop ?? 0;
            float paddingBottom = table.PaddingBottom ?? 0;
            float paddingLeft = table.PaddingLeft ?? 0;
            float paddingRight = table.PaddingRight ?? 0;

            // Step 1: Apply top margin
            _currentY -= marginTop;

            // Step 2: Set X and Y
            table.X = _x + marginLeft + paddingLeft;
            table.Y = _currentY;

            // Table width
            table.Width = table.Width > 0 ? table.Width : _width - marginLeft - marginRight - paddingLeft - paddingRight;

            // Step 3: (Optional) estimate height based on row count, font size, and paddings
            // For now, we set the Y and add it to the page.
            _page.AddElement(table);

            // Step 4: Step down for the next block (approximate, refine after rendering logic)
            float tableHeight = table.Height > 0 ? table.Height : 40 + (table.Rows.Count * 24); // crude estimate
            _currentY -= tableHeight + marginBottom;
        }
    }
}
