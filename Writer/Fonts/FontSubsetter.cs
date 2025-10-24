using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using PdfBuilder.Writer.Fonts;

namespace PdfBuilder.Writer
{
    internal static class FontSubsetter
    {
        private enum hb_memory_mode_t : uint
        {
            HB_MEMORY_MODE_DUPLICATE = 0,
            HB_MEMORY_MODE_READONLY,
            HB_MEMORY_MODE_WRITABLE,
            HB_MEMORY_MODE_READWRITE
        }

        private const uint HB_SUBSET_FLAGS_RETAIN_GIDS = 1u;

        public static bool TrySubset(byte[] fontData, IEnumerable<uint> glyphIds, IEnumerable<int> codePoints, string fontName, out byte[] subsetBytes)
        {
            subsetBytes = Array.Empty<byte>();

            void ReportFailure(string message)
            {
                FontDiagnostics.Report($"Font subset skipped for '{fontName}': {message}");
            }

            var glyphList = glyphIds?.Distinct().ToList() ?? new List<uint>();
            if (!glyphList.Contains(0))
                glyphList.Insert(0, 0);

            if (glyphList.Count == 0)
            {
                ReportFailure("glyph set empty");
                return false;
            }

            var unicodeList = codePoints?.Where(cp => cp > 0).Distinct().ToList() ?? new List<int>();

            if (fontData.Length == 0)
            {
                ReportFailure("font data is empty");
                return false;
            }

            var handle = GCHandle.Alloc(fontData, GCHandleType.Pinned);
            try
            {
                IntPtr blob = hb_blob_create(handle.AddrOfPinnedObject(), (uint)fontData.Length, hb_memory_mode_t.HB_MEMORY_MODE_READONLY, IntPtr.Zero, IntPtr.Zero);
                if (blob == IntPtr.Zero)
                {
                    ReportFailure("hb_blob_create returned null");
                    return false;
                }

                try
                {
                    IntPtr face = hb_face_create(blob, 0);
                    if (face == IntPtr.Zero)
                    {
                        ReportFailure("hb_face_create returned null");
                        return false;
                    }

                    try
                    {
                        IntPtr input = hb_subset_input_create_or_fail();
                        if (input == IntPtr.Zero)
                        {
                            ReportFailure("hb_subset_input_create_or_fail returned null");
                            return false;
                        }

                        try
                        {
                            hb_subset_input_set_flags(input, HB_SUBSET_FLAGS_RETAIN_GIDS);

                            IntPtr glyphSet = hb_subset_input_glyph_set(input);
                            if (glyphSet == IntPtr.Zero)
                            {
                                ReportFailure("glyph set handle was null");
                                return false;
                            }

                            foreach (var glyphId in glyphList)
                                hb_set_add(glyphSet, glyphId);

                            IntPtr unicodeSet = hb_subset_input_unicode_set(input);
                            if (unicodeSet != IntPtr.Zero)
                            {
                                foreach (var codePoint in unicodeList)
                                    hb_set_add(unicodeSet, (uint)codePoint);
                            }

                            IntPtr subsetFace = hb_subset_or_fail(face, input);
                            if (subsetFace == IntPtr.Zero)
                            {
                                ReportFailure("hb_subset_or_fail returned null");
                                return false;
                            }

                            try
                            {
                                IntPtr subsetBlob = hb_face_reference_blob(subsetFace);
                                if (subsetBlob == IntPtr.Zero)
                                {
                                    ReportFailure("subset face blob was null");
                                    return false;
                                }

                                try
                                {
                                    uint length = hb_blob_get_length(subsetBlob);
                                    if (length == 0)
                                    {
                                        ReportFailure("subset blob length was zero");
                                        return false;
                                    }

                                    IntPtr dataPtr = hb_blob_get_data(subsetBlob, out uint actualLength);
                                    if (dataPtr == IntPtr.Zero || actualLength == 0)
                                    {
                                        ReportFailure("subset blob data pointer invalid");
                                        return false;
                                    }

                                    subsetBytes = new byte[actualLength];
                                    Marshal.Copy(dataPtr, subsetBytes, 0, (int)actualLength);
                                    return true;
                                }
                                finally
                                {
                                    hb_blob_destroy(subsetBlob);
                                }
                            }
                            finally
                            {
                                hb_face_destroy(subsetFace);
                            }
                        }
                        finally
                        {
                            hb_subset_input_destroy(input);
                        }
                    }
                    finally
                    {
                        hb_face_destroy(face);
                    }
                }
                finally
                {
                    hb_blob_destroy(blob);
                }
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_blob_create(IntPtr data, uint length, hb_memory_mode_t mode, IntPtr userData, IntPtr destroy);

        [DllImport("libHarfBuzzSharp")]
        private static extern void hb_blob_destroy(IntPtr blob);

        [DllImport("libHarfBuzzSharp")]
        private static extern uint hb_blob_get_length(IntPtr blob);

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_blob_get_data(IntPtr blob, out uint length);

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_face_create(IntPtr blob, uint index);

        [DllImport("libHarfBuzzSharp")]
        private static extern void hb_face_destroy(IntPtr face);

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_face_reference_blob(IntPtr face);

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_subset_input_create_or_fail();

        [DllImport("libHarfBuzzSharp")]
        private static extern void hb_subset_input_destroy(IntPtr input);

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_subset_input_glyph_set(IntPtr input);

        [DllImport("libHarfBuzzSharp")]
        private static extern void hb_subset_input_set_flags(IntPtr input, uint flags);

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_subset_input_unicode_set(IntPtr input);

        [DllImport("libHarfBuzzSharp")]
        private static extern void hb_set_add(IntPtr set, uint value);

        [DllImport("libHarfBuzzSharp")]
        private static extern IntPtr hb_subset_or_fail(IntPtr face, IntPtr input);
    }
}
