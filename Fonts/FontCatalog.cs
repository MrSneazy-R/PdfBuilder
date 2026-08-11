using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PdfBuilder.Writer.Fonts;
using SkiaSharp;

namespace PdfBuilder.Fonts
{
    /// <summary>
    /// Allows registering additional font faces that can be referenced by name in documents.
    /// </summary>
    public static class FontCatalog
    {
        internal sealed class Entry
        {
            public Entry(string alias, SKTypeface typeface)
            {
                Alias = alias;
                Typeface = typeface;
                Style = typeface.FontStyle;
                Weight = typeface.FontWeight;
            }

            public string Alias { get; }
            public SKTypeface Typeface { get; }
            public SKFontStyle Style { get; }
            public int Weight { get; }

            public bool Matches(string alias, SKFontStyle style)
                => string.Equals(Alias, alias, StringComparison.OrdinalIgnoreCase)
                   && Style.Slant == style.Slant
                   && Style.Width == style.Width
                   && Weight == style.Weight;
        }

        private static readonly object _sync = new();
        private static readonly List<Entry> _entries = new();
        private static readonly List<string> _fallbackFonts = new();
        private static readonly AsyncLocal<FontCatalogSnapshot?> _activeSnapshot = new();
        private static int _version;
        private static bool _strictMatching;
        private static long _maximumFontFileBytes = 64L * 1024 * 1024;

        /// <summary>Gets or sets whether unresolved families or glyphs throw a <see cref="FontNotFoundException"/>.</summary>
        public static bool StrictMatching
        {
            get { lock (_sync) return _strictMatching; }
            set
            {
                lock (_sync)
                {
                    if (_strictMatching == value) return;
                    _strictMatching = value;
                    Interlocked.Increment(ref _version);
                }
            }
        }

        /// <summary>Gets or sets the maximum accepted size of one registered font file or stream.</summary>
        public static long MaximumFontFileBytes
        {
            get { lock (_sync) return _maximumFontFileBytes; }
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                lock (_sync) _maximumFontFileBytes = value;
            }
        }

        /// <summary>Gets the deterministic global fallback chain.</summary>
        public static IReadOnlyList<string> FallbackFonts { get { lock (_sync) return _fallbackFonts.ToArray(); } }

        /// <summary>Replaces the deterministic fallback chain used when a style does not provide one.</summary>
        public static void SetFallbackFonts(params string[] fontFamilies)
        {
            if (fontFamilies == null) throw new ArgumentNullException(nameof(fontFamilies));
            lock (_sync)
            {
                _fallbackFonts.Clear();
                _fallbackFonts.AddRange(fontFamilies.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>Captures an immutable, versioned view suitable for one concurrent document generation.</summary>
        public static FontCatalogSnapshot CaptureSnapshot()
        {
            lock (_sync)
                return new FontCatalogSnapshot(_version, _strictMatching,
                    Array.AsReadOnly(_fallbackFonts.ToArray()), Array.AsReadOnly(_entries.ToArray()));
        }

        internal static FontCatalogSnapshot CurrentSnapshot => _activeSnapshot.Value ?? CaptureSnapshot();
        internal static IDisposable EnterSnapshot(FontCatalogSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var previous = _activeSnapshot.Value;
            _activeSnapshot.Value = snapshot;
            return new SnapshotScope(previous);
        }

        /// <summary>Registers a font from an in-memory byte array.</summary>
        public static void RegisterFont(byte[] data, params string[] aliases)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ValidateSize(data.LongLength, nameof(data));
            using var fontData = SKData.CreateCopy(data);
            var typeface = SKTypeface.FromData(fontData) ?? throw new InvalidOperationException("Unable to load font data.");
            RegisterTypeface(typeface, aliases);
        }

        /// <summary>Registers a font from a readable stream without taking ownership of the stream.</summary>
        public static void RegisterFont(Stream stream, params string[] aliases)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Font stream must be readable.", nameof(stream));
            if (stream.CanSeek) ValidateSize(stream.Length - stream.Position, nameof(stream));
            using var buffer = new MemoryStream();
            var block = new byte[81920];
            int read;
            long total = 0;
            while ((read = stream.Read(block, 0, block.Length)) > 0)
            {
                total += read;
                ValidateSize(total, nameof(stream));
                buffer.Write(block, 0, read);
            }
            RegisterFont(buffer.ToArray(), aliases);
        }

        /// <summary>Registers a local font file.</summary>
        public static void RegisterFontFile(string path, params string[] aliases) => RegisterFile(path, aliases);

        /// <summary>Registers supported font files from a local directory in ordinal path order.</summary>
        public static void RegisterFontDirectory(string path, SearchOption searchOption = SearchOption.AllDirectories) => RegisterFolder(path, searchOption);

        /// <summary>
        /// Registers the specified font file and associates it with optional aliases.
        /// </summary>
        public static void RegisterFile(string path, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("The font file was not found.", path);
            ValidateSize(new FileInfo(path).Length, nameof(path));
            var typeface = SKTypeface.FromFile(path) ?? throw new InvalidOperationException($"Unable to load font file '{path}'.");
            RegisterTypeface(typeface, aliases);
        }

        /// <summary>
        /// Registers all font files found in the specified directory.
        /// </summary>
        /// <param name="directory">Directory containing fonts.</param>
        /// <param name="searchOption">Search option for enumeration.</param>
        public static void RegisterFolder(string directory, SearchOption searchOption = SearchOption.AllDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Font directory '{directory}' was not found.");

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf", ".ttc", ".otc" };
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", searchOption).OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!allowed.Contains(Path.GetExtension(file)))
                    continue;

                try
                {
                    RegisterFontFile(file);
                }
                catch (Exception ex)
                {
                    FontDiagnostics.Report($"Failed to register font '{file}': {ex.Message}");
                }
            }
        }

