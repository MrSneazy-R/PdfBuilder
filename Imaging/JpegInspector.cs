// --- Imaging/JpegInspector.cs (enhanced: Adobe APP14 + ICC profile) ---
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfBuilder.Writer.Imaging
{
    /// <summary>
    /// Parses SOF for W/H/components, detects Adobe APP14 transform (YCCK),
    /// and assembles ICC profile from APP2 segments (if present).
    /// No third-party libs.
    /// </summary>
    public static class JpegInspector
    {
        public sealed class Info
        {
            public int Width;
            public int Height;
            public int Components;          // 1=Gray, 3=RGB/YCbCr, 4=CMYK/YCCK
            public bool HasAdobeMarker;
            public int AdobeTransform = -1; // -1=none, 0=unknown, 1=YCbCr, 2=YCCK
            public byte[]? IccProfile;      // concatenated ICC (if present)
        }

        public static bool LooksLikeJpeg(ReadOnlySpan<byte> data)
            => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

        public static Info GetInfo(byte[] data)
        {
            if (!LooksLikeJpeg(data)) throw new InvalidDataException("Not a JPEG");

            var info = new Info();
            int i = 2;
            // For ICC profiles split across APP2 segments
            var iccParts = new Dictionary<int, byte[]>();
            int iccTotalParts = 0;

            while (i + 3 < data.Length)
            {
                // Seek marker
                if (data[i] != 0xFF) { i++; continue; }
                byte marker = data[i + 1];

                // Standalone markers (no length)
                if (marker == 0xD8 || marker == 0xD9) { i += 2; continue; } // SOI/EOI

                // Start of Scan: header ends; we can stop parsing meta
                if (marker == 0xDA) break;

                if (i + 4 > data.Length) break;
                int len = (data[i + 2] << 8) | data[i + 3];
                if (len < 2 || i + 2 + len > data.Length) break;

                int segStart = i + 4;
                int segLen = len - 2;

                // SOFn (frame header) for baseline/progressive etc.
                if (marker == 0xC0 || marker == 0xC1 || marker == 0xC2)
                {
                    if (segLen >= 7)
                    {
                        int p = segStart;            // precision (ignored; assume 8)
                        p++;                         // precision byte
                        int h = (data[p++] << 8) | data[p++];
                        int w = (data[p++] << 8) | data[p++];
                        int comps = data[p++];

                        info.Width = Math.Max(1, w);
                        info.Height = Math.Max(1, h);
                        info.Components = comps;
                    }
                }
                // APP14 'Adobe' marker (0xEE): detect transform (0,1,2)
                else if (marker == 0xEE && segLen >= 12)
                {
                    // Must start with "Adobe"
                    if (StartsWithAscii(data, segStart, segLen, "Adobe"))
                    {
                        info.HasAdobeMarker = true;
                        // layout: "Adobe" (5), version(2), flags0(2), flags1(2), transform(1)
                        int baseOff = segStart + 5 + 2 + 2 + 2;
                        if (baseOff < segStart + segLen)
                            info.AdobeTransform = data[baseOff];
                    }
                }
                // APP2 ICC_PROFILE (0xE2): "ICC_PROFILE\0" + seq + count + data
                else if (marker == 0xE2 && segLen >= 14)
                {
                    if (StartsWithAscii(data, segStart, segLen, "ICC_PROFILE\0"))
                    {
                        int p = segStart + 12;
                        if (p + 2 <= segStart + segLen)
                        {
                            int seq = data[p++];
                            int count = data[p++];
                            int payloadStart = p;
                            int payloadLen = segStart + segLen - payloadStart;
                            if (seq >= 1 && count >= 1 && payloadLen > 0)
                            {
                                iccTotalParts = Math.Max(iccTotalParts, count);
                                var chunk = new byte[payloadLen];
                                Buffer.BlockCopy(data, payloadStart, chunk, 0, payloadLen);
                                iccParts[seq] = chunk;
                            }
                        }
                    }
                }

                i += 2 + len;
            }

            // Assemble ICC profile if all parts present
            if (iccTotalParts > 0 && iccParts.Count > 0)
            {
                // Concatenate in seq order (best-effort even if parts are missing)
                int totalLen = 0;
                for (int seq = 1; seq <= iccTotalParts; seq++)
                    if (iccParts.TryGetValue(seq, out var part)) totalLen += part.Length;

                if (totalLen > 0)
                {
                    var icc = new byte[totalLen];
                    int pos = 0;
                    for (int seq = 1; seq <= iccTotalParts; seq++)
                    {
                        if (iccParts.TryGetValue(seq, out var part))
                        {
                            Buffer.BlockCopy(part, 0, icc, pos, part.Length);
                            pos += part.Length;
                        }
                    }
                    info.IccProfile = icc;
                }
            }

            // Fallback — if SOF never found, leave 1x1/Components 3 to avoid div by zero later
            if (info.Width == 0 || info.Height == 0)
            {
                info.Width = Math.Max(1, info.Width);
                info.Height = Math.Max(1, info.Height);
                if (info.Components == 0) info.Components = 3;
            }

            return info;
        }

        private static bool StartsWithAscii(byte[] data, int start, int len, string tag)
        {
            if (len < tag.Length) return false;
            for (int i = 0; i < tag.Length; i++)
                if (data[start + i] != (byte)tag[i]) return false;
            return true;
        }
    }
}
