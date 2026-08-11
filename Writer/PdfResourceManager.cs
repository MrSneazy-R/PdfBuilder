// --- PdfResourceManager.cs ---
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PdfBuilder.Document.Layout;
using PdfBuilder.Elements;
using PdfBuilder.Models;
using PdfBuilder.Writer.Imaging;

namespace PdfBuilder.Writer
{
    /// <summary>
    /// Tracks and writes shared PDF resources (fonts, images, extgstates).
    /// Supports PNG (all types via PngDecoder), JPEG (incl. CMYK/YCCK + ICC),
    /// and WebP through the shared cross-platform Skia codec.
    /// Native PNG and JPEG streams remain untouched unless optimisation is requested.
    /// </summary>
    public class PdfResourceManager
    {
        // Legacy maps (back-compat with any old call sites)
        private readonly Dictionary<string, int> _fonts = new();
        private readonly Dictionary<string, int> _imagesLegacy = new();
        private readonly Dictionary<float, int> _opacityStatesLegacy = new();

        // The hash identifies candidates; byte equality prevents a collision from aliasing two assets.
        // Each manager is document-scoped, so no caller-owned image data is shared across documents.
        private readonly Dictionary<string, List<ImageResource>> _imageMap = new(StringComparer.Ordinal);
        private readonly PdfOutputOptions _options;
        private readonly PdfRenderLimits _renderLimits;

        private sealed class ImageResource
        {
            public ImageResource(byte[] content, int imageObjectId, int? softMaskObjectId)
            {
                Content = content;
                ImageObjectId = imageObjectId;
                SoftMaskObjectId = softMaskObjectId;
            }

            public byte[] Content { get; }
            public int ImageObjectId { get; }
            public int? SoftMaskObjectId { get; }
        }

        public PdfResourceManager()
            : this(null)
        {
        }

        public PdfResourceManager(PdfOutputOptions? options)
            : this(options, null)
        {
        }

        public PdfResourceManager(PdfOutputOptions? options, PdfRenderLimits? renderLimits)
        {
            _options = options ?? new PdfOutputOptions();
            _renderLimits = renderLimits ?? new PdfRenderLimits();
        }

        private readonly struct ExtGStateHandle
        {
            public ExtGStateHandle(string resourceName, int objectId)
            {
                ResourceName = resourceName;
                ObjectId = objectId;
            }

            public string ResourceName { get; }
            public int ObjectId { get; }
        }

        // ExtGState registry
        private readonly Dictionary<string, ExtGStateHandle> _extGStates = new(StringComparer.Ordinal);

        // ---------- LEGACY HELPERS ----------
        public int RegisterFont(string fontName, Func<int> addObject)
        {
            if (_fonts.ContainsKey(fontName)) return _fonts[fontName];
            int id = addObject();
            _fonts[fontName] = id;
            return id;
        }

        public int RegisterImage(string key, Func<int> addObject)
        {
            if (_imagesLegacy.ContainsKey(key)) return _imagesLegacy[key];
            int id = addObject();
            _imagesLegacy[key] = id;
            return id;
        }

        public int RegisterOpacity(float opacity, Func<int> addObject)
        {
            if (_opacityStatesLegacy.ContainsKey(opacity)) return _opacityStatesLegacy[opacity];
            int id = addObject();
            _opacityStatesLegacy[opacity] = id;
            return id;
        }

