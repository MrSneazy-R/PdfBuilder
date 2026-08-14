using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class BenchmarkBaselineRunner
{
    private const double TimingRegressionThreshold = 0.15d;
    private const double TableAllocationRegressionMultiplier = 1.20d;
    private const double TableAllocationScalingMultiplier = 12d;
    private const double TableStructuralRegressionMultiplier = 1.10d;
    private static readonly string[] TableRegressionScenarioNames = ["table-100-rows", "table-1000-rows"];
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
        WriteTableSummary(report.Scenarios);
        Console.WriteLine($"Captured {report.Scenarios.Count} benchmark baselines in '{Path.GetFullPath(outputPath)}'.");
    }

    public static void CaptureTables(string outputPath, int iterations = 1)
    {
        if (iterations <= 0) throw new ArgumentOutOfRangeException(nameof(iterations));
        BenchmarkEnvironment environment = CaptureEnvironment();
        ScenarioDefinition[] definitions = BenchmarkScenarios.All.Where(item => item.RowCount.HasValue).ToArray();
        IReadOnlyList<BenchmarkScenarioBaseline> scenarios = definitions
            .Select(definition => Measure(definition, iterations))
            .ToArray();
        var report = new BenchmarkBaseline(DateTimeOffset.UtcNow, environment, scenarios);
        Write(outputPath, report);
        WriteTableSummary(report.Scenarios);
        Console.WriteLine($"Captured {report.Scenarios.Count} table benchmark baselines in '{Path.GetFullPath(outputPath)}'.");
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

    public static void VerifyTableRegressionGates(string baselinePath, string currentPath)
    {
        BenchmarkBaseline baseline = Read(baselinePath, requireComplete: false);
        BenchmarkBaseline current = Read(currentPath, requireComplete: false);
        RequireTableRegressionScenarios(baseline, baselinePath, allowAdditionalScenarios: false);
        RequireTableRegressionScenarios(current, currentPath, allowAdditionalScenarios: true);

        var failures = new List<string>();
        foreach (string name in TableRegressionScenarioNames)
        {
            ScenarioDefinition definition = BenchmarkScenarios.All.Single(item => item.Name == name);
            BenchmarkScenarioBaseline expected = baseline.Scenarios.Single(item => item.Name == name);
            BenchmarkScenarioBaseline actual = current.Scenarios.Single(item => item.Name == name);

            long allocationCeiling = UpperBound(expected.AllocatedBytes, TableAllocationRegressionMultiplier);
            if (actual.AllocatedBytes > allocationCeiling)
                failures.Add($"{name}: allocated {actual.AllocatedBytes:N0} > 120% baseline ceiling {allocationCeiling:N0}");

            long expectedFactoryCalls = definition.ExpectedContentFactoryInvocationCount
                ?? throw new InvalidDataException($"Table regression scenario '{name}' does not declare its expected cell count.");
            long factoryCeiling = UpperBound(expectedFactoryCalls, TableStructuralRegressionMultiplier);
            if (actual.ContentFactoryInvocationCount > factoryCeiling)
                failures.Add($"{name}: content factories {actual.ContentFactoryInvocationCount:N0} > cells + 10% ceiling {factoryCeiling:N0}");

            long expectedRows = definition.RowCount
                ?? throw new InvalidDataException($"Table regression scenario '{name}' does not declare its row count.");
            long rowMeasurementCeiling = UpperBound(expectedRows, TableStructuralRegressionMultiplier);
            if (actual.TableRowMeasurementCount > rowMeasurementCeiling)
                failures.Add($"{name}: row measurements {actual.TableRowMeasurementCount:N0} > rows + 10% ceiling {rowMeasurementCeiling:N0}");
        }

        BenchmarkScenarioBaseline table100 = current.Scenarios.Single(item => item.Name == TableRegressionScenarioNames[0]);
        BenchmarkScenarioBaseline table1000 = current.Scenarios.Single(item => item.Name == TableRegressionScenarioNames[1]);
        long scalingCeiling = UpperBound(table100.AllocatedBytes, TableAllocationScalingMultiplier);
        if (table1000.AllocatedBytes > scalingCeiling)
        {
            failures.Add(
                $"table allocation scaling: 1,000 rows allocated {table1000.AllocatedBytes:N0} > " +
                $"12 x 100-row ceiling {scalingCeiling:N0}");
        }

        if (failures.Count > 0)
            throw new InvalidOperationException("Table regression gates failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));

        Console.WriteLine(
            $"Table allocation and structural gates passed. Allocation scaling: " +
            $"{table1000.AllocatedBytes / (double)Math.Max(1L, table100.AllocatedBytes):N2}x (maximum {TableAllocationScalingMultiplier:N0}x).");
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
        WriteTableSummary(report.Scenarios);
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
        BenchmarkBaseline baseline = Read(baselinePath, requireComplete: false);
        BenchmarkBaseline current = Read(currentPath, requireComplete: false);
        string[] baselineNames = baseline.Scenarios.Select(item => item.Name).ToArray();
        string[] currentNames = current.Scenarios.Select(item => item.Name).ToArray();
        if (!baselineNames.SequenceEqual(currentNames, StringComparer.Ordinal))
            throw new InvalidDataException("Benchmark files must contain the same scenarios in canonical order.");
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
        var gen0Collections = new int[iterations];
        var gen1Collections = new int[iterations];
        var gen2Collections = new int[iterations];
        ScenarioResult? latest = null;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            long before = GC.GetTotalAllocatedBytes(precise: false);
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            var stopwatch = Stopwatch.StartNew();
            latest = definition.Execute();
            stopwatch.Stop();
            AssertContentFactoryInvocations(definition, latest);
            AssertRetainedTableSegments(definition, latest);
            AssertReusableCellDrawBuffers(definition, latest);
            allocations[iteration] = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - before);
            gen0Collections[iteration] = Math.Max(0, GC.CollectionCount(0) - gen0Before);
            gen1Collections[iteration] = Math.Max(0, GC.CollectionCount(1) - gen1Before);
            gen2Collections[iteration] = Math.Max(0, GC.CollectionCount(2) - gen2Before);
            elapsed[iteration] = stopwatch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(elapsed);
        Array.Sort(allocations);
        Array.Sort(gen0Collections);
        Array.Sort(gen1Collections);
        Array.Sort(gen2Collections);
        if (latest == null)
            throw new InvalidOperationException($"Scenario '{definition.Name}' produced no result.");
        double medianMs = elapsed[elapsed.Length / 2];
        long medianAllocation = allocations[allocations.Length / 2];
        int rowCount = definition.RowCount ?? 0;
        long allocationLimit = IsTableRegressionScenario(definition)
            ? UpperBound(medianAllocation, TableAllocationRegressionMultiplier)
            : checked((long)Math.Ceiling(medianAllocation * 1.35d + 65_536d));
        var result = new BenchmarkScenarioBaseline(
            definition.Name,
            definition.RowCount,
            iterations,
            medianMs,
            medianAllocation,
            rowCount <= 0 ? 0 : medianAllocation / rowCount,
            allocationLimit,
            latest.OutputBytes,
            latest.Pages,
            medianMs <= 0 ? 0 : latest.Pages / (medianMs / 1000d),
            gen0Collections[gen0Collections.Length / 2],
            gen1Collections[gen1Collections.Length / 2],
            gen2Collections[gen2Collections.Length / 2],
            latest.MaximumRetainedStreams,
            latest.ResourceCount,
            latest.FontResources,
            latest.ImageReferences,
            latest.UniqueImageResources,
            latest.ImageDeduplicationHits,
            latest.TableMeasurementCount,
            latest.TableRowMeasurementCount,
            latest.TableCellMeasurementCount,
            latest.TableCloneCount,
            latest.TableRowCloneCount,
            latest.ContentFactoryInvocationCount,
            latest.TableCellDrawBufferAllocationCount,
            latest.Deterministic,
            definition.ExactOutputGate,
            definition.AllocationGate);
        Console.WriteLine($"  {result.MedianMilliseconds:N2} ms, {result.AllocatedBytes:N0} B allocated, {result.OutputBytes:N0} B output, {result.Pages} page(s), {result.TableCellMeasurementCount:N0} cell measure(s)");
        return result;
    }

    private static void AssertContentFactoryInvocations(ScenarioDefinition definition, ScenarioResult result)
    {
        if (!definition.ExpectedContentFactoryInvocationCount.HasValue)
            return;

        long expected = definition.ExpectedContentFactoryInvocationCount.Value;
        if (result.ContentFactoryInvocationCount != expected)
        {
            throw new InvalidOperationException(
                $"Scenario '{definition.Name}' invoked canonical table cell factories {result.ContentFactoryInvocationCount:N0} times; expected exactly {expected:N0}. " +
                "A completed TableLayoutPlan must preserve cell content and measurements across pagination.");
        }
    }

    private static void AssertRetainedTableSegments(ScenarioDefinition definition, ScenarioResult result)
    {
        if (!definition.RowCount.HasValue)
            return;
        if (result.TableCloneCount == 0 && result.TableRowCloneCount == 0)
            return;

        throw new InvalidOperationException(
            $"Scenario '{definition.Name}' cloned {result.TableCloneCount:N0} table(s) and {result.TableRowCloneCount:N0} row(s). " +
            "Normal pagination must render retained TableSegment views without cloning table structures.");
    }

    private static void AssertReusableCellDrawBuffers(ScenarioDefinition definition, ScenarioResult result)
    {
        if (!definition.RowCount.HasValue)
            return;
        if (result.TableCellDrawBufferAllocationCount == result.Pages)
            return;

        throw new InvalidOperationException(
            $"Scenario '{definition.Name}' allocated {result.TableCellDrawBufferAllocationCount:N0} cell draw buffer(s) for {result.Pages:N0} page(s). " +
            "Canonical table cells must share one reusable draw buffer per retained page segment.");
    }

    private static bool IsTableRegressionScenario(ScenarioDefinition definition) =>
        TableRegressionScenarioNames.Contains(definition.Name, StringComparer.Ordinal);

    private static long UpperBound(long value, double multiplier) =>
        checked((long)Math.Floor(value * multiplier));

    private static void RequireTableRegressionScenarios(
        BenchmarkBaseline report,
        string path,
        bool allowAdditionalScenarios)
    {
        string[] present = report.Scenarios
            .Where(item => TableRegressionScenarioNames.Contains(item.Name, StringComparer.Ordinal))
            .Select(item => item.Name)
            .ToArray();
        if (!present.SequenceEqual(TableRegressionScenarioNames, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Benchmark file '{path}' must contain {string.Join(" and ", TableRegressionScenarioNames)} in canonical order.");
        }
        if (!allowAdditionalScenarios && report.Scenarios.Count != TableRegressionScenarioNames.Length)
            throw new InvalidDataException($"Table regression baseline '{path}' must contain only the ordinary-CI table scenarios.");
    }

    private static void WriteTableSummary(IEnumerable<BenchmarkScenarioBaseline> scenarios)
    {
        BenchmarkScenarioBaseline[] tables = scenarios.Where(item => item.RowCount.HasValue).ToArray();
        if (tables.Length == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("| Rows | Time (ms) | Allocation (B) | Alloc/row (B) | Pages | Output (B) | Gen0/1/2 | Retained streams | Table measures | Row measures | Cell measures | Factory calls | Draw buffers | Table clones | Row clones |");
        Console.WriteLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (BenchmarkScenarioBaseline table in tables)
        {
            Console.WriteLine(
                $"| {table.RowCount:N0} | {table.MedianMilliseconds:N2} | {table.AllocatedBytes:N0} | {table.AllocatedBytesPerRow:N0} | " +
                $"{table.Pages:N0} | {table.OutputBytes:N0} | {table.Gen0Collections:N0}/{table.Gen1Collections:N0}/{table.Gen2Collections:N0} | " +
                $"{table.MaximumRetainedStreams:N0} | {table.TableMeasurementCount:N0} | {table.TableRowMeasurementCount:N0} | " +
                $"{table.TableCellMeasurementCount:N0} | {table.ContentFactoryInvocationCount:N0} | {table.TableCellDrawBufferAllocationCount:N0} | {table.TableCloneCount:N0} | {table.TableRowCloneCount:N0} |");
        }
        Console.WriteLine();
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
    int? RowCount,
    int Iterations,
    double MedianMilliseconds,
    long AllocatedBytes,
    long AllocatedBytesPerRow,
    long AllocationLimitBytes,
    long OutputBytes,
    int Pages,
    double PagesPerSecond,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int MaximumRetainedStreams,
    int ResourceCount,
    int FontResources,
    int ImageReferences,
    int UniqueImageResources,
    int ImageDeduplicationHits,
    long TableMeasurementCount,
    long TableRowMeasurementCount,
    long TableCellMeasurementCount,
    long TableCloneCount,
    long TableRowCloneCount,
    long ContentFactoryInvocationCount,
    long TableCellDrawBufferAllocationCount,
    bool Deterministic,
    bool ExactOutputGate,
    bool AllocationGate);
