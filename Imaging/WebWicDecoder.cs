// --- Imaging/WebpWicDecoder.cs ---
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace PdfBuilder.Writer.Imaging
{
    /// <summary>
    /// WebP decode via Windows WIC (no third-party libs). On non-Windows platforms,
    /// this API throws PlatformNotSupportedException. We detect OS at runtime.
    /// Converts to BGRA32 via IWICFormatConverter, then splits into RGB + A.
    /// </summary>
    public static class WebpWicDecoder
    {
        // CLSID_WICImagingFactory (v1 and v2). We'll try v2 then fallback to v1.  :contentReference[oaicite:4]{index=4}
        private static readonly Guid CLSID_WICImagingFactory2 = new("317d06e8-5f24-433d-bdf7-79ce68d8abc2");
        private static readonly Guid CLSID_WICImagingFactory = new("cacaf262-9370-4615-a13b-9f5539da4c0a");

        // GUID_WICPixelFormat32bppBGRA  :contentReference[oaicite:5]{index=5}
        private static readonly Guid GUID_WICPixelFormat32bppBGRA = new("6fddc324-4e03-4bfe-b185-3d77768dc90f");

        // SHCreateMemStream for IStream over in-memory bytes
        [DllImport("shlwapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SHCreateMemStream(byte[] data, uint cbData);

        public sealed class Result
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public byte[] Rgb { get; init; } = Array.Empty<byte>();   // packed RGBRGB...
            public byte[]? Alpha { get; init; }
        }

        public static Result Decode(byte[] webpBytes)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("WebP decode needs WIC (Windows-only).");

            if (webpBytes == null || webpBytes.Length < 12) throw new InvalidDataException("Empty WebP");

            // create COM stream
            IntPtr p = SHCreateMemStream(webpBytes, (uint)webpBytes.Length);
            if (p == IntPtr.Zero) throw new IOException("SHCreateMemStream failed.");
            var stream = (IStream)Marshal.GetObjectForIUnknown(p);

            object facObj = TryCreateFactory(CLSID_WICImagingFactory2) ?? TryCreateFactory(CLSID_WICImagingFactory)
                            ?? throw new PlatformNotSupportedException("WIC factory not available.");

            var factory = (IWICImagingFactory)facObj;

            IWICBitmapDecoder decoder = null!;
            IWICBitmapFrameDecode frame = null!;
            IWICFormatConverter conv = null!;
            try
            {
                Guid vendor = Guid.Empty;
                // WICDecodeMetadataCacheOnLoad = 0x1
                factory.CreateDecoderFromStream(stream, ref vendor, 0x1, out decoder);
                decoder.GetFrame(0, out frame);

                // Convert to 32bpp BGRA
                factory.CreateFormatConverter(out conv);
                var fmt = GUID_WICPixelFormat32bppBGRA;
                conv.Initialize((IWICBitmapSource)frame, ref fmt,
                                WICBitmapDitherType.WICBitmapDitherTypeNone,
                                IntPtr.Zero, 0.0,
                                WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

                // Pull pixels
                conv.GetSize(out uint w, out uint h);
                int width = (int)w, height = (int)h;
                int stride = width * 4;
                var bgra = new byte[stride * height];
                conv.CopyPixels(IntPtr.Zero, (uint)stride, (uint)bgra.Length, bgra);

                // Split BGRA -> RGB + A
                var rgb = new byte[width * height * 3];
                byte[]? alpha = null;
                bool anyAlpha = false;

                for (int y = 0, di = 0, si = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++, si += 4)
                    {
                        byte b = bgra[si + 0];
                        byte g = bgra[si + 1];
                        byte r = bgra[si + 2];
                        byte a = bgra[si + 3];
                        rgb[di++] = r; rgb[di++] = g; rgb[di++] = b;
                        if (a != 255) anyAlpha = true;
                    }
                }
                if (anyAlpha)
                {
                    alpha = new byte[width * height];
                    for (int y = 0, pi = 0, si2 = 3; y < height; y++)
                        for (int x = 0; x < width; x++, si2 += 4)
                            alpha[pi++] = bgra[si2];
                }

                return new Result { Width = width, Height = height, Rgb = rgb, Alpha = alpha };
            }
            finally
            {
                if (conv != null) Marshal.ReleaseComObject(conv);
                if (frame != null) Marshal.ReleaseComObject(frame);
                if (decoder != null) Marshal.ReleaseComObject(decoder);
                Marshal.ReleaseComObject(stream);
                Marshal.Release(p);
            }
        }

        private static object? TryCreateFactory(Guid clsid)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            Type? type = Type.GetTypeFromCLSID(clsid);
            if (type == null)
                return null;

            try { return Activator.CreateInstance(type); }
            catch { return null; }
        }

        #region Minimal COM interop

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70")]               // IID_IWICImagingFactory  :contentReference[oaicite:6]{index=6}
        private interface IWICImagingFactory
        {
            // vtbl slots trimmed to only what we use:
            void _VtblGap1_3(); // CreateDecoderFromFilename, CreateDecoderFromStream is 4th
            void CreateDecoderFromStream([In] IStream pIStream,
                                         [In] ref Guid pguidVendor,
                                         [In] int metadataOptions,
                                         [Out] out IWICBitmapDecoder ppIDecoder);
            void _VtblGap2_2(); // CreateEncoder, CreateComponentInfo
            void CreateDecoder([In] ref Guid guidContainerFormat, [In] ref Guid pguidVendor, out IntPtr ppIDecoder); // unused
            void CreatePalette(out IntPtr ppIPalette); // unused
            void CreateFormatConverter([Out] out IWICFormatConverter ppIFormatConverter);
            // (rest omitted)
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("9EDDE9E7-8DEE-47ea-99DF-E6FAF2ED44BF")]
        private interface IWICBitmapDecoder
        {
            void QueryCapability([In] IStream pIStream, out uint pdwCapability); // unused
            void Initialize([In] IStream pIStream, int cacheOptions); // unused
            void GetContainerFormat(out Guid pguidContainerFormat);
            void GetDecoderInfo(out IntPtr ppIDecoderInfo); // unused
            void CopyPalette(IntPtr pIPalette); // unused
            void GetMetadataQueryReader(out IntPtr ppIMetadataQueryReader); // unused
            void GetPreview(out IntPtr ppIBitmapSource); // unused
            void GetColorContexts(int cCount, IntPtr ppIColorContexts, out int pcActualCount); // unused
            void GetThumbnail(out IntPtr ppIThumbnail); // unused
            void GetFrameCount(out int pCount); // unused
            void GetFrame(int index, out IWICBitmapFrameDecode ppIBitmapFrame);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("3B16811B-6A43-4ec9-A813-3D930C13B940")]
        private interface IWICBitmapSource
        {
            void GetSize(out uint puiWidth, out uint puiHeight);
            void GetPixelFormat(out Guid pPixelFormat);
            void GetResolution(out double pDpmX, out double pDpmY);
            void CopyPalette(IntPtr pIPalette);
            void CopyPixels(IntPtr prc, uint cbStride, uint cbBufferSize, [Out] byte[] pbBuffer);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("E8EDA601-3D48-431a-AB44-69059BE88BBE")]
        private interface IWICBitmapFrameDecode : IWICBitmapSource
        { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("B3B9DDE0-F09F-4e1e-AF1C-B676769FAD7A")]
        private interface IWICFormatConverter : IWICBitmapSource
        {
            void Initialize([In] IWICBitmapSource pISource,
                            [In] ref Guid dstFormat,
                            [In] WICBitmapDitherType dither,
                            [In] IntPtr pIPalette,
                            [In] double alphaThresholdPercent,
                            [In] WICBitmapPaletteType paletteTranslate);
            void CanConvert([In] ref Guid srcPixelFormat, [In] ref Guid dstPixelFormat, out int pfCanConvert);
        }

        private enum WICBitmapDitherType { WICBitmapDitherTypeNone = 0 }
        private enum WICBitmapPaletteType { WICBitmapPaletteTypeCustom = 0 }

        #endregion

    }
}