        // ---------- NEW IMAGE API ----------
        /// <summary>
        /// Ensure an image XObject exists for the given element and return:
        /// (image obj id, optional SMask obj id, optional ExtGState resource name, pdf resource name)
        /// </summary>
        public (int imageObjId, int? smaskObjId, string? extGStateResourceName, string pdfName)
            EnsureImageXObject(PdfStreamWriter w, ImageElement img)
        {
            if (img == null) throw new ArgumentNullException(nameof(img));
            byte[] sourceData = img.ResolveImageData();
            if (sourceData.Length == 0)
                throw new InvalidDataException("ImageElement image source is empty.");

            PreparedImage prepared = SkiaImageOptimiser.Prepare(sourceData, img, _renderLimits.MaximumImagePixels);
            ImageInfo imageInfo = prepared.SourceInfo;
            _renderLimits.ValidateImagePixels(imageInfo.PixelCount);
            bool swapsDimensions = imageInfo.Orientation is ImageOrientation.LeftTop or ImageOrientation.RightTop or ImageOrientation.RightBottom or ImageOrientation.LeftBottom;
            img.SourcePixelWidth = swapsDimensions ? imageInfo.Height : imageInfo.Width;
            img.SourcePixelHeight = swapsDimensions ? imageInfo.Width : imageInfo.Height;
            img.SourceDpiX = swapsDimensions ? imageInfo.DpiY : imageInfo.DpiX;
            img.SourceDpiY = swapsDimensions ? imageInfo.DpiX : imageInfo.DpiY;

            string key = Hash(prepared.Data);
            if (!_imageMap.TryGetValue(key, out var candidates))
            {
                candidates = new List<ImageResource>();
                _imageMap.Add(key, candidates);
            }

            var resource = candidates.FirstOrDefault(candidate =>
                candidate.Content.AsSpan().SequenceEqual(prepared.Data));
            if (resource == null)
            {
                // Keep a private immutable snapshot for collision-safe equality. The original data
                // may be caller-owned and must not be retained as mutable shared state.
                var ids = WriteImageAuto(w, prepared.Data);
                resource = new ImageResource(prepared.Data.ToArray(), ids.imObj, ids.smaskObj);
                candidates.Add(resource);
            }

            // Optional overall opacity (separate from per-pixel alpha)
            string? gsName = null;
            float op = Clamp01(img.Opacity);
            if (op < 0.999f)
            {
                var handle = EnsureImageOpacityExtGState(op, w);
                gsName = handle.ResourceName;
            }

            string name = $"/Im{resource.ImageObjectId}";
            img.PdfResourceName = name; // optional debug
            return (resource.ImageObjectId, resource.SoftMaskObjectId, gsName, name);
        }

        /// <summary>Build /XObject entries, e.g. "/Im5 5 0 R /Im9 9 0 R"</summary>
        public string BuildXObjectResources()
        {
            if (_imageMap.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var resource in _imageMap.Values.SelectMany(value => value).OrderBy(value => value.ImageObjectId))
            {
                int id = resource.ImageObjectId;
                sb.Append($"/Im{id} {id} 0 R ");
            }
            return sb.ToString();
        }

        /// <summary>Build /ExtGState entries, e.g. "/GS7 7 0 R"</summary>
        public string BuildExtGStateResources()
        {
            if (_extGStates.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var entry in _extGStates.Values)
                sb.Append($"{entry.ResourceName} {entry.ObjectId} 0 R ");
            return sb.ToString();
        }

        private ExtGStateHandle EnsureImageOpacityExtGState(float opacity, PdfStreamWriter w)
        {
            string opToken = opacity.ToString("0.###", CultureInfo.InvariantCulture);
            string key = $"img:{opToken}";
            if (_extGStates.TryGetValue(key, out var handle))
                return handle;

            int objId = w.BeginObject();
            w.WriteLine("<< /Type /ExtGState");
            w.WriteLine($"   /CA {opToken} /ca {opToken} >>");
            w.EndObject();

            handle = new ExtGStateHandle($"/GS{objId}", objId);
            _extGStates[key] = handle;
            return handle;
        }

        public string EnsureWatermarkExtGState(float opacity, PdfStreamWriter w)
        {
            float op = Clamp01(opacity);
            string opToken = op.ToString("0.###", CultureInfo.InvariantCulture);
            string key = $"wm:{opToken}";
            if (_extGStates.TryGetValue(key, out var existing))
                return existing.ResourceName;

            int objId = w.BeginObject();
            w.WriteLine("<< /Type /ExtGState");
            w.WriteLine($"   /CA {opToken}");
            w.WriteLine($"   /ca {opToken}");
            w.WriteLine("   /BM /Normal >>");
            w.EndObject();

            string resourceName = "/GSwm";
            if (_extGStates.Values.Any(h => string.Equals(h.ResourceName, resourceName, StringComparison.Ordinal)))
            {
                int suffix = 2;
                do
                {
                    resourceName = $"/GSwm{suffix}";
                    suffix++;
                } while (_extGStates.Values.Any(h => string.Equals(h.ResourceName, resourceName, StringComparison.Ordinal)));
            }

            var handle = new ExtGStateHandle(resourceName, objId);
            _extGStates[key] = handle;
            return handle.ResourceName;
        }

