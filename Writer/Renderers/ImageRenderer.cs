// --- ImageRenderer.cs ---
using PdfBuilder.Elements;
using System;
using System.Globalization;
using System.Text;

namespace PdfBuilder.Writer
{
    public static class ImageRenderer
    {
        private static readonly IFormatProvider Inv = CultureInfo.InvariantCulture;
        private static string N(double v) => v.ToString("0.###", Inv);

        /// <summary>
        /// Append drawing commands for a single ImageElement.
        /// imageObjId is the XObject object id (we reference it by name /Im{imageObjId}).
        /// If opacity < 1, extGStateResourceName should be the ExtGState resource (e.g., /GS7).
        /// </summary>
        public static void Append(
     StringBuilder sb,
     ImageElement img,
     float pageHeight,
     int imageObjId,
     string? extGStateResourceName = null)
        {
            if (img == null) return;

            float w = Math.Max(0.1f, img.Width);
            float h = Math.Max(0.1f, img.Height);

            // Top-left Y → center (cx, cy)
            float padT = img.PaddingTop ?? 0f;
            float topY = img.Y - padT;
            float cx = img.X + w / 2f;
            float cy = topY - h / 2f;

            // ---- Optional SHADOW (solid offset; not affected by image opacity) ----
            if (!string.IsNullOrWhiteSpace(img.ShadowColor) &&
                (((img.ShadowOffsetX ?? 0) != 0) || ((img.ShadowOffsetY ?? 0) != 0)))
            {
                float sox = img.ShadowOffsetX ?? 0f;
                float soy = img.ShadowOffsetY ?? 0f;
                string sRGB = TryRgb(img.ShadowColor) ?? "0 0 0";

                sb.Append("q ");
                // place at image center, rotate, then move to image local origin and apply offset
                AppendTransform(sb, cx, cy, img.Rotation);
                sb.Append($"1 0 0 1 {N(-w / 2 + sox)} {N(-h / 2 - soy)} cm ");

                sb.Append($"{sRGB} rg ");
                AppendShadowPath(sb, img, w, h);   // draws a path for the shadow shape
                sb.Append("f Q\n");                 // fill it and restore
            }

            // ---- Main block: clip, image ----
            sb.Append("q ");
            // center & rotate
            AppendTransform(sb, cx, cy, img.Rotation);
            // translate to image's local origin (bottom-left)
            sb.Append($"1 0 0 1 {N(-w / 2)} {N(-h / 2)} cm ");

            // --- Always clip to the image box for clean edges ---
            float rClip = Math.Max(0, img.CornerRadius ?? 0);
            if (img.ClipShape == ImageClipShape.Circle)
            {
                float r0 = Math.Min(w, h) / 2f;
                AppendEllipsePath(sb, w / 2f, h / 2f, r0, r0);
                sb.Append("W n ");
            }
            else if (img.ClipShape == ImageClipShape.Ellipse)
            {
                var (rx, ry) = GetEllipseRadii(img, w, h);
                AppendEllipsePath(sb, w / 2f, h / 2f, rx, ry);
                sb.Append("W n ");
            }
            else
            {
                AppendRoundedRectPath(sb, 0, 0, w, h, rClip);
                sb.Append("W n ");
            }

            // Draw image exactly within the clip (no bleed). A prior 0.5pt bleed
            // could create edge halos with alpha PNGs due to resampling beyond
            // image bounds in some PDF viewers.
            const float bleed = 0f;
            if (!string.IsNullOrEmpty(extGStateResourceName))
                sb.Append($"{extGStateResourceName} gs ");
            sb.Append($"{N(w + 2 * bleed)} 0 0 {N(h + 2 * bleed)} {N(-bleed)} {N(-bleed)} cm ");
            sb.Append($"/Im{imageObjId} Do ");
            sb.Append("Q\n"); // <-- pop the clip + transform

            // --- Border drawn after clip so full stroke shows ---
            if (!string.IsNullOrWhiteSpace(img.BorderColor) && (img.BorderWidth ?? 0f) > 0f)
            {
                string strokeRGB = TryRgb(img.BorderColor) ?? "0 0 0";

                sb.Append("q ");
                AppendTransform(sb, cx, cy, img.Rotation);
                sb.Append($"1 0 0 1 {N(-w / 2)} {N(-h / 2)} cm ");

                // round joins/caps look nicer on rounded corners
                sb.Append($"{strokeRGB} RG {N(img.BorderWidth ?? 1)} w 1 j 1 J ");
                AppendBorderPath(sb, img, w, h, Math.Max(0, img.CornerRadius ?? 0));
                sb.Append("S Q\n");
            }
        }




