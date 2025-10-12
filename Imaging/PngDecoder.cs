// --- Imaging/PngDecoder.cs (FULL PNG: 1/2/4/8/16-bit, CT 0/2/3/4/6, Adam7) ---
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PdfBuilder.Writer.Imaging
{
    public static class PngDecoder
    {
        private static readonly byte[] Sig = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

        public sealed class Result
        {
            public int Width;
            public int Height;

            // Output is normalized to 8-bit per component for PDF 1.4 compatibility.
            public int BitsPerComponent = 8;

            // 1 for Gray/Gray+Alpha, 3 for RGB/RGBA, 1 for Indexed (indices).
            public int Components;

            // For Gray/RGB: interleaved 8-bit pixels: G or RGB.
            // For Indexed: one byte per pixel = palette index (0..n-1).
            public byte[] Pixels = Array.Empty<byte>();

            // Optional 8-bit alpha plane (per pixel).
            public byte[]? Alpha;

            // For Indexed images: RGB palette (3*n).
            public byte[]? PaletteRGB;

            // True if color type = 3 (palette).
            public bool IsIndexed;

            // True when Pixels still contain the per-row PNG filter byte (and thus require Predictor 15).
            public bool PixelsContainFilterBytes;

            // True when Alpha still contains per-row PNG filter bytes.
            public bool AlphaContainsFilterBytes;

            // Convenience alias for the number of interleaved color components in Pixels.
            public int ColorComponents => Components;
        }

        public static bool LooksLikePng(ReadOnlySpan<byte> data)
            => data.Length >= 8 && data.Slice(0, 8).SequenceEqual(Sig);

        // ---- Decode entry ----
        public static Result Decode(byte[] data)
        {
            if (!LooksLikePng(data)) throw new InvalidDataException("Not a PNG signature");

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            ms.Position = 8; // skip signature

            int width = 0, height = 0;
            int bitDepth = 0, colorType = 0, compression = 0, filter = 0, interlace = 0;

            byte[]? plte = null;  // RGB triples
            byte[]? trns = null;  // transparency chunk
            var idat = new MemoryStream();

            // Read chunks
            while (ms.Position + 8 <= ms.Length)
            {
                int len = ReadInt32BE(br);
                string type = ReadAscii(br, 4);

                if (len < 0 || ms.Position + len + 4 > ms.Length)
                    throw new InvalidDataException("Corrupt PNG chunk length");

                switch (type)
                {
                    case "IHDR":
                        width = ReadInt32BE(br);
                        height = ReadInt32BE(br);
                        bitDepth = br.ReadByte();
                        colorType = br.ReadByte();
                        compression = br.ReadByte();
                        filter = br.ReadByte();
                        interlace = br.ReadByte();

                        if (compression != 0 || filter != 0)
                            throw new NotSupportedException("PNG: unsupported compression/filter method");

                        ValidateIHDR(width, height, bitDepth, colorType, interlace);
                        break;

                    case "PLTE":
                        plte = br.ReadBytes(len);
                        break;

                    case "tRNS":
                        trns = br.ReadBytes(len);
                        break;

                    case "IDAT":
                        var chunk = br.ReadBytes(len);
                        idat.Write(chunk, 0, chunk.Length);
                        break;

                    case "IEND":
                        br.ReadBytes(len); // should be zero
                        break;

                    default:
                        br.ReadBytes(len); // skip ancillary chunk data
                        break;
                }

                br.ReadUInt32(); // CRC (ignored)
                if (type == "IEND") break;
            }

            // Inflate combined IDAT. PNG uses a zlib wrapper, so use ZLibStream here.
            idat.Position = 0;
            byte[] decompressed;
            using (var z = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
            using (var outMs = new MemoryStream())
            {
                z.CopyTo(outMs);
                decompressed = outMs.ToArray();
            }

            // Decide how many samples per pixel before expansion
            var spp = SamplesPerPixel(colorType);

            var res = new Result
            {
                Width = width,
                Height = height
            };

            if (interlace == 0)
            {
                DecodeNonInterlaced(res, decompressed, width, height, bitDepth, colorType, spp, plte, trns);
            }
            else
            {
                DecodeAdam7(res, decompressed, width, height, bitDepth, colorType, spp, plte, trns);
            }

            return res;
        }

        // -------------------------- Non-Interlaced --------------------------
        private static void DecodeNonInterlaced(
            Result res, byte[] src, int width, int height, int bitDepth, int ct, int spp,
            byte[]? plte, byte[]? trns)
        {
            int bppBytes = BytesPerPixelForFilter(spp, bitDepth);
            int rowBytesPacked = RowBytesPacked(width, spp, bitDepth);
            int expected = (rowBytesPacked + 1) * height;
            if (src.Length != expected)
                throw new InvalidDataException("PNG: unexpected decompressed size (non-interlaced)");

            // Unfilter to packed rows
            var unfiltered = new byte[height * rowBytesPacked];
            for (int y = 0; y < height; y++)
            {
                int f = src[y * (rowBytesPacked + 1)];
                int sRow = y * (rowBytesPacked + 1) + 1;
                int dRow = y * rowBytesPacked;

                switch (f)
                {
                    case 0: // None
                        Buffer.BlockCopy(src, sRow, unfiltered, dRow, rowBytesPacked);
                        break;
                    case 1: // Sub
                        UnfilterSub(src, unfiltered, sRow, dRow, rowBytesPacked, bppBytes);
                        break;
                    case 2: // Up
                        UnfilterUp(src, unfiltered, sRow, dRow, rowBytesPacked, y, rowBytesPacked);
                        break;
                    case 3: // Average
                        UnfilterAverage(src, unfiltered, sRow, dRow, rowBytesPacked, bppBytes, y, rowBytesPacked);
                        break;
                    case 4: // Paeth
                        UnfilterPaeth(src, unfiltered, sRow, dRow, rowBytesPacked, bppBytes, y, rowBytesPacked);
                        break;
                    default: throw new InvalidDataException($"PNG: unknown filter {f}");
                }
            }

            // Expand/convert packed rows to 8-bit channels (and optional alpha)
            ExpandToResult(res, unfiltered, width, height, bitDepth, ct, spp, plte, trns, isInterlaced: false);
        }

        // -------------------------- Adam7 Interlace --------------------------
        private static readonly int[] Adam7_StartingX = { 0, 4, 0, 2, 0, 1, 0 };
        private static readonly int[] Adam7_StartingY = { 0, 0, 4, 0, 2, 0, 1 };
        private static readonly int[] Adam7_StepX = { 8, 8, 4, 4, 2, 2, 1 };
        private static readonly int[] Adam7_StepY = { 8, 8, 8, 4, 4, 2, 2 };

        private static void DecodeAdam7(
            Result res, byte[] src, int width, int height, int bitDepth, int ct, int spp,
            byte[]? plte, byte[]? trns)
        {
            // We'll iterate through the 7 passes and scatter into final image buffers.
            // First compute the byte-per-filter basis for each pass.
            int srcPos = 0;
            var passRows = new List<byte[]>(7); // unfiltered packed rows per pass in order

            // We'll collect all unfiltered rows for all passes first (so we can bounds-check src length properly)
            for (int pass = 0; pass < 7; pass++)
            {
                int x0 = Adam7_StartingX[pass];
                int y0 = Adam7_StartingY[pass];
                int dx = Adam7_StepX[pass];
                int dy = Adam7_StepY[pass];

                int pw = Adam7ComputeSize(width, x0, dx);
                int ph = Adam7ComputeSize(height, y0, dy);

                if (pw == 0 || ph == 0)
                {
                    passRows.Add(Array.Empty<byte>());
                    continue;
                }

                int bppBytes = BytesPerPixelForFilter(spp, bitDepth);
                int rowBytesPacked = RowBytesPacked(pw, spp, bitDepth);

                var unfiltered = new byte[ph * rowBytesPacked];

                for (int y = 0; y < ph; y++)
                {
                    if (srcPos >= src.Length) throw new InvalidDataException("PNG: truncated IDAT (Adam7)");

                    byte filter = src[srcPos++];
                    int sRow = srcPos;
                    srcPos += rowBytesPacked;

                    if (srcPos > src.Length) throw new InvalidDataException("PNG: truncated IDAT (Adam7 row)");

                    int dRow = y * rowBytesPacked;
                    switch (filter)
                    {
                        case 0: Buffer.BlockCopy(src, sRow, unfiltered, dRow, rowBytesPacked); break;
                        case 1: UnfilterSub(src, unfiltered, sRow, dRow, rowBytesPacked, bppBytes); break;
                        case 2: UnfilterUpPacked(unfiltered, sRow, dRow, rowBytesPacked, y, rowBytesPacked); break;
                        case 3: UnfilterAveragePacked(unfiltered, sRow, dRow, rowBytesPacked, bppBytes, y, rowBytesPacked); break;
                        case 4: UnfilterPaethPacked(unfiltered, sRow, dRow, rowBytesPacked, bppBytes, y, rowBytesPacked); break;
                        default: throw new InvalidDataException($"PNG: unknown filter {filter} in pass {pass + 1}");
                    }
                }

                passRows.Add(unfiltered);
            }

            // Now expand each pass rows and scatter into final image
            ExpandAdam7ToResult(res, passRows, width, height, bitDepth, ct, spp, plte, trns);
        }

        private static int Adam7ComputeSize(int full, int start, int step)
        {
            if (start >= full) return 0;
            return (full - start + step - 1) / step;
        }

        // -------------------------- Expand/Convert to Result --------------------------
        private static void ExpandToResult(
            Result res, byte[] rowsPacked, int width, int height, int bitDepth, int ct, int spp,
            byte[]? plte, byte[]? trns, bool isInterlaced)
        {
            // Common entry for non-interlaced
            switch (ct)
            {
                case 0: // Gray
                    res.Components = 1;
                    res.Pixels = new byte[width * height];
                    res.Alpha = BuildAlphaFromTRNS_Gray(trns, rowsPacked, width, height, bitDepth);
                    UnpackGray(rowsPacked, res.Pixels, width, height, bitDepth);
                    break;

                case 2: // RGB
                    res.Components = 3;
                    res.Pixels = new byte[width * height * 3];
                    res.Alpha = BuildAlphaFromTRNS_RGB(trns, rowsPacked, width, height, bitDepth);
                    UnpackRGB(rowsPacked, res.Pixels, width, height, bitDepth);
                    break;

                case 3: // Indexed
                    if (plte == null) throw new InvalidDataException("PNG indexed without PLTE");
                    res.IsIndexed = true;
                    res.Components = 1;
                    res.PaletteRGB = plte;
                    res.Pixels = new byte[width * height]; // indices expanded to 8-bit
                    UnpackIndexed(rowsPacked, res.Pixels, width, height, bitDepth);
                    // Build tRNS-derived alpha: needs (indices, trns) order
                    res.Alpha = BuildAlphaFromTRNS_Indexed(res.Pixels, trns);
                    break;

                case 4: // Gray + Alpha
                    res.Components = 1;
                    res.Pixels = new byte[width * height];
                    res.Alpha = new byte[width * height];
                    UnpackGrayAlpha(rowsPacked, res.Pixels, res.Alpha, width, height, bitDepth);
                    break;

                case 6: // RGB + Alpha
                    res.Components = 3;
                    res.Pixels = new byte[width * height * 3];
                    res.Alpha = new byte[width * height];
                    UnpackRGBA(rowsPacked, res.Pixels, res.Alpha, width, height, bitDepth);
                    break;

                default:
                    throw new NotSupportedException($"PNG: unsupported color type {ct}");
            }
        }

        private static void ExpandAdam7ToResult(
            Result res, List<byte[]> passRows, int width, int height, int bitDepth, int ct, int spp,
            byte[]? plte, byte[]? trns)
        {
            switch (ct)
            {
                case 0: // Gray
                    res.Components = 1;
                    res.Pixels = new byte[width * height];
                    byte[]? aG = BuildAlphaFromTRNS_Gray_Adam7(trns, passRows, width, height, bitDepth);
                    UnpackGray_Adam7(passRows, res.Pixels, width, height, bitDepth);
                    res.Alpha = aG;
                    break;

                case 2: // RGB
                    res.Components = 3;
                    res.Pixels = new byte[width * height * 3];
                    byte[]? aR = BuildAlphaFromTRNS_RGB_Adam7(trns, passRows, width, height, bitDepth);
                    UnpackRGB_Adam7(passRows, res.Pixels, width, height, bitDepth);
                    res.Alpha = aR;
                    break;

                case 3: // Indexed
                    if (plte == null) throw new InvalidDataException("PNG indexed without PLTE");
                    res.IsIndexed = true;
                    res.Components = 1;
                    res.PaletteRGB = plte;
                    res.Pixels = new byte[width * height];
                    UnpackIndexed_Adam7(passRows, res.Pixels, width, height, bitDepth);
                    res.Alpha = BuildAlphaFromTRNS_Indexed(res.Pixels, trns);
                    break;

                case 4: // Gray+Alpha
                    res.Components = 1;
                    res.Pixels = new byte[width * height];
                    res.Alpha = new byte[width * height];
                    UnpackGrayAlpha_Adam7(passRows, res.Pixels, res.Alpha, width, height, bitDepth);
                    break;

                case 6: // RGBA
                    res.Components = 3;
                    res.Pixels = new byte[width * height * 3];
                    res.Alpha = new byte[width * height];
                    UnpackRGBA_Adam7(passRows, res.Pixels, res.Alpha, width, height, bitDepth);
                    break;

                default:
                    throw new NotSupportedException($"PNG: unsupported color type {ct}");
            }
        }

        // -------------------------- Unpackers (non-interlaced) --------------------------
        private static void UnpackGray(byte[] packed, byte[] dstG, int w, int h, int bd)
        {
            if (bd == 8)
            {
                CopyRows(packed, dstG, w, h);
            }
            else if (bd == 16)
            {
                // take MSB
                int srcStride = w * 2;
                for (int y = 0; y < h; y++)
                {
                    int s = y * srcStride;
                    int d = y * w;
                    for (int x = 0; x < w; x++, s += 2, d++)
                        dstG[d] = packed[s];
                }
            }
            else // 1/2/4
            {
                UnpackBitDepthTo8(packed, dstG, w, h, bd);
            }
        }

        private static void UnpackRGB(byte[] packed, byte[] dstRGB, int w, int h, int bd)
        {
            if (bd == 8)
            {
                CopyRows(packed, dstRGB, w * 3, h);
            }
            else if (bd == 16)
            {
                int srcStride = w * 3 * 2;
                for (int y = 0; y < h; y++)
                {
                    int s = y * srcStride;
                    int d = y * w * 3;
                    for (int x = 0; x < w; x++)
                    {
                        dstRGB[d++] = packed[s]; s += 2; // R MSB
                        dstRGB[d++] = packed[s]; s += 2; // G MSB
                        dstRGB[d++] = packed[s]; s += 2; // B MSB
                    }
                }
            }
            else
            {
                // RGB cannot have bd < 8 per spec (only 8 or 16). Still guard:
                throw new InvalidDataException("RGB PNG with bit depth < 8 is invalid");
            }
        }

        private static void UnpackIndexed(byte[] packedIdx, byte[] dstIdx, int w, int h, int bd)
        {
            if (bd == 8)
            {
                CopyRows(packedIdx, dstIdx, w, h);
            }
            else
            {
                UnpackBitDepthTo8(packedIdx, dstIdx, w, h, bd);
            }
        }

        private static void UnpackGrayAlpha(byte[] packed, byte[] dstG, byte[] dstA, int w, int h, int bd)
        {
            if (bd == 8)
            {
                int srcStride = w * 2;
                for (int y = 0; y < h; y++)
                {
                    int s = y * srcStride;
                    int dg = y * w;
                    int da = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        dstG[dg++] = packed[s++];
                        dstA[da++] = packed[s++];
                    }
                }
            }
            else if (bd == 16)
            {
                int srcStride = w * 2 * 2;
                for (int y = 0; y < h; y++)
                {
                    int s = y * srcStride;
                    int dg = y * w;
                    int da = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        dstG[dg++] = packed[s]; s += 2;
                        dstA[da++] = packed[s]; s += 2;
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Gray+Alpha PNG with bit depth < 8 is invalid");
            }
        }

        private static void UnpackRGBA(byte[] packed, byte[] dstRGB, byte[] dstA, int w, int h, int bd)
        {
            if (bd == 8)
            {
                int srcStride = w * 4;
                for (int y = 0; y < h; y++)
                {
                    int s = y * srcStride;
                    int d = y * w * 3;
                    int a = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        dstRGB[d++] = packed[s++]; // R
                        dstRGB[d++] = packed[s++]; // G
                        dstRGB[d++] = packed[s++]; // B
                        dstA[a++] = packed[s++];   // A
                    }
                }
            }
            else if (bd == 16)
            {
                int srcStride = w * 4 * 2;
                for (int y = 0; y < h; y++)
                {
                    int s = y * srcStride;
                    int d = y * w * 3;
                    int a = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        dstRGB[d++] = packed[s]; s += 2; // R MSB
                        dstRGB[d++] = packed[s]; s += 2; // G MSB
                        dstRGB[d++] = packed[s]; s += 2; // B MSB
                        dstA[a++] = packed[s]; s += 2; // A MSB
                    }
                }
            }
            else
            {
                throw new InvalidDataException("RGBA PNG with bit depth < 8 is invalid");
            }
        }

        // -------------------------- Unpackers (Adam7) --------------------------
        private static void UnpackGray_Adam7(List<byte[]> passRows, byte[] dstG, int w, int h, int bd)
        {
            for (int pass = 0; pass < 7; pass++)
            {
                int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                int pw = Adam7ComputeSize(w, x0, dx);
                int ph = Adam7ComputeSize(h, y0, dy);
                if (pw == 0 || ph == 0) continue;

                var rows = passRows[pass];
                if (bd == 8)
                {
                    int rowBytes = pw;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int d = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++, s++)
                        {
                            dstG[d + xx] = rows[s];
                            xx += dx;
                        }
                    }
                }
                else if (bd == 16)
                {
                    int rowBytes = pw * 2;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int d = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++, s += 2)
                        {
                            dstG[d + xx] = rows[s]; // MSB
                            xx += dx;
                        }
                    }
                }
                else // 1/2/4
                {
                    var tmp = new byte[pw];
                    for (int y = 0; y < ph; y++)
                    {
                        int yy = y0 + y * dy;
                        UnpackBitsRow(rows, y, pw, bd, tmp);
                        int d = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            // scale to 8-bit
                            dstG[d + xx] = ScaleTo8(tmp[x], bd);
                            xx += dx;
                        }
                    }
                }
            }
        }

        private static void UnpackRGB_Adam7(List<byte[]> passRows, byte[] dstRGB, int w, int h, int bd)
        {
            if (bd == 8)
            {
                for (int pass = 0; pass < 7; pass++)
                {
                    int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                    int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                    int pw = Adam7ComputeSize(w, x0, dx);
                    int ph = Adam7ComputeSize(h, y0, dy);
                    if (pw == 0 || ph == 0) continue;

                    var rows = passRows[pass];
                    int rowBytes = pw * 3;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int d = yy * w * 3;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            dstRGB[d + xx * 3 + 0] = rows[s++];
                            dstRGB[d + xx * 3 + 1] = rows[s++];
                            dstRGB[d + xx * 3 + 2] = rows[s++];
                            xx += dx;
                        }
                    }
                }
            }
            else if (bd == 16)
            {
                for (int pass = 0; pass < 7; pass++)
                {
                    int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                    int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                    int pw = Adam7ComputeSize(w, x0, dx);
                    int ph = Adam7ComputeSize(h, y0, dy);
                    if (pw == 0 || ph == 0) continue;

                    var rows = passRows[pass];
                    int rowBytes = pw * 3 * 2;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int d = yy * w * 3;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            dstRGB[d + xx * 3 + 0] = rows[s]; s += 2;
                            dstRGB[d + xx * 3 + 1] = rows[s]; s += 2;
                            dstRGB[d + xx * 3 + 2] = rows[s]; s += 2;
                            xx += dx;
                        }
                    }
                }
            }
            else
            {
                throw new InvalidDataException("RGB PNG with bit depth < 8 is invalid");
            }
        }

        private static void UnpackIndexed_Adam7(List<byte[]> passRows, byte[] dstIdx, int w, int h, int bd)
        {
            for (int pass = 0; pass < 7; pass++)
            {
                int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                int pw = Adam7ComputeSize(w, x0, dx);
                int ph = Adam7ComputeSize(h, y0, dy);
                if (pw == 0 || ph == 0) continue;

                var rows = passRows[pass];

                if (bd == 8)
                {
                    int rowBytes = pw;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int d = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++, s++)
                        {
                            dstIdx[d + xx] = rows[s];
                            xx += dx;
                        }
                    }
                }
                else
                {
                    var tmp = new byte[pw];
                    for (int y = 0; y < ph; y++)
                    {
                        UnpackBitsRow(rows, y, pw, bd, tmp);
                        int yy = y0 + y * dy;
                        int d = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            dstIdx[d + xx] = tmp[x];
                            xx += dx;
                        }
                    }
                }
            }
        }

        private static void UnpackGrayAlpha_Adam7(List<byte[]> passRows, byte[] dstG, byte[] dstA, int w, int h, int bd)
        {
            if (bd == 8)
            {
                for (int pass = 0; pass < 7; pass++)
                {
                    int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                    int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                    int pw = Adam7ComputeSize(w, x0, dx);
                    int ph = Adam7ComputeSize(h, y0, dy);
                    if (pw == 0 || ph == 0) continue;

                    var rows = passRows[pass];
                    int rowBytes = pw * 2;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int dg = yy * w;
                        int da = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            dstG[dg + xx] = rows[s++];
                            dstA[da + xx] = rows[s++];
                            xx += dx;
                        }
                    }
                }
            }
            else if (bd == 16)
            {
                for (int pass = 0; pass < 7; pass++)
                {
                    int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                    int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                    int pw = Adam7ComputeSize(w, x0, dx);
                    int ph = Adam7ComputeSize(h, y0, dy);
                    if (pw == 0 || ph == 0) continue;

                    var rows = passRows[pass];
                    int rowBytes = pw * 2 * 2;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int dg = yy * w;
                        int da = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            dstG[dg + xx] = rows[s]; s += 2;
                            dstA[da + xx] = rows[s]; s += 2;
                            xx += dx;
                        }
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Gray+Alpha PNG with bit depth < 8 is invalid");
            }
        }

        private static void UnpackRGBA_Adam7(List<byte[]> passRows, byte[] dstRGB, byte[] dstA, int w, int h, int bd)
        {
            if (bd == 8)
            {
                for (int pass = 0; pass < 7; pass++)
                {
                    int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                    int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                    int pw = Adam7ComputeSize(w, x0, dx);
                    int ph = Adam7ComputeSize(h, y0, dy);
                    if (pw == 0 || ph == 0) continue;

                    var rows = passRows[pass];
                    int rowBytes = pw * 4;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int d = yy * w * 3;
                        int a = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            dstRGB[d + xx * 3 + 0] = rows[s++];
                            dstRGB[d + xx * 3 + 1] = rows[s++];
                            dstRGB[d + xx * 3 + 2] = rows[s++];
                            dstA[a + xx] = rows[s++];
                            xx += dx;
                        }
                    }
                }
            }
            else if (bd == 16)
            {
                for (int pass = 0; pass < 7; pass++)
                {
                    int x0 = Adam7_StartingX[pass], y0 = Adam7_StartingY[pass];
                    int dx = Adam7_StepX[pass], dy = Adam7_StepY[pass];
                    int pw = Adam7ComputeSize(w, x0, dx);
                    int ph = Adam7ComputeSize(h, y0, dy);
                    if (pw == 0 || ph == 0) continue;

                    var rows = passRows[pass];
                    int rowBytes = pw * 4 * 2;
                    for (int y = 0; y < ph; y++)
                    {
                        int s = y * rowBytes;
                        int yy = y0 + y * dy;
                        int d = yy * w * 3;
                        int a = yy * w;
                        int xx = x0;
                        for (int x = 0; x < pw; x++)
                        {
                            dstRGB[d + xx * 3 + 0] = rows[s]; s += 2;
                            dstRGB[d + xx * 3 + 1] = rows[s]; s += 2;
                            dstRGB[d + xx * 3 + 2] = rows[s]; s += 2;
                            dstA[a + xx] = rows[s]; s += 2;
                            xx += dx;
                        }
                    }
                }
            }
            else
            {
                throw new InvalidDataException("RGBA PNG with bit depth < 8 is invalid");
            }
        }

        // -------------------------- tRNS helpers --------------------------
        private static byte[]? BuildAlphaFromTRNS_Gray(byte[]? trns, byte[] packed, int w, int h, int bd)
        {
            if (trns == null) return null;
            if (trns.Length < 2) return null;

            // tRNS gray key is 16-bit, scale depends on bit depth.
            int keyN = trns[0] << 8 | trns[1];
            int keySample = bd switch
            {
                16 => keyN,
                8 => keyN >> 8,
                4 => keyN >> 12,
                2 => keyN >> 14,
                1 => keyN >> 15,
                _ => keyN >> 8
            };

            var alpha = new byte[w * h];

            if (bd == 16)
            {
                int rowBytes = w * 2;
                for (int y = 0; y < h; y++)
                {
                    int s = y * rowBytes;
                    int a = y * w;
                    for (int x = 0; x < w; x++, s += 2, a++)
                    {
                        int g16 = (packed[s] << 8) | packed[s + 1];
                        alpha[a] = (g16 == keySample) ? (byte)0x00 : (byte)0xFF;
                    }
                }
            }
            else if (bd == 8)
            {
                CopyKeyCompare8(packed, alpha, w, h, (byte)keySample);
            }
            else // 1/2/4
            {
                var tmp = new byte[w];
                for (int y = 0; y < h; y++)
                {
                    UnpackBitsRow(packed, y, w, bd, tmp);
                    int a = y * w;
                    for (int x = 0; x < w; x++, a++)
                        alpha[a] = (tmp[x] == keySample) ? (byte)0x00 : (byte)0xFF;
                }
            }

            return alpha;
        }

        private static byte[]? BuildAlphaFromTRNS_RGB(byte[]? trns, byte[] packed, int w, int h, int bd)
        {
            if (trns == null || trns.Length < 6) return null;

            int rKey = trns[0] << 8 | trns[1];
            int gKey = trns[2] << 8 | trns[3];
            int bKey = trns[4] << 8 | trns[5];

            var alpha = new byte[w * h];

            if (bd == 16)
            {
                int rowBytes = w * 3 * 2;
                for (int y = 0; y < h; y++)
                {
                    int s = y * rowBytes;
                    int a = y * w;
                    for (int x = 0; x < w; x++, a++)
                    {
                        int r = (packed[s] << 8) | packed[s + 1]; s += 2;
                        int g = (packed[s] << 8) | packed[s + 1]; s += 2;
                        int b = (packed[s] << 8) | packed[s + 1]; s += 2;
                        alpha[a] = (r == rKey && g == gKey && b == bKey) ? (byte)0x00 : (byte)0xFF;
                    }
                }
            }
            else // 8-bit
            {
                int rowBytes = w * 3;
                byte rk = (byte)(rKey >> 8), gk = (byte)(gKey >> 8), bk = (byte)(bKey >> 8);
                for (int y = 0; y < h; y++)
                {
                    int s = y * rowBytes;
                    int a = y * w;
                    for (int x = 0; x < w; x++, a++)
                    {
                        byte r = packed[s++], g = packed[s++], b = packed[s++];
                        alpha[a] = (r == rk && g == gk && b == bk) ? (byte)0x00 : (byte)0xFF;
                    }
                }
            }

            return alpha;
        }

        private static byte[]? BuildAlphaFromTRNS_Indexed(byte[]? indices, byte[]? trns)
        {
            if (indices == null || trns == null) return null;
            var alpha = new byte[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                byte idx = indices[i];
                alpha[i] = idx < trns.Length ? trns[idx] : (byte)0xFF;
            }
            return alpha;
        }

        private static byte[]? BuildAlphaFromTRNS_Gray_Adam7(byte[]? trns, List<byte[]> passRows, int w, int h, int bd)
        {
            if (trns == null) return null;
            var tmp = new Result { Width = w, Height = h, Components = 1, Pixels = new byte[w * h] };
            UnpackGray_Adam7(passRows, tmp.Pixels, w, h, bd);
            // Reuse non-interlaced builder on reconstructed pixels (converted to 8-bit already)
            if (bd >= 8)
            {
                // If bd==16 we used MSB, else exact; we can compare against scaled key
                int keyN = trns[0] << 8 | trns[1];
                int keySample = bd switch
                {
                    16 => keyN >> 8,
                    8 => keyN >> 8,
                    4 => keyN >> 12,
                    2 => keyN >> 14,
                    1 => keyN >> 15,
                    _ => keyN >> 8
                };
                var alpha = new byte[w * h];
                for (int i = 0; i < tmp.Pixels.Length; i++)
                    alpha[i] = (tmp.Pixels[i] == (byte)keySample) ? (byte)0x00 : (byte)0xFF;
                return alpha;
            }
            else
            {
                // bd < 8 path is handled above by UnpackGray_Adam7 (scaled already)
                int keyN = trns[0] << 8 | trns[1];
                int keySample = keyN >> (16 - bd);
                var alpha = new byte[w * h];
                for (int i = 0; i < tmp.Pixels.Length; i++)
                    alpha[i] = (tmp.Pixels[i] == ScaleTo8(keySample, bd)) ? (byte)0x00 : (byte)0xFF;
                return alpha;
            }
        }

        private static byte[]? BuildAlphaFromTRNS_RGB_Adam7(byte[]? trns, List<byte[]> passRows, int w, int h, int bd)
        {
            if (trns == null) return null;
            var tmp = new Result { Width = w, Height = h, Components = 3, Pixels = new byte[w * h * 3] };
            UnpackRGB_Adam7(passRows, tmp.Pixels, w, h, bd);

            int rKey = trns[0] << 8 | trns[1];
            int gKey = trns[2] << 8 | trns[3];
            int bKey = trns[4] << 8 | trns[5];
            byte rk = (byte)(rKey >> 8), gk = (byte)(gKey >> 8), bk = (byte)(bKey >> 8);

            var alpha = new byte[w * h];
            for (int i = 0, p = 0; i < alpha.Length; i++)
            {
                byte r = tmp.Pixels[p++], g = tmp.Pixels[p++], b = tmp.Pixels[p++];
                alpha[i] = (r == rk && g == gk && b == bk) ? (byte)0x00 : (byte)0xFF;
            }
            return alpha;
        }

        // -------------------------- Filters --------------------------
        private static void UnfilterSub(byte[] src, byte[] dst, int sRow, int dRow, int count, int bpp)
        {
            for (int x = 0; x < count; x++)
            {
                int left = (x >= bpp) ? dst[dRow + x - bpp] : 0;
                dst[dRow + x] = (byte)(src[sRow + x] + left);
            }
        }

        private static void UnfilterUp(byte[] src, byte[] dst, int sRow, int dRow, int count, int y, int stride)
        {
            for (int x = 0; x < count; x++)
            {
                int up = (y > 0) ? dst[dRow - stride + x] : 0;
                dst[dRow + x] = (byte)(src[sRow + x] + up);
            }
        }

        // Versions that read previous row from already-unfiltered 'dst' (used for Adam7 where src is not contiguous)
        private static void UnfilterUpPacked(byte[] dst, int sRow, int dRow, int count, int y, int stride)
        {
            for (int x = 0; x < count; x++)
            {
                int up = (y > 0) ? dst[dRow - stride + x] : 0;
                dst[dRow + x] = (byte)(dst[sRow + x] + up);
            }
        }

        private static void UnfilterAverage(byte[] src, byte[] dst, int sRow, int dRow, int count, int bpp, int y, int stride)
        {
            for (int x = 0; x < count; x++)
            {
                int left = (x >= bpp) ? dst[dRow + x - bpp] : 0;
                int up = (y > 0) ? dst[dRow - stride + x] : 0;
                dst[dRow + x] = (byte)(src[sRow + x] + ((left + up) >> 1));
            }
        }

        private static void UnfilterAveragePacked(byte[] dst, int sRow, int dRow, int count, int bpp, int y, int stride)
        {
            for (int x = 0; x < count; x++)
            {
                int left = (x >= bpp) ? dst[dRow + x - bpp] : 0;
                int up = (y > 0) ? dst[dRow - stride + x] : 0;
                dst[dRow + x] = (byte)(dst[sRow + x] + ((left + up) >> 1));
            }
        }

        private static void UnfilterPaeth(byte[] src, byte[] dst, int sRow, int dRow, int count, int bpp, int y, int stride)
        {
            for (int x = 0; x < count; x++)
            {
                int a = (x >= bpp) ? dst[dRow + x - bpp] : 0;             // left
                int b = (y > 0) ? dst[dRow - stride + x] : 0;             // up
                int c = (x >= bpp && y > 0) ? dst[dRow - stride + x - bpp] : 0; // up-left
                dst[dRow + x] = (byte)(src[sRow + x] + Paeth(a, b, c));
            }
        }

        private static void UnfilterPaethPacked(byte[] dst, int sRow, int dRow, int count, int bpp, int y, int stride)
        {
            for (int x = 0; x < count; x++)
            {
                int a = (x >= bpp) ? dst[dRow + x - bpp] : 0;
                int b = (y > 0) ? dst[dRow - stride + x] : 0;
                int c = (x >= bpp && y > 0) ? dst[dRow - stride + x - bpp] : 0;
                dst[dRow + x] = (byte)(dst[sRow + x] + Paeth(a, b, c));
            }
        }

        private static byte Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return (byte)a;
            if (pb <= pc) return (byte)b;
            return (byte)c;
        }

        // -------------------------- Utilities --------------------------
        private static void ValidateIHDR(int w, int h, int bd, int ct, int interlace)
        {
            if (w <= 0 || h <= 0) throw new InvalidDataException("PNG: invalid dimensions");
            if (bd != 1 && bd != 2 && bd != 4 && bd != 8 && bd != 16)
                throw new NotSupportedException($"PNG: unsupported bit depth {bd}");

            // Valid color type + bit depth combos
            switch (ct)
            {
                case 0: /* Gray */ if (!(bd == 1 || bd == 2 || bd == 4 || bd == 8 || bd == 16)) throw new NotSupportedException(); break;
                case 2: /* RGB  */ if (!(bd == 8 || bd == 16)) throw new NotSupportedException(); break;
                case 3: /* Indexed */ if (!(bd == 1 || bd == 2 || bd == 4 || bd == 8)) throw new NotSupportedException(); break;
                case 4: /* Gray+Alpha */ if (!(bd == 8 || bd == 16)) throw new NotSupportedException(); break;
                case 6: /* RGBA */ if (!(bd == 8 || bd == 16)) throw new NotSupportedException(); break;
                default: throw new NotSupportedException($"PNG: unsupported color type {ct}");
            }
            if (interlace != 0 && interlace != 1) throw new NotSupportedException("PNG: unknown interlace method");
        }

        private static int SamplesPerPixel(int ct) => ct switch
        {
            0 => 1, // Gray
            2 => 3, // RGB
            3 => 1, // Indexed (indices)
            4 => 2, // Gray+Alpha
            6 => 4, // RGBA
            _ => throw new NotSupportedException()
        };

        private static int BytesPerPixelForFilter(int spp, int bd)
        {
            int bitsPerPixel = spp * bd;
            return Math.Max(1, (bitsPerPixel + 7) / 8);
        }

        private static int RowBytesPacked(int w, int spp, int bd)
        {
            int bits = w * spp * bd;
            return (bits + 7) / 8;
        }

        private static void CopyRows(byte[] src, byte[] dst, int rowWidth, int rows)
        {
            int srcStride = rowWidth;
            int dstStride = rowWidth;
            Buffer.BlockCopy(src, 0, dst, 0, rows * rowWidth);
        }

        private static void UnpackBitDepthTo8(byte[] packed, byte[] dst, int w, int h, int bd)
        {
            for (int y = 0; y < h; y++)
                UnpackBitsRowTo8(packed, y, w, bd, dst, y * w);
        }

        private static void UnpackBitsRow(byte[] packedRows, int y, int w, int bd, byte[] tmpOut)
        {
            // Extract raw sample values (0..(2^bd -1)) into tmpOut
            int rowBytes = (w * bd + 7) / 8;
            int s = y * rowBytes;
            int bitMask = (1 << bd) - 1;

            int bitsRead = 0;
            byte cur = 0;
            for (int i = 0; i < w; i++)
            {
                if (bitsRead == 0)
                {
                    cur = packedRows[s++];
                    bitsRead = 8;
                }
                int shift = bitsRead - bd;
                byte val = (byte)((cur >> shift) & bitMask);
                bitsRead -= bd;
                tmpOut[i] = val;
                if (bitsRead == 0 && ((w - (i + 1)) * bd) % 8 != 0)
                {
                    // will load next on next iteration
                }
            }
        }

        private static void UnpackBitsRowTo8(byte[] packedRows, int y, int w, int bd, byte[] dst, int dstOffset)
        {
            var tmp = new byte[w];
            UnpackBitsRow(packedRows, y, w, bd, tmp);
            for (int i = 0; i < w; i++)
                dst[dstOffset + i] = ScaleTo8(tmp[i], bd);
        }

        private static byte ScaleTo8(int v, int bd)
        {
            if (bd == 8) return (byte)v;
            int max = (1 << bd) - 1;
            return (byte)((v * 255 + (max / 2)) / max);
        }

        private static void CopyKeyCompare8(byte[] g, byte[] alpha, int w, int h, byte key)
        {
            for (int y = 0; y < h; y++)
            {
                int s = y * w;
                for (int x = 0; x < w; x++)
                {
                    alpha[s] = (g[s] == key) ? (byte)0x00 : (byte)0xFF;
                    s++;
                }
            }
        }

        private static int ReadInt32BE(BinaryReader br)
            => BinaryPrimitives.ReadInt32BigEndian(br.ReadBytes(4));

        private static string ReadAscii(BinaryReader br, int n)
            => System.Text.Encoding.ASCII.GetString(br.ReadBytes(n));
    }
}
