using System.Collections.Generic;

namespace PdfBuilder.Writer
{
    public class FontManager
    {
        private readonly Dictionary<string, int> _fontMap = new();
        private int _nextObjectId;

        public FontManager(int startingObjectId)
        {
            _nextObjectId = startingObjectId;
        }

        public Dictionary<string, int> FontMap => _fontMap;

        public int RegisterFont(string fontKey)
        {
            if (_fontMap.ContainsKey(fontKey))
                return _fontMap[fontKey];

            int assignedId = _nextObjectId++;
            _fontMap[fontKey] = assignedId;
            return assignedId;
        }

       public string ResolveBaseFontName(string fontKey)
        {
            return fontKey switch
            {
                "Helvetica" => "/Helvetica",
                "Helvetica-Bold" => "/Helvetica-Bold",
                "Helvetica-Italic" => "/Helvetica-Oblique",
                "Helvetica-BoldItalic" => "/Helvetica-BoldOblique",
                "Times" or "Times-Roman" => "/Times-Roman",
                "Times-Bold" => "/Times-Bold",
                "Times-Italic" => "/Times-Italic",
                "Courier" => "/Courier",
                _ => "/Helvetica"
            };
        }
        public static string NormalizeFontKey(string? fontFamily, bool bold = false, bool italic = false)
        {
            if (string.IsNullOrWhiteSpace(fontFamily)) fontFamily = "Helvetica";
            string key = fontFamily.Trim().Replace(" ", "-");

            // Remove duplicate Bold/Italic/Oblique
            key = key.Replace("-BoldItalic", "").Replace("-Bold", "").Replace("-Italic", "").Replace("-Oblique", "");

            // Build up correct key
            if (bold && italic)
            {
                // For Helvetica and Times, PDF uses "-BoldOblique" or "-BoldItalic"
                if (key.Equals("Helvetica", StringComparison.OrdinalIgnoreCase))
                    key = "Helvetica-BoldOblique";
                else if (key.Equals("Times", StringComparison.OrdinalIgnoreCase) || key.Equals("Times-Roman", StringComparison.OrdinalIgnoreCase))
                    key = "Times-BoldItalic";
                else
                    key += "-BoldItalic";
            }
            else if (bold)
            {
                key += "-Bold";
            }
            else if (italic)
            {
                // Helvetica and Courier use Oblique, Times uses Italic
                if (key.Equals("Helvetica", StringComparison.OrdinalIgnoreCase))
                    key = "Helvetica-Oblique";
                else if (key.StartsWith("Times", StringComparison.OrdinalIgnoreCase))
                    key += "-Italic";
                else
                    key += "-Oblique";
            }

            return key;
        }




    }
}