        // ---------- IMAGE WRITERS ----------

        // JPEG (incl. CMYK/YCCK + ICC)
        private (int imObj, int? smaskObj) WriteJpegXObject(PdfStreamWriter w, byte[] jpeg)
        {
            var info = JpegInspector.GetInfo(jpeg);

            // ColorSpace (prefer ICCBased when profile present)
            string colorSpaceEntry;
            int? iccObj = null;

            if (info.IccProfile != null && (info.Components == 1 || info.Components == 3 || info.Components == 4))
            {
                byte[] iccCompressed;
                using (var ms = new MemoryStream())
                {
                    using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                        z.Write(info.IccProfile, 0, info.IccProfile.Length);
                    iccCompressed = ms.ToArray();
                }

                iccObj = w.BeginObject();
                string alt = info.Components switch
                {
                    1 => "/DeviceGray",
                    3 => "/DeviceRGB",
                    4 => "/DeviceCMYK",
                    _ => "/DeviceRGB"
                };
                w.WriteLine("<< /N " + info.Components);
                w.WriteLine($"   /Alternate {alt}");
                w.WriteLine($"   /Length {iccCompressed.Length}");
                w.WriteLine("   /Filter /FlateDecode >>");
                w.WriteLine("stream");
                w.WriteBytes(iccCompressed);
                w.WriteLine("\nendstream");
                w.EndObject();

                colorSpaceEntry = $"[/ICCBased {iccObj} 0 R]";
            }
            else
            {
                colorSpaceEntry = info.Components switch
                {
                    1 => "/DeviceGray",
                    3 => "/DeviceRGB",
                    4 => "/DeviceCMYK",
                    _ => "/DeviceRGB"
                };
            }

            // Adobe APP14 transform: 2 => YCCK (invert + disable color transform)
            bool isYcck = (info.Components == 4 && info.HasAdobeMarker && info.AdobeTransform == 2);
            string? decodeArray = isYcck ? "[1 0 1 0 1 0 1 0]" : null;
            string? decodeParms = isYcck ? "<</ColorTransform 0>>" : null;

            int im = w.BeginObject();
            w.WriteLine("<< /Type /XObject /Subtype /Image");
            w.WriteLine($"   /Width {info.Width} /Height {info.Height}");
            w.WriteLine("   /BitsPerComponent 8");
            w.WriteLine($"   /ColorSpace {colorSpaceEntry}");
            w.WriteLine("   /Filter /DCTDecode");
            if (decodeParms != null) w.WriteLine($"   /DecodeParms {decodeParms}");
            if (decodeArray != null) w.WriteLine($"   /Decode {decodeArray}");
            w.WriteLine($"   /Length {jpeg.Length} >>");
            w.WriteLine("stream");
            w.WriteBytes(jpeg);
            w.WriteLine("\nendstream");
            w.EndObject();

            return (im, null);
        }

