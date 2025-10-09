// --- Imaging/WebpInspector.cs ---
using System;
using System.Buffers.Binary;
using System.IO;

namespace PdfBuilder.Writer.Imaging
{
    public static class WebpInspector
    {
        public sealed class Info { public int Width; public int Height; public bool HasAlpha; public bool Animated; public string PrimaryChunk = ""; }

        public static bool LooksLikeWebp(ReadOnlySpan<byte> s)
        {
            // RIFF....WEBP
            return s.Length >= 12 &&
                   s[0] == (byte)'R' && s[1] == (byte)'I' && s[2] == (byte)'F' && s[3] == (byte)'F' &&
                   s[8] == (byte)'W' && s[9] == (byte)'E' && s[10] == (byte)'B' && s[11] == (byte)'P';
        }

        public static Info GetInfo(byte[] data)
        {
            if (!LooksLikeWebp(data))
                throw new InvalidDataException("Not a WebP (RIFF/WEBP missing).");

            // After "RIFF size WEBP", parse chunks
            int i = 12;
            var info = new Info();

            while (i + 8 <= data.Length)
            {
                uint fourcc = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i)); // actually four bytes literal
                int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i + 4));
                int chunkStart = i + 8;
                if (chunkStart + size > data.Length) break;

                // 'VP8X' extended header (canvas + flags)
                if (fourcc == ToFCC("VP8X") && size >= 10)
                {
                    byte flags = data[chunkStart];
                    info.HasAlpha = (flags & 0x10) != 0;
                    info.Animated = (flags & 0x02) != 0;

                    // width-1 & height-1 are 24-bit little-endian
                    int w = 1 + (data[chunkStart + 4] | (data[chunkStart + 5] << 8) | (data[chunkStart + 6] << 16));
                    int h = 1 + (data[chunkStart + 7] | (data[chunkStart + 8] << 8) | (data[chunkStart + 9] << 16));
                    info.Width = w; info.Height = h;
                }
                else if (fourcc == ToFCC("VP8 ") && size >= 10)
                {
                    // Lossy VP8 bitstream: dimensions are stored inside the VP8 frame header (keyframe).
                    info.PrimaryChunk = "VP8 ";
                }
                else if (fourcc == ToFCC("VP8L") && size >= 5)
                {
                    // Lossless VP8L header (0x2F, then packed WxH etc.). Full details: spec. :contentReference[oaicite:2]{index=2}
                    info.PrimaryChunk = "VP8L";
                    byte sig = data[chunkStart]; // should be 0x2F
                    if (sig == 0x2F)
                    {
                        uint wlh = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(chunkStart + 1));
                        int w = 1 + (int)(wlh & 0x3FFF);
                        int h = 1 + (int)((wlh >> 14) & 0x3FFF);
                        bool alpha = ((wlh >> 28) & 1) != 0;
                        info.Width = w; info.Height = h; info.HasAlpha |= alpha;
                    }
                }

                // advance; chunks are padded to even
                i = chunkStart + size + (size & 1);
                // stop early if we already have dims and a primary chunk
                if (info.Width > 0 && info.Height > 0 && info.PrimaryChunk.Length > 0) break;
            }

            if (info.Width <= 0 || info.Height <= 0)
                throw new InvalidDataException("WebP size not found.");
            return info;
        }

        private static uint ToFCC(string s) =>
            (uint)(byte)s[0] | ((uint)(byte)s[1] << 8) | ((uint)(byte)s[2] << 16) | ((uint)(byte)s[3] << 24);
    }
}
