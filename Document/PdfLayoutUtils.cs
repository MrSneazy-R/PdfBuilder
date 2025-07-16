using PdfBuilder.Elements;
using System.Collections.Generic;
using System.Text;

namespace PdfBuilder.Document
{
    public static class PdfLayoutUtils
    {
        public static List<string> WrapText(string text, float maxWidth, float fontSize)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string> { "" };

            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = new StringBuilder();
            float maxChars = maxWidth / (fontSize * 0.5f);

            foreach (var word in words)
            {
                if (currentLine.Length + word.Length + 1 > maxChars)
                {
                    lines.Add(currentLine.ToString().Trim());
                    currentLine.Clear();
                }
                currentLine.Append(word + " ");
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString().Trim());

            return lines;
        }
      
            // Full width table for ASCII chars (0-127) for Helvetica Regular (units = 1/1000 text space)
            private static readonly int[] HelveticaWidths = new int[128]
            {
                // 0-31: control chars (all 0)
                0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
                // 32-47: space !"#$%&'()*+,-./
                278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
                // 48-63: 0-9:;<=>?
                556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
                // 64-95: @A-Z[\]^_
                1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
                667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
                // 96-127: `a-z{|}~ DEL
                278,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,
                611,611,389,556,333,611,556,778,556,556,500,389,280,389,584,0
            };

            public static float EstimateTextWidth(string text, string fontFamily, float fontSize, bool monospace = false, bool bold = false)
            {
                if (string.IsNullOrEmpty(text))
                    return 0f;

                // For now: Helvetica only. You can extend this with fontFamily + bold lookup if you want.
                float width = 0f;
                foreach (char c in text)
                {
                    int w = (c < 128) ? HelveticaWidths[c] : 500; // fallback for non-ASCII
                    width += w;
                }
                return (width * fontSize) / 1000f;
            }
        

        //private static readonly Dictionary<string, (float CharMult, float SpaceMult, float BoldCharMult, float BoldSpaceMult)> FontWidthFactors =
        //new()
        //{
        //    // Sans-serif (Helvetica, Arial, Verdana)
        //    { "helvetica",          (0.58f, 0.28f, 0.62f, 0.32f) },
        //    { "helvetica-bold",     (0.62f, 0.32f, 0.62f, 0.32f) }, // Explicit for bold
        //    { "arial",              (0.57f, 0.27f, 0.61f, 0.31f) },
        //    { "arial-bold",         (0.61f, 0.31f, 0.61f, 0.31f) },
        //    { "verdana",            (0.61f, 0.30f, 0.66f, 0.34f) },
        //    { "verdana-bold",       (0.66f, 0.34f, 0.66f, 0.34f) },

        //    // Serif (Times, Georgia)
        //    { "times",              (0.60f, 0.26f, 0.64f, 0.30f) },
        //    { "times-roman",        (0.60f, 0.26f, 0.64f, 0.30f) }, // PDF Base
        //    { "times-bold",         (0.64f, 0.30f, 0.64f, 0.30f) },
        //    { "georgia",            (0.62f, 0.29f, 0.67f, 0.33f) },
        //    { "georgia-bold",       (0.67f, 0.33f, 0.67f, 0.33f) },

        //    // Monospace
        //    { "courier",            (0.60f, 0.60f, 0.63f, 0.63f) },
        //    { "courier-new",        (0.61f, 0.61f, 0.64f, 0.64f) },
        //    { "consolas",           (0.59f, 0.59f, 0.62f, 0.62f) },
        //    { "monaco",             (0.60f, 0.60f, 0.62f, 0.62f) },