        // PNG (all types via PngDecoder; palette & alpha supported)
        private (int imObj, int? smaskObj) WritePngXObject(PdfStreamWriter w, byte[] png)
        {
            var dec = PngDecoder.Decode(png);

            // Palette object (for Indexed)
            int? paletteObj = null;
            if (dec.IsIndexed && dec.PaletteRGB != null)
            {
                paletteObj = w.BeginObject();
                w.WriteLine($"<< /Length {dec.PaletteRGB.Length} >>");
                w.WriteLine("stream");
                w.WriteBytes(dec.PaletteRGB);
                w.WriteLine("\nendstream");
                w.EndObject();
            }

            // Optional SMask (alpha)
            int? smask = null;
            if (dec.Alpha != null)
            {
                // Safety: only write SMask when alpha buffer matches dimensions and is not uniform 0 or 255
                bool validSize = dec.Alpha.Length == dec.Width * dec.Height;
                if (validSize)
                {
                    bool allZero = true, allFull = true;
                    for (int i = 0; i < dec.Alpha.Length; i++)
                    {
                        byte a = dec.Alpha[i];
                        if (a != 0) allZero = false;
                        if (a != 255) allFull = false;
                        if (!allZero && !allFull) break;
                    }

                    if (!allZero && !allFull)
                    {
                        bool alphaHasPredictor = dec.AlphaContainsFilterBytes;
                        byte[] alphaPayload = dec.Alpha;
                        if (!alphaHasPredictor && _options.UsePngPredictor)
                        {
                            alphaPayload = AddPngPredictorRows(dec.Alpha, dec.Width, 1, dec.Height);
                            alphaHasPredictor = true;
                        }
                        byte[] alphaFlated = PdfCompression.Flate(alphaPayload, _options.ImageCompressionLevel);
                        smask = w.BeginObject();
                        w.WriteLine("<< /Type /XObject /Subtype /Image");
                        w.WriteLine($"   /Width {dec.Width} /Height {dec.Height}");
                        w.WriteLine("   /ColorSpace /DeviceGray /BitsPerComponent 8");
                        w.WriteLine("   /Filter /FlateDecode");
                        w.WriteLine("   /Interpolate false");
                        if (alphaHasPredictor)
                            w.WriteLine($"   /DecodeParms <</Predictor 15 /Colors 1 /BitsPerComponent 8 /Columns {dec.Width}>>");
                        // SMask alpha: 0 = fully transparent, 1 = fully opaque
                        w.WriteLine("   /Decode [0 1]");
                        w.WriteLine($"   /Length {alphaFlated.Length} >>");
                        w.WriteLine("stream");
                        w.WriteBytes(alphaFlated);
                        w.WriteLine("\nendstream");
                        w.EndObject();
                    }
                }
            }

            // Main pixel data
            int colorsForPredictor = (dec.IsIndexed ? 1 : (dec.Components == 1 ? 1 : 3));
            bool pixelsHavePredictor = dec.PixelsContainFilterBytes;
            byte[] pixelPayload = dec.Pixels;
            if (!pixelsHavePredictor && _options.UsePngPredictor)
            {
                pixelPayload = AddPngPredictorRows(dec.Pixels, dec.Width, colorsForPredictor, dec.Height);
                pixelsHavePredictor = true;
            }
            byte[] mainFlated = PdfCompression.Flate(pixelPayload, _options.ImageCompressionLevel);

            string colorSpace;
            if (dec.IsIndexed && paletteObj.HasValue)
            {
                int maxIndex = (dec.PaletteRGB!.Length / 3) - 1;
                colorSpace = $"[/Indexed /DeviceRGB {maxIndex} {paletteObj.Value} 0 R]";
            }
            else
            {
                colorSpace = dec.Components == 1 ? "/DeviceGray" : "/DeviceRGB";
            }

            int im = w.BeginObject();
            w.WriteLine("<< /Type /XObject /Subtype /Image");
            w.WriteLine($"   /Width {dec.Width} /Height {dec.Height}");
            w.WriteLine($"   /ColorSpace {colorSpace} /BitsPerComponent {dec.BitsPerComponent}");
            w.WriteLine("   /Filter /FlateDecode");
            w.WriteLine("   /Interpolate false");
            if (pixelsHavePredictor)
                w.WriteLine($"   /DecodeParms <</Predictor 15 /Colors {colorsForPredictor} /BitsPerComponent {dec.BitsPerComponent} /Columns {dec.Width}>>");
            if (smask.HasValue) w.WriteLine($"   /SMask {smask.Value} 0 R");
            w.WriteLine($"   /Length {mainFlated.Length} >>");
            w.WriteLine("stream");
            w.WriteBytes(mainFlated);
            w.WriteLine("\nendstream");
            w.EndObject();

            return (im, smask);
        }

