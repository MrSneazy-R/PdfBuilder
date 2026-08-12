using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class BenchmarkBaselineRunner
{
    private const double TimingRegressionThreshold = 0.15d;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Capture(string outputPath, int iterations = 1)
    {
        if (iterations <= 0) throw new ArgumentOutOfRangeException(nameof(iterations));
        BenchmarkEnvironment environment = CaptureEnvironment();
        var scenarios = new List<BenchmarkScenarioBaseline>();
        foreach (ScenarioDefinition definition in BenchmarkScenarios.All)
        {
            scenarios.Add(Measure(definition, iterations));
            Write(outputPath, new BenchmarkBaseline(DateTimeOffset.UtcNow, environment, scenarios));
        }
        BenchmarkBaseline report = new(DateTimeOffset.UtcNow, environment, scenarios);
        Console.WriteLine($"Captured {report.Scenarios.Count} benchmark baselines in '{Path.GetFullPath(outputPath)}'.");
    }

    public static void VerifyDeterministicGates(string baselinePath)
    {
        BenchmarkBaseline baseline = Read(baselinePath);
        var failures = new List<string>();
        ScenarioDefinition[] gated = BenchmarkScenarios.All.Where(item => item.ExactOutputGate || item.AllocationGate).ToArray();
        foreach (ScenarioDefinition definition in gated)
        {
            BenchmarkScenarioBaseline expected = baseline.Scenarios.Single(item => item.Name == definition.Name);
            BenchmarkScenarioBaseline actual = Measure(definition, iterations: 1);
            if (definition.ExactOutputGate && actual.OutputBytes > expected.OutputBytes)
                failures.Add($"{definition.Name}: output bytes {actual.OutputBytes:N0} > baseline ceiling {expected.OutputBytes:N0}");
            if (definition.AllocationGate && actual.AllocatedBytes > expected.AllocationLimitBytes)
                failures.Add($"{definition.Name}: allocated {actual.AllocatedBytes:N0} > gate {expected.AllocationLimitBytes:N0}");
            if (actual.MaximumRetainedStreams > expected.MaximumRetainedStreams)
                failures.Add($"{definition.Name}: retained streams {actual.MaximumRetainedStreams} > baseline {expected.MaximumRetainedStreams}");
            if (actual.ResourceCount > expected.ResourceCount)
                failures.Add($"{definition.Name}: resources {actual.ResourceCount} > baseline ceiling {expected.ResourceCount}");
        }

        if (failures.Count > 0)
            throw new InvalidOperationException("Deterministic benchmark gates failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        Console.WriteLine($"Deterministic output-size, allocation, retained-stream, and resource ceilings passed for {gated.Length} scenarios.");
    }

    public static void CaptureScenario(string name, string outputPath, int iterations = 1)
    {
        if (iterations <= 0) throw new ArgumentOutOfRangeException(nameof(iterations));
        ScenarioDefinition definition = BenchmarkScenarios.All.Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        BenchmarkScenarioBaseline measurement = Measure(definition, iterations);
        BenchmarkBaseline report;
        if (File.Exists(outputPath))
        {
            BenchmarkBaseline existing = Read(outputPath, requireComplete: false);
            var byName = existing.Scenarios.ToDictionary(item => item.Name, StringComparer.Ordinal);
            byName[name] = measurement;
            var ordered = BenchmarkScenarios.All.Where(item => byName.ContainsKey(item.Name)).Select(item => byName[item.Name]).ToList();
            report = new BenchmarkBaseline(DateTimeOffset.UtcNow, CaptureEnvironment(), ordered);
        }
        else
        {
            report = new BenchmarkBaseline(DateTimeOffset.UtcNow, CaptureEnvironment(), new[] { measurement });
        }
        Write(outputPath, report);
        Console.WriteLine($"Captured '{name}' in '{Path.GetFullPath(outputPath)}'.");
    }

    public static void CompareTiming(string baselinePath, string outputPath)
    {
        BenchmarkBaseline current = Measure(iterations: 1);
        Write(outputPath, current);
        CompareFiles(baselinePath, outputPath);
    }

    public static void CompareFiles(string baselinePath, string currentPath)
    {
        BenchmarkBaseline baseline = Read(baselinePath);
        BenchmarkBaseline current = Read(currentPath);
        var regressions = new List<string>();
        foreach (BenchmarkScenarioBaseline actual in current.Scenarios)
        {
            BenchmarkScenarioBaseline expected = baseline.Scenarios.Single(item => item.Name == actual.Name);
            double ratio = expected.MedianMilliseconds <= 0 ? 0 : (actual.MedianMilliseconds - expected.MedianMilliseconds) / expected.MedianMilliseconds;
            Console.WriteLine($"{actual.Name,-28} {actual.MedianMilliseconds,10:N2} ms ({ratio,8:P1})");
            if (ratio > TimingRegressionThreshold)
                regressions.Add($"{actual.Name}: {ratio:P1} slower ({actual.MedianMilliseconds:N2} ms vs {expected.MedianMilliseconds:N2} ms)");
        }
        if (regressions.Count > 0)
        {
            foreach (string regression in regressions)
                Console.WriteLine($"::warning title=Benchmark timing regression::{regression}");
        }
    }

    private static BenchmarkBaseline Measure(int iterations) => new(
        DateTimeOffset.UtcNow,
        CaptureEnvironment(),
        BenchmarkScenarios.All.Select(definition => Measure(definition, iterations)).ToList());

    private static BenchmarkEnvironment CaptureEnvironment() =>
        new(
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
            GCSettings.IsServerGC);

    private static BenchmarkScenarioBaseline Measure(ScenarioDefinition definition, int iterations)
    {
        Console.WriteLine($"Measuring {definition.Name} ({iterations} iteration{(iterations == 1 ? string.Empty : "s")})...");
        if (iterations > 1)
            _ = definition.Execute();
        var elapsed = new double[iterations];
        var allocations = new long[iterations];
        ScenarioResult? latest = null;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            long before = GC.GetTotalAllocatedBytes(precise: false);
            var stopwatch = Stopwatch.StartNew();
            latest = definition.Execute();
            stopwatch.Stop();
            allocations[iteration] = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - before);
            elapsed[iteration] = stopwatch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(elapsed);
        Array.Sort(allocations);
        if (latest == null)
            throw new InvalidOperationException($"Scenario '{definition.Name}' produced no result.");
        double medianMs = elapsed[elapsed.Length / 2];
        long medianAllocation = allocations[allocations.Length / 2];
        long allocationLimit = checked((long)Math.Ceiling(medianAllocation * 1.35d + 65_536d));
        var result = new BenchmarkScenarioBaseline(
            definition.Name,
            iterations,
            medianMs,
            medianAllocation,
            allocationLimit,
            latest.OutputBytes,
            latest.Pages,
            medianMs <= 0 ? 0 : latest.Pages / (medianMs / 1000d),
            latest.MaximumRetainedStreams,
            latest.ResourceCount,
            latest.FontResources,
            latest.ImageReferences,
            latest.UniqueImageResources,
            latest.ImageDeduplicationHits,
            latest.Deterministic,
            definition.ExactOutputGate,
            definition.AllocationGate);
        Console.WriteLine($"  {result.MedianMilliseconds:N2} ms, {result.AllocatedBytes:N0} B allocated, {result.OutputBytes:N0} B output, {result.Pages} page(s)");
        return result;
    }

    private static BenchmarkBaseline Read(string path, bool requireComplete = true)
    {
        BenchmarkBaseline baseline = JsonSerializer.Deserialize<BenchmarkBaseline>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Benchmark baseline '{path}' is empty or invalid.");
        string[] expectedNames = BenchmarkScenarios.All.Select(item => item.Name).ToArray();
        string[] actualNames = baseline.Scenarios.Select(item => item.Name).ToArray();
        if (requireComplete && !actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
            throw new InvalidDataException($"Benchmark baseline '{path}' must contain every scenario once in canonical order.");
        if (!requireComplete && (actualNames.Distinct(StringComparer.Ordinal).Count() != actualNames.Length || actualNames.Any(name => !expectedNames.Contains(name, StringComparer.Ordinal))))
            throw new InvalidDataException($"Benchmark baseline '{path}' contains duplicate or unknown scenarios.");
        if (baseline.Scenarios.Any(item => item.Iterations <= 0 || item.AllocatedBytes < 0 || item.OutputBytes < 0 || item.Pages < 0 || !item.Deterministic))
            throw new InvalidDataException($"Benchmark baseline '{path}' contains invalid or non-deterministic measurements.");
        return baseline;
    }

    private static void Write(string path, BenchmarkBaseline report)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, JsonOptions));
    }
}

internal sealed record BenchmarkBaseline(
    DateTimeOffset CapturedAtUtc,
    BenchmarkEnvironment Environment,
    IReadOnlyList<BenchmarkScenarioBaseline> Scenarios);

internal sealed record BenchmarkEnvironment(
    string OperatingSystem,
    string Runtime,
    string Architecture,
    int ProcessorCount,
    string Processor,
    bool ServerGc);

internal sealed record BenchmarkScenarioBaseline(
    string Name,
    int Iterations,
    double MedianMilliseconds,
    long AllocatedBytes,
    long AllocationLimitBytes,
    long OutputBytes,
    int Pages,
    double PagesPerSecond,
    int MaximumRetainedStreams,
    int ResourceCount,
    int FontResources,
    int ImageReferences,
    int UniqueImageResources,
    int ImageDeduplicationHits,
    bool Deterministic,
    bool ExactOutputGate,
    bool AllocationGate);