        //    // Other web-safe fonts
        //    { "calibri",            (0.57f, 0.26f, 0.61f, 0.30f) },
        //    { "calibri-bold",       (0.61f, 0.30f, 0.61f, 0.30f) },
        //    { "tahoma",             (0.60f, 0.28f, 0.64f, 0.32f) },
        //    { "tahoma-bold",        (0.64f, 0.32f, 0.64f, 0.32f) },
        //    { "trebuchet",          (0.59f, 0.27f, 0.64f, 0.31f) },
        //    { "trebuchet-bold",     (0.64f, 0.31f, 0.64f, 0.31f) },
        //    { "impact",             (0.68f, 0.34f, 0.72f, 0.38f) },
        //};

       

    }
    public static class ImageHeaderParser
    {
        public static (int width, int height) GetDimensions(byte[] imageData)
        {
            if (imageData.Length < 10)
                throw new InvalidDataException("Image data too small");

            // PNG
            if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E &&
                imageData[3] == 0x47 && imageData[4] == 0x0D && imageData[5] == 0x0A &&
                imageData[6] == 0x1A && imageData[7] == 0x0A)
            {
                // IHDR chunk: width = bytes 16-19, height = 20-23
                int width = ReadInt32BigEndian(imageData, 16);
                int height = ReadInt32BigEndian(imageData, 20);
                return (width, height);
            }
            // JPEG
            if (imageData[0] == 0xFF && imageData[1] == 0xD8)
            {
                return ParseJpegDimensions(imageData);
            }
            // GIF
            if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46)
            {
                int width = imageData[6] | (imageData[7] << 8);
                int height = imageData[8] | (imageData[9] << 8);
                return (width, height);
            }
            // BMP
            if (imageData[0] == 0x42 && imageData[1] == 0x4D)
            {
                // DIB header: offset 18 (width), 22 (height), both 4 bytes little endian
                int width = BitConverter.ToInt32(imageData, 18);
                int height = BitConverter.ToInt32(imageData, 22);
                return (Math.Abs(width), Math.Abs(height)); // May be negative for top-down BMP
            }
            // TIFF (little/big endian)
            if ((imageData[0] == 0x49 && imageData[1] == 0x49 && imageData[2] == 0x2A && imageData[3] == 0x00) ||
                (imageData[0] == 0x4D && imageData[1] == 0x4D && imageData[2] == 0x00 && imageData[3] == 0x2A))
            {
                return ParseTiffDimensions(imageData);
            }

            throw new NotSupportedException("Unknown or unsupported image format.");
        }

        private static int ReadInt32BigEndian(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | (data[offset + 3]);
        }

        // JPEG SOF marker parsing
        private static (int width, int height) ParseJpegDimensions(byte[] data)
        {
            int pos = 2;
            while (pos < data.Length - 9)
            {
                if (data[pos] != 0xFF)
                {
                    pos++;
                    continue;
                }

                // SOF0 (0xC0), SOF2 (0xC2) -- only those are baseline/progressive
                byte marker = data[pos + 1];
                if ((marker >= 0xC0 && marker <= 0xC3) || (marker >= 0xC5 && marker <= 0xC7) ||
                    (marker >= 0xC9 && marker <= 0xCB) || (marker >= 0xCD && marker <= 0xCF))
                {
                    int blockLen = (data[pos + 2] << 8) | data[pos + 3];
                    int height = (data[pos + 5] << 8) | data[pos + 6];
                    int width = (data[pos + 7] << 8) | data[pos + 8];
                    return (width, height);
                }
                else
                {
                    int blockLen = (data[pos + 2] << 8) | data[pos + 3];
                    pos += 2 + blockLen;
                }
            }
            throw new InvalidDataException("JPEG SOF marker not found");
        }

        // TIFF parsing: searches for width/height tags in the first IFD
        private static (int width, int height) ParseTiffDimensions(byte[] data)
        {
            bool littleEndian = data[0] == 0x49;
            int ifdOffset = littleEndian
                ? BitConverter.ToInt32(data, 4)
                : (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];

            int entries = littleEndian
                ? BitConverter.ToUInt16(data, ifdOffset)
                : (data[ifdOffset] << 8) | data[ifdOffset + 1];

            int width = 0, height = 0;

            for (int i = 0; i < entries; i++)
            {
                int entryOffset = ifdOffset + 2 + i * 12;
                int tag = littleEndian
                    ? BitConverter.ToUInt16(data, entryOffset)
                    : (data[entryOffset] << 8) | data[entryOffset + 1];

                if (tag == 256) // ImageWidth
                {
                    width = littleEndian
                        ? BitConverter.ToInt32(data, entryOffset + 8)
                        : (data[entryOffset + 8] << 24) | (data[entryOffset + 9] << 16) | (data[entryOffset + 10] << 8) | data[entryOffset + 11];
                }
                else if (tag == 257) // ImageLength
                {
                    height = littleEndian
                        ? BitConverter.ToInt32(data, entryOffset + 8)
                        : (data[entryOffset + 8] << 24) | (data[entryOffset + 9] << 16) | (data[entryOffset + 10] << 8) | data[entryOffset + 11];
                }
                if (width > 0 && height > 0)
                    return (width, height);
            }
            throw new InvalidDataException("TIFF width/height tag not found");
        }

        public static bool IsJpeg(byte[] data) =>
             data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[data.Length - 2] == 0xFF && data[data.Length - 1] == 0xD9;

        public static bool IsPng(byte[] data) =>
            data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;

    }
}