        // WebP fallback (the optimiser normally converts it to PNG or JPEG first).
        private (int imObj, int? smaskObj) WriteWebpXObject(PdfStreamWriter w, byte[] webp)
        {
            ImageInfo info = MediaImageDecoders.ReadInfo(webp, _renderLimits.MaximumImagePixels);
            using DecodedImage decoded = SkiaImageOptimiser.Decode(webp, info, null);
            return WriteRawRgbWithOptionalAlpha(w, decoded.Info.Width, decoded.Info.Height, decoded.Pixels, decoded.Alpha);
        }

        // Auto-detect and write
        private (int imObj, int? smaskObj) WriteImageAuto(PdfStreamWriter w, byte[] bytes)
        {
            if (JpegInspector.LooksLikeJpeg(bytes))
                return WriteJpegXObject(w, bytes);

            if (PngDecoder.LooksLikePng(bytes))
                return WritePngXObject(w, bytes);

            if (WebpInspector.LooksLikeWebp(bytes))
                return WriteWebpXObject(w, bytes);

            throw new InvalidDataException("Unsupported image format. PdfBuilder supports PNG, JPEG, and WebP.");
        }

        // Helper: write RGB + optional alpha as Flate-decoded XObject + SMask
        private (int imObj, int? smaskObj) WriteRawRgbWithOptionalAlpha(
            PdfStreamWriter w, int width, int height, byte[] rgb, byte[]? alpha)
        {
            // Main image (DeviceRGB, 8bpc) without PNG predictor framing (already raw pixels)
            int im = w.BeginObject();
            var deflated = PdfCompression.Flate(rgb, _options.ImageCompressionLevel);
            w.WriteLine("<< /Type /XObject /Subtype /Image");
            w.WriteLine($"   /Width {width} /Height {height}");
            w.WriteLine("   /ColorSpace /DeviceRGB /BitsPerComponent 8");
            w.WriteLine("   /Filter /FlateDecode");
            w.WriteLine("   /Interpolate false");
            w.WriteLine($"   /Length {deflated.Length} >>");
            w.WriteLine("stream");
            w.WriteBytes(deflated);
            w.WriteRaw("\nendstream\n");
            w.EndObject();

            int? smask = null;
            if (alpha != null && alpha.Length == width * height)
            {
                bool allZero = true, allFull = true;
                for (int i = 0; i < alpha.Length; i++)
                {
                    byte a = alpha[i];
                    if (a != 0) allZero = false;
                    if (a != 255) allFull = false;
                    if (!allZero && !allFull) break;
                }
                if (!allZero && !allFull)
                {
                    smask = w.BeginObject();
                    var aDef = PdfCompression.Flate(alpha, _options.ImageCompressionLevel);
                    w.WriteLine("<< /Type /XObject /Subtype /Image");
                    w.WriteLine($"   /Width {width} /Height {height}");
                    w.WriteLine("   /ColorSpace /DeviceGray /BitsPerComponent 8");
                    w.WriteLine("   /Filter /FlateDecode");
                    w.WriteLine("   /Interpolate false");
                    w.WriteLine("   /Decode [0 1]");
                    w.WriteLine($"   /Length {aDef.Length} >>");
                    w.WriteLine("stream");
                    w.WriteBytes(aDef);
                    w.WriteRaw("\nendstream\n");
                    w.EndObject();
                }
            }

            return (im, smask);
        }

        // ---------- UTILS ----------
        private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

        private static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 1f;
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        // Adds a one-byte PNG predictor flag (0=None) at start of each row
        private static byte[] AddPngPredictorRows(byte[] pixels, int width, int colors, int height)
        {
            int rowBytes = width * colors;
            if (pixels.Length != rowBytes * height)
                throw new InvalidDataException("Predictor framing mismatch: buffer size != width*colors*height");

            var dst = new byte[height * (rowBytes + 1)];
            int src = 0, d = 0;
            for (int y = 0; y < height; y++)
            {
                dst[d++] = 0; // filter type 0 (None)
                Buffer.BlockCopy(pixels, src, dst, d, rowBytes);
                src += rowBytes;
                d += rowBytes;
            }
            return dst;
        }

    }
}


