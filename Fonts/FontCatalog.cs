using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfBuilder.Writer.Fonts;
using SkiaSharp;

namespace PdfBuilder.Fonts
{
    /// <summary>
    /// Allows registering additional font faces that can be referenced by name in documents.
    /// </summary>
    public static class FontCatalog
    {
        private sealed class Entry
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

        /// <summary>
        /// Registers the specified font file and associates it with optional aliases.
        /// </summary>
        public static void RegisterFile(string path, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
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
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", searchOption))
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
            }
        }

        internal static SKTypeface? Resolve(string family, SKFontStyle style)
        {
            lock (_sync)
            {
                return _entries.FirstOrDefault(e => e.Matches(family, style))?.Typeface;
            }
        }

        internal static IEnumerable<SKTypeface> EnumerateRegisteredTypefaces()
        {
            lock (_sync)
            {
                return _entries.Select(e => e.Typeface).Distinct().ToList();
            }
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
}
