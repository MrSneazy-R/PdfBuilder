using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PdfBuilder.Document.Layout
{
    public sealed class LayoutProfilerConfig
    {
        public bool Enabled { get; set; }
        public string? OutputPath { get; set; }
        public Action<LayoutProfileSnapshot>? OnCompleted { get; set; }

        internal LayoutProfilerConfig Clone()
        {
            return new LayoutProfilerConfig
            {
                Enabled = Enabled,
                OutputPath = OutputPath,
                OnCompleted = OnCompleted
            };
        }
    }

    public sealed class LayoutProfilerSession
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, LayoutProfilerEntry> _entries = new(StringComparer.Ordinal);

        public void RecordMeasurement(Type componentType, double milliseconds)
        {
            lock (_sync)
            {
                var entry = GetOrCreate(componentType);
                entry.MeasureCount++;
                entry.MeasureTotalMs += milliseconds;
                if (milliseconds > entry.MeasureMaxMs)
                    entry.MeasureMaxMs = milliseconds;
            }
        }

        public void RecordCacheHit(Type componentType)
        {
            lock (_sync)
            {
                var entry = GetOrCreate(componentType);
                entry.CacheHits++;
            }
        }

        public void RecordDraw(Type componentType, double milliseconds)
        {
            lock (_sync)
            {
                var entry = GetOrCreate(componentType);
                entry.DrawCount++;
                entry.DrawTotalMs += milliseconds;
                if (milliseconds > entry.DrawMaxMs)
                    entry.DrawMaxMs = milliseconds;
            }
        }

        public LayoutProfileSnapshot Snapshot()
        {
            lock (_sync)
            {
                var entries = _entries.Values
                    .Select(e => e.ToSnapshotEntry())
                    .OrderByDescending(e => e.MeasureTotalMs + e.DrawTotalMs)
                    .ToList();

                return new LayoutProfileSnapshot
                {
                    GeneratedUtc = DateTime.UtcNow,
                    TotalMeasurementMs = entries.Sum(e => e.MeasureTotalMs),
                    TotalDrawMs = entries.Sum(e => e.DrawTotalMs),
                    Entries = entries
                };
            }
        }

        public void Reset()
        {
            lock (_sync)
            {
                _entries.Clear();
            }
        }

        internal void EnsureComponent(Type componentType)
        {
            lock (_sync)
            {
                GetOrCreate(componentType);
            }
        }

        internal void Emit(LayoutProfilerConfig config)
        {
            if (config == null || !config.Enabled)
                return;

            var snapshot = Snapshot();

            if (!string.IsNullOrWhiteSpace(config.OutputPath))
            {
                try
                {
                    string path = Path.GetFullPath(config.OutputPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                }
                catch
                {
                    // Swallow IO issues to avoid failing PDF generation.
                }
            }

            config.OnCompleted?.Invoke(snapshot);
        }

        private LayoutProfilerEntry GetOrCreate(Type componentType)
        {
            string key = componentType.Name;
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new LayoutProfilerEntry(key);
                _entries[key] = entry;
            }
            return entry;
        }

        private sealed class LayoutProfilerEntry
        {
            public LayoutProfilerEntry(string component)
            {
                Component = component;
            }

            public string Component { get; }
            public int MeasureCount;
            public double MeasureTotalMs;
            public double MeasureMaxMs;
            public int CacheHits;
            public int DrawCount;
            public double DrawTotalMs;
            public double DrawMaxMs;

            public LayoutProfileEntry ToSnapshotEntry()
            {
                double measureAvg = MeasureCount > 0 ? MeasureTotalMs / MeasureCount : 0;
                double drawAvg = DrawCount > 0 ? DrawTotalMs / DrawCount : 0;

                return new LayoutProfileEntry
                {
                    Component = Component,
                    MeasureCount = MeasureCount,
                    MeasureTotalMs = MeasureTotalMs,
                    MeasureAverageMs = measureAvg,
                    MeasureMaxMs = MeasureMaxMs,
                    CacheHits = CacheHits,
                    DrawCount = DrawCount,
                    DrawTotalMs = DrawTotalMs,
                    DrawAverageMs = drawAvg,
                    DrawMaxMs = DrawMaxMs
                };
            }
        }
    }

    public sealed class LayoutProfileSnapshot
    {
        public DateTime GeneratedUtc { get; init; }
        public double TotalMeasurementMs { get; init; }
        public double TotalDrawMs { get; init; }
        public IReadOnlyList<LayoutProfileEntry> Entries { get; init; } = Array.Empty<LayoutProfileEntry>();
    }

    public sealed class LayoutProfileEntry
    {
        public string Component { get; init; } = string.Empty;
        public int MeasureCount { get; init; }
        public double MeasureTotalMs { get; init; }
        public double MeasureAverageMs { get; init; }
        public double MeasureMaxMs { get; init; }
        public int CacheHits { get; init; }
        public int DrawCount { get; init; }
        public double DrawTotalMs { get; init; }
        public double DrawAverageMs { get; init; }
        public double DrawMaxMs { get; init; }
    }
}



