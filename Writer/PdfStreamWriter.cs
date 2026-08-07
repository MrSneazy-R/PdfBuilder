using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfBuilder.Writer
{
    /// <summary>
    /// Low-level PDF writer that tracks object offsets, writes xref/trailer,
    /// and provides helpers for writing text and binary content streams.
    /// </summary>
    public sealed class PdfStreamWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly List<long> _offsets = new(); // offsets for 1..N (0 is reserved free entry)
        private int _objectCount = 0;
        private bool _inObject = false;
        private bool _disposed = false;

        public PdfStreamWriter(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));
        }

        /// <summary>Writes the PDF header, e.g. %PDF-1.4</summary>
        public void WriteHeader(string version = "1.4")
        {
            WriteLine($"%PDF-{version}");
            // Write a binary marker comment to signal binary content may follow (recommended)
            // Contains at least one byte with value >= 128
            WriteRaw("%\xE2\xE3\xCF\xD3\n");
        }

        /// <summary>Begin a new PDF object. Returns the object number.</summary>
        public int BeginObject()
        {
            EnsureNotDisposed();
            if (_inObject) throw new InvalidOperationException("Already inside an object.");

            _objectCount++;
            _offsets.Add(_stream.Position); // store exact byte offset where "n 0 obj" is written
            WriteLine($"{_objectCount} 0 obj");
            _inObject = true;
            return _objectCount;
        }

        /// <summary>Ends the current PDF object.</summary>
        public void EndObject()
        {
            EnsureNotDisposed();
            if (!_inObject) throw new InvalidOperationException("Not inside an object.");
            WriteLine("endobj");
            _inObject = false;
        }

        /// <summary>Writes a content stream object with the given data. Creates the dictionary with /Length automatically.</summary>
        public void WriteInlineStream(byte[] data) => WriteStream(data);

        /// <summary>
        /// Writes a stream with optional additional dictionary entries (e.g. /Filter, /Length1).
        /// </summary>
        public void WriteStream(byte[]? data, params (string Key, string Value)[] extraEntries)
        {
            EnsureNotDisposed();
            data ??= Array.Empty<byte>();

            var sb = new StringBuilder();
            sb.Append("<< /Length ");
            sb.Append(data.Length);

            if (extraEntries != null)
            {
                foreach (var (keyRaw, value) in extraEntries)
                {
                    if (string.IsNullOrWhiteSpace(keyRaw) || string.IsNullOrWhiteSpace(value))
                        continue;

                    string key = PdfNameEncoder.Encode(keyRaw.Trim());

                    sb.Append(' ');
                    sb.Append(key);
                    sb.Append(' ');
                    sb.Append(value);
                }
            }

            sb.Append(" >>");
            WriteLine(sb.ToString());
            WriteLine("stream");
            WriteBytes(data);
            WriteRaw("\nendstream\n");
        }

        /// <summary>
        /// Writes raw string data to the stream (no newline added).
        /// Uses ASCII encoding for determinism (PDF syntax is ASCII-friendly).
        /// </summary>
        public void WriteRaw(string data)
        {
            EnsureNotDisposed();
            if (string.IsNullOrEmpty(data)) return;
            var bytes = Encoding.Latin1.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Writes a line (raw + newline).</summary>
        public void WriteLine(string line)
        {
            EnsureNotDisposed();
            WriteRaw(line);
            WriteRaw("\n");
        }

        /// <summary>Writes raw bytes exactly as provided.</summary>
        public void WriteBytes(byte[] bytes)
        {
            EnsureNotDisposed();
            if (bytes is { Length: > 0 })
                _stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Write the xref table and trailer. Pass the root /Catalog object id.
        /// Optionally include an /Info dictionary object id.
        /// </summary>
        public void WriteXRefAndTrailer(int rootObjectId, int? infoObjectId = null, string? documentIdHex = null)
        {
            EnsureNotDisposed();
            if (_inObject) throw new InvalidOperationException("Cannot write xref while inside an object.");

            long xrefPos = _stream.Position;

            // xref header
            WriteLine("xref");
            // include object 0 (free) plus 1.._objectCount
            WriteLine($"0 {_objectCount + 1}");
            // free object 0 entry
            WriteLine("0000000000 65535 f ");

            // Each offset corresponds to object 1..N
            for (int i = 0; i < _offsets.Count; i++)
            {
                long off = _offsets[i];
                // 10-digit, leading zeros, then " 00000 n "
                WriteLine($"{off:D10} 00000 n ");
            }

            // trailer
            WriteLine("trailer");
            WriteLine("<<");
            WriteLine($"/Size {_objectCount + 1}");
            WriteLine($"/Root {rootObjectId} 0 R");
            if (infoObjectId.HasValue)
                WriteLine($"/Info {infoObjectId.Value} 0 R");
            if (!string.IsNullOrWhiteSpace(documentIdHex))
                WriteLine($"/ID [<{documentIdHex}> <{documentIdHex}>]");
            WriteLine(">>");
            WriteLine("startxref");
            WriteLine(xrefPos.ToString());
            WriteLine("%%EOF");
        }

        /// <summary>Flush the underlying stream.</summary>
        public void Flush()
        {
            EnsureNotDisposed();
            _stream.Flush();
        }

        /// <summary>If the underlying stream is a MemoryStream, saves its content to a file.</summary>
        public void SaveToFile(string path)
        {
            EnsureNotDisposed();
            if (_stream is MemoryStream ms)
            {
                File.WriteAllBytes(path, ms.ToArray());
            }
            else
            {
                throw new InvalidOperationException("Underlying stream is not a MemoryStream.");
            }
        }

        public int ObjectCount => _objectCount;
        public long Position => _stream.Position;
        public IReadOnlyList<long> GetOffsets() => _offsets.AsReadOnly();

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PdfStreamWriter));
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { _stream.Flush(); } catch { /* ignore */ }
            _disposed = true;
        }
    }
}