        private static void AppendTransform(StringBuilder sb, float cx, float cy, float rotationDeg)
        {
            // Translate to center
            sb.Append($"1 0 0 1 {N(cx)} {N(cy)} cm ");
            if (Math.Abs(rotationDeg) > 0.01f)
            {
                double rad = rotationDeg * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);
                // Rotate around origin (which is at the image center after translate)
                sb.Append($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} 0 0 cm ");
            }
        }

        private static bool AppendClipPath(StringBuilder sb, ImageElement img, float w, float h)
        {
            float r = Math.Max(0, img.CornerRadius ?? 0);

            switch (img.ClipShape)
            {
                case ImageClipShape.Circle:
                {
                    float rx = Math.Min(w, h) / 2f;
                    AppendEllipsePath(sb, w / 2f, h / 2f, rx, rx);
                    return true;
                }
                case ImageClipShape.Ellipse:
                {
                    var (rx, ry) = GetEllipseRadii(img, w, h);
                    AppendEllipsePath(sb, w / 2f, h / 2f, rx, ry);
                    return true;
                }
                case ImageClipShape.RoundedRect:
                {
                    AppendRoundedRectPath(sb, 0, 0, w, h, r);
                    return true;
                }
                default:
                    // If no explicit clip shape but a corner radius is provided, clip as rounded rect.
                    if (r > 0.01f)
                    {
                        AppendRoundedRectPath(sb, 0, 0, w, h, r);
                        return true;
                    }
                    return false;
            }
        }


        private static void AppendBorderPath(StringBuilder sb, ImageElement img, float w, float h, float r)
        {
            if (img.ClipShape == ImageClipShape.Circle)
            {
                float r0 = Math.Min(w, h) / 2f;
                AppendEllipsePath(sb, w / 2f, h / 2f, r0, r0);
            }
            else if (img.ClipShape == ImageClipShape.Ellipse)
            {
                var (rxB, ryB) = GetEllipseRadii(img, w, h);
                AppendEllipsePath(sb, w / 2f, h / 2f, rxB, ryB);
            }
            else
            {
                AppendRoundedRectPath(sb, 0, 0, w, h, Math.Max(0, img.CornerRadius ?? 0));
            }

        }