        private static void RegisterFontFile(string path)
        {
            ValidateSize(new FileInfo(path).Length, nameof(path));
            string extension = Path.GetExtension(path);
            if (extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".otc", StringComparison.OrdinalIgnoreCase))
            {
                int index = 0;
                while (true)
                {
                    var typeface = SKTypeface.FromFile(path, index);
                    if (typeface == null)
                        break;

                    RegisterTypeface(typeface, typeface.FamilyName);
                    index++;
                }
                if (index == 0)
                    throw new InvalidOperationException("No faces found in collection.");
            }
            else
            {
                var typeface = SKTypeface.FromFile(path);
                if (typeface == null)
                    throw new InvalidOperationException("Unable to load font.");

                RegisterTypeface(typeface, typeface.FamilyName);
            }
        }

        /// <summary>
        /// Registers common system font directories for the current platform.
        /// </summary>
        public static void RegisterSystemFonts()
        {
            foreach (var directory in GetSystemFontDirectories())
            {
                try
                {
                    RegisterFolder(directory, SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    FontDiagnostics.Report($"Failed to register system font directory '{directory}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Registers an existing <see cref="SKTypeface"/> instance and associates it with optional aliases.
        /// </summary>
        public static void RegisterTypeface(SKTypeface typeface, params string[] aliases)
        {
            if (typeface == null) throw new ArgumentNullException(nameof(typeface));

            var aliasList = (aliases != null && aliases.Length > 0)
                ? aliases.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray()
                : new[] { typeface.FamilyName };

            lock (_sync)
            {
                foreach (var alias in aliasList)
                {
                    // Remove existing registration for the same alias/style to avoid duplicates.
                    _entries.RemoveAll(e => e.Matches(alias, typeface.FontStyle));
                    _entries.Add(new Entry(alias, typeface));
                }
                Interlocked.Increment(ref _version);
            }
        }

        internal static SKTypeface? Resolve(string family, SKFontStyle style)
        {
            return CurrentSnapshot.Resolve(family, style);
        }

        internal static int Version => CurrentSnapshot.Version;
        internal static bool IsStrictMatching => CurrentSnapshot.StrictMatching;
        internal static IReadOnlyList<string> GetFallbackFonts() => CurrentSnapshot.FallbackFonts;

        internal static IEnumerable<SKTypeface> EnumerateRegisteredTypefaces()
        {
            return CurrentSnapshot.Entries.Select(e => e.Typeface).Distinct().ToArray();
        }

        private static void ValidateSize(long length, string parameterName)
        {
            if (length <= 0) throw new ArgumentException("Font data cannot be empty.", parameterName);
            if (length > MaximumFontFileBytes) throw new ArgumentOutOfRangeException(parameterName, $"Font data exceeds the configured {MaximumFontFileBytes} byte limit.");
        }

        private sealed class SnapshotScope : IDisposable
        {
            private readonly FontCatalogSnapshot? _previous;
            private bool _disposed;
            public SnapshotScope(FontCatalogSnapshot? previous) => _previous = previous;
            public void Dispose() { if (_disposed) return; _activeSnapshot.Value = _previous; _disposed = true; }
        }

        private static IEnumerable<string> GetSystemFontDirectories()
        {
            if (OperatingSystem.IsWindows())
            {
                var windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                if (!string.IsNullOrWhiteSpace(windowsFonts))
                    yield return windowsFonts;
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/System/Library/Fonts";
                yield return "/Library/Fonts";
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts");
            }
            else if (OperatingSystem.IsLinux())
            {
                yield return "/usr/share/fonts";
                yield return "/usr/local/share/fonts";
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts");
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "fonts");
            }
        }
    }

    /// <summary>An immutable versioned view of registered fonts and fallback policy.</summary>
    public sealed class FontCatalogSnapshot
    {
        internal FontCatalogSnapshot(int version, bool strictMatching, IReadOnlyList<string> fallbackFonts, IReadOnlyList<FontCatalog.Entry> entries)
        {
            Version = version;
            StrictMatching = strictMatching;
            FallbackFonts = fallbackFonts;
            Entries = entries;
        }
        /// <summary>Gets the catalogue version included in this snapshot.</summary>
        public int Version { get; }
        /// <summary>Gets whether strict matching was enabled when captured.</summary>
        public bool StrictMatching { get; }
        /// <summary>Gets the immutable ordered fallback chain.</summary>
        public IReadOnlyList<string> FallbackFonts { get; }
        internal IReadOnlyList<FontCatalog.Entry> Entries { get; }
        internal SKTypeface? Resolve(string family, SKFontStyle style) => Entries.FirstOrDefault(entry => entry.Matches(family, style))?.Typeface;
    }
}
