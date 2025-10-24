using System;
using System.IO;
using System.IO.Compression;

namespace PdfBuilder.Writer
{
    /// <summary>
    /// Shared Flate compression helpers for PDF streams.
    /// </summary>
    internal static class PdfCompression
    {
        /// <summary>
        /// Compresses the supplied data with zlib (Flate) using the requested compression level.
        /// Returns an empty array when input is null or empty.
        /// </summary>
        public static byte[] Flate(byte[]? data, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, level, leaveOpen: true))
            {
                z.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }
}