        // Rounded rectangle path at (x,y) bottom-left
        private static void AppendRoundedRectPath(StringBuilder sb, float x, float y, float w, float h, float r)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2f));
            if (r <= 0.01f)
            {
                sb.Append($"{N(x)} {N(y)} {N(w)} {N(h)} re ");
                return;
            }

            // kappa for quarter circle via cubic Béziers
            float c = r * 0.5522847498f;

            float x0 = x, y0 = y;
            float x1 = x + r, y1 = y;
            float x2 = x + w - r, y2 = y;
            float x3 = x + w, y3 = y + r;
            float x4 = x + w, y4 = y + h - r;
            float x5 = x + w - r, y5 = y + h;
            float x6 = x + r, y6 = y + h;
            float x7 = x, y7 = y + h - r;

            sb.Append($"{N(x1)} {N(y1)} m ");
            sb.Append($"{N(x2)} {N(y2)} l ");
            sb.Append($"{N(x2 + c)} {N(y2)} {N(x3)} {N(y3 - c)} {N(x3)} {N(y3)} c ");
            sb.Append($"{N(x4)} {N(y4)} l ");
            sb.Append($"{N(x4)} {N(y4 + c)} {N(x5 + c)} {N(y5)} {N(x5)} {N(y5)} c ");
            sb.Append($"{N(x6)} {N(y6)} l ");
            sb.Append($"{N(x6 - c)} {N(y6)} {N(x0)} {N(y7 + c)} {N(x0)} {N(y7)} c ");
            sb.Append($"{N(x0)} {N(y1 + r)} l ");
            sb.Append($"{N(x0)} {N(y1 + c)} {N(x1 - c)} {N(y1)} {N(x1)} {N(y1)} c ");
            sb.Append("h ");
        }

        // Ellipse via 4 cubic Bézier arcs
        private static void AppendEllipsePath(StringBuilder sb, float cx, float cy, float rx, float ry)
        {
            // Magic kappa for circle; scales fine per axis
            const float K = 0.5522847498f;
            float cX = rx * K;
            float cY = ry * K;

            // Start at (cx+rx, cy)
            sb.Append($"{N(cx + rx)} {N(cy)} m ");

            // Quadrants: right->top, top->left, left->bottom, bottom->right
            sb.Append($"{N(cx + rx)} {N(cy + cY)} {N(cx + cX)} {N(cy + ry)} {N(cx)} {N(cy + ry)} c ");
            sb.Append($"{N(cx - cX)} {N(cy + ry)} {N(cx - rx)} {N(cy + cY)} {N(cx - rx)} {N(cy)} c ");
            sb.Append($"{N(cx - rx)} {N(cy - cY)} {N(cx - cX)} {N(cy - ry)} {N(cx)} {N(cy - ry)} c ");
            sb.Append($"{N(cx + cX)} {N(cy - ry)} {N(cx + rx)} {N(cy - cY)} {N(cx + rx)} {N(cy)} c ");
            sb.Append("h ");
        }

        private static string? TryRgb(string? color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;
            if (color.Equals("black", StringComparison.OrdinalIgnoreCase)) return null;
            if (color.StartsWith("#") && color.Length == 7 &&
                int.TryParse(color.Substring(1, 2), NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(color.Substring(3, 2), NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(color.Substring(5, 2), NumberStyles.HexNumber, null, out var b))
            {
                return $"{(r / 255.0).ToString("0.###", Inv)} {(g / 255.0).ToString("0.###", Inv)} {(b / 255.0).ToString("0.###", Inv)}";
            }
            return null;
        }

        private static void AppendShadowPath(StringBuilder sb, ImageElement img, float w, float h)
        {
            float r = Math.Max(0, img.CornerRadius ?? 0);

            switch (img.ClipShape)
            {
                case ImageClipShape.Circle:
                {
                    float r0 = Math.Min(w, h) / 2f;
                    AppendEllipsePath(sb, w / 2f, h / 2f, r0, r0);
                    return;
                }
                case ImageClipShape.Ellipse:
                {
                    var (rxS, ryS) = GetEllipseRadii(img, w, h);
                    AppendEllipsePath(sb, w / 2f, h / 2f, rxS, ryS);
                    return;
                }
                case ImageClipShape.RoundedRect:
                {
                    AppendRoundedRectPath(sb, 0, 0, w, h, r);
                    return;
                }
                default:
                    // For shadows, if no explicit clip, we still draw a rect (rounded if CornerRadius > 0)
                    AppendRoundedRectPath(sb, 0, 0, w, h, r);
                    return;
            }
        }

        private static (float rx, float ry) GetEllipseRadii(ImageElement img, float w, float h)
        {
            // Start with the inscribed ellipse
            float rx = w / 2f;
            float ry = h / 2f;

            // Apply orientation by shrinking the *minor* axis using EllipseSquash (<= 1)
            float squash = img.EllipseSquash <= 0 ? 1f : img.EllipseSquash;

            if (img.EllipseOrientation == EllipseOrientation.Vertical)
                rx *= squash;  // narrower horizontally → “vertical” feel
            else
                ry *= squash;  // flatter vertically → “horizontal” feel

            // Keep it inside the image box just in case
            rx = Math.Min(rx, w / 2f);
            ry = Math.Min(ry, h / 2f);
            return (rx, ry);
        }

    }
}
