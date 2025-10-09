// --- PdfResourceManager.cs ---
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PdfBuilder.Elements;
using PdfBuilder.Writer.Imaging;

namespace PdfBuilder.Writer
{
    /// <summary>
    /// Tracks and writes shared PDF resources (fonts, images, extgstates).
    /// Supports PNG (all types via PngDecoder), JPEG (incl. CMYK/YCCK + ICC),
    /// and WebP (decoded to RGB + optional SMask via WebpWicDecoder on Windows).
    /// No third-party libs — only System.* (plus OS WIC for WebP).
    /// </summary>
    public class PdfResourceManager
    {
        // Legacy maps (back-compat with any old call sites)
        private readonly Dictionary<string, int> _fonts = new();
        private readonly Dictionary<string, int> _imagesLegacy = new();
        private readonly Dictionary<float, int> _opacityStatesLegacy = new();

        // New image map: key → (main image object, optional SMask object)
        private readonly Dictionary<string, (int imObj, int? smaskObj)> _imageMap = new();

        // Opacity (ExtGState): opacity → object id
        private readonly Dictionary<float, int> _extGStates = new();

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
        /// (image obj id, optional SMask obj id, optional ExtGState obj id, pdf resource name)
        /// </summary>
        public (int imageObjId, int? smaskObjId, int? extGStateObjId, string pdfName)
            EnsureImageXObject(PdfStreamWriter w, ImageElement img)
        {
            if (img == null) throw new ArgumentNullException(nameof(img));
            if (img.ImageData == null || img.ImageData.Length == 0)
                throw new InvalidDataException("ImageElement.ImageData is empty.");

            string key = !string.IsNullOrWhiteSpace(img.ImageId)
                ? img.ImageId!
                : Hash(img.ImageData);

            if (!_imageMap.TryGetValue(key, out var ids))
            {
                // Single auto path handles JPEG/PNG/WebP
                ids = WriteImageAuto(w, img.ImageData);
                _imageMap[key] = ids;
            }

            // Optional overall opacity (separate from per-pixel alpha)
            int? gsObj = null;
            float op = Clamp01(img.Opacity);
            if (op < 0.999f)
            {
                if (!_extGStates.TryGetValue(op, out var eid))
                {
                    eid = w.BeginObject();
                    w.WriteLine("<< /Type /ExtGState");
                    w.WriteLine($"   /CA {op:0.###} /ca {op:0.###} >>");
                    w.EndObject();
                    _extGStates[op] = eid;
                }
                gsObj = _extGStates[op];
            }

            string name = $"/Im{ids.imObj}";
            img.PdfResourceName = name; // optional debug
            return (ids.imObj, ids.smaskObj, gsObj, name);
        }

        /// <summary>Build /XObject entries, e.g. "/Im5 5 0 R /Im9 9 0 R"</summary>
        public string BuildXObjectResources()
        {
            if (_imageMap.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var kv in _imageMap)
            {
                int id = kv.Value.imObj;
                sb.Append($"/Im{id} {id} 0 R ");
            }
            return sb.ToString();
        }

        /// <summary>Build /ExtGState entries, e.g. "/GS7 7 0 R"</summary>
        public string BuildExtGStateResources()
        {
            if (_extGStates.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var kv in _extGStates)
            {
                int id = kv.Value;
                sb.Append($"/GS{id} {id} 0 R ");
            }
            return sb.ToString();
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
                    using (var z = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
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
                byte[] alphaFlated = Flate(dec.Alpha);
                smask = w.BeginObject();
                w.WriteLine("<< /Type /XObject /Subtype /Image");
                w.WriteLine($"   /Width {dec.Width} /Height {dec.Height}");
                w.WriteLine("   /ColorSpace /DeviceGray /BitsPerComponent 8");
                w.WriteLine("   /Filter /FlateDecode");
                w.WriteLine($"   /Length {alphaFlated.Length} >>");
                w.WriteLine("stream");
                w.WriteBytes(alphaFlated);
                w.WriteLine("\nendstream");
                w.EndObject();
            }

            // Main pixel data
            byte[] mainFlated = Flate(dec.Pixels);

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
            if (smask.HasValue) w.WriteLine($"   /SMask {smask.Value} 0 R");
            w.WriteLine($"   /Length {mainFlated.Length} >>");
            w.WriteLine("stream");
            w.WriteBytes(mainFlated);
            w.WriteLine("\nendstream");
            w.EndObject();

            return (im, smask);
        }

        // WebP (decode to RGB + optional alpha via WIC; write Flate + /SMask)
        private (int imObj, int? smaskObj) WriteWebpXObject(PdfStreamWriter w, byte[] webp)
        {
            var info = WebpInspector.GetInfo(webp);
            if (info.Animated) throw new InvalidDataException("Animated WebP is not supported.");

            var wp = WebpWicDecoder.Decode(webp);
            return WriteRawRgbWithOptionalAlpha(w, wp.Width, wp.Height, wp.Rgb, wp.Alpha);
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

            throw new InvalidDataException("Unsupported image format (expect JPEG/PNG/WebP).");
        }

        // Helper: write RGB + optional alpha as Flate-decoded XObject + SMask
        private (int imObj, int? smaskObj) WriteRawRgbWithOptionalAlpha(
            PdfStreamWriter w, int width, int height, byte[] rgb, byte[]? alpha)
        {
            // Main image (DeviceRGB, 8bpc)
            int im = w.BeginObject();
            var deflated = Deflate(rgb);
            w.WriteLine("<< /Type /XObject /Subtype /Image");
            w.WriteLine($"   /Width {width} /Height {height}");
            w.WriteLine("   /ColorSpace /DeviceRGB /BitsPerComponent 8");
            w.WriteLine("   /Filter /FlateDecode");
            w.WriteLine($"   /Length {deflated.Length} >>");
            w.WriteLine("stream");
            w.WriteBytes(deflated);
            w.WriteRaw("\nendstream\n");
            w.EndObject();

            int? smask = null;
            if (alpha != null)
            {
                smask = w.BeginObject();
                var aDef = Deflate(alpha);
                w.WriteLine("<< /Type /XObject /Subtype /Image");
                w.WriteLine($"   /Width {width} /Height {height}");
                w.WriteLine("   /ColorSpace /DeviceGray /BitsPerComponent 8");
                w.WriteLine("   /Filter /FlateDecode");
                w.WriteLine("   /Decode [0 1]");
                w.WriteLine($"   /Length {aDef.Length} >>");
                w.WriteLine("stream");
                w.WriteBytes(aDef);
                w.WriteRaw("\nendstream\n");
                w.EndObject();
            }

            return (im, smask);
        }

        // ---------- UTILS ----------
        private static string Hash(byte[] data) => Convert.ToHexString(SHA1.HashData(data));

        private static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 1f;
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        private static byte[] Flate(byte[] raw)
        {
            using var ms = new MemoryStream();
            using (var z = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
                z.Write(raw, 0, raw.Length);
            return ms.ToArray();
        }

        // Replace the existing Deflate(...) in PdfResourceManager.cs
        private static byte[] Deflate(byte[] raw)
        {
            using var ms = new MemoryStream();
#if NET6_0_OR_GREATER || NETSTANDARD2_0 || NET5_0_OR_GREATER || NETCOREAPP3_1
            using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
#else
    // If your target framework doesn't have CompressionLevel overloads,
    // fall back to standard "Compress" mode (still fine).
    using (var ds = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
#endif
            {
                ds.Write(raw, 0, raw.Length);
            }
            return ms.ToArray();
        }

    }
}
