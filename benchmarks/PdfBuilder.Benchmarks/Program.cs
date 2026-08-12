using BenchmarkDotNet.Running;

if (args.Length > 0 && string.Equals(args[0], "--capture-baseline", StringComparison.OrdinalIgnoreCase))
{
    string output = args.Length > 1 ? args[1] : throw new ArgumentException("--capture-baseline requires an output path.");
    int iterations = args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 1;
    BenchmarkBaselineRunner.Capture(output, iterations);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--verify-gates", StringComparison.OrdinalIgnoreCase))
{
    string baseline = args.Length > 1 ? args[1] : throw new ArgumentException("--verify-gates requires a baseline path.");
    BenchmarkBaselineRunner.VerifyDeterministicGates(baseline);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--capture-scenario", StringComparison.OrdinalIgnoreCase))
{
    string name = args.Length > 1 ? args[1] : throw new ArgumentException("--capture-scenario requires a scenario name.");
    string output = args.Length > 2 ? args[2] : throw new ArgumentException("--capture-scenario requires an output path.");
    int iterations = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 1;
    BenchmarkBaselineRunner.CaptureScenario(name, output, iterations);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--compare-baseline", StringComparison.OrdinalIgnoreCase))
{
    string baseline = args.Length > 1 ? args[1] : throw new ArgumentException("--compare-baseline requires a baseline path.");
    string output = args.Length > 2 ? args[2] : Path.Combine("BenchmarkDotNet.Artifacts", "current-baseline.json");
    BenchmarkBaselineRunner.CompareTiming(baseline, output);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--compare-files", StringComparison.OrdinalIgnoreCase))
{
    string baseline = args.Length > 1 ? args[1] : throw new ArgumentException("--compare-files requires a baseline path.");
    string current = args.Length > 2 ? args[2] : throw new ArgumentException("--compare-files requires a current-results path.");
    BenchmarkBaselineRunner.CompareFiles(baseline, current);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
