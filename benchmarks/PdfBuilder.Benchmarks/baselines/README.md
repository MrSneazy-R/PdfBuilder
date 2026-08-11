# Benchmark baselines

`windows-x64-net10.json` is the checked-in reference snapshot for the complete benchmark corpus. It records the capture timestamp, OS, runtime, architecture, processor, GC mode, timing, allocations, output bytes, page throughput, maximum retained streams, resource counts, font/image counts, image deduplication, and deterministic-output status.

The reference was captured on the hardware recorded inside the JSON file. Timing is comparable only after reviewing that metadata. The scheduled Windows workflow captures every scenario in an isolated process, compares its timings with a 15% review threshold, emits workflow warnings for regressions, runs BenchmarkDotNet, and uploads both result sets. Timing variance does not block ordinary pull requests.

Normal Windows CI runs `--verify-gates` against the bounded deterministic subset. It requires exact output/resource counts, retained-stream ceilings, and allocation limits. Heavy/noisy cases such as the 1,000-row table, 500-page report, multilingual platform fonts, previews, concurrency, and cancellation remain measured but are not ordinary-CI gates.

To refresh a scenario without retaining previous-process allocation pressure:

```powershell
dotnet run -c Release --project benchmarks/PdfBuilder.Benchmarks -- --capture-scenario invoice benchmarks/PdfBuilder.Benchmarks/baselines/windows-x64-net10.json 1
```

Review every baseline change. In particular, do not normalize away the 1,000-row allocation result: it is a retained performance signal even though the generated PDF remains compact. No QuestPDF comparison is included because its use has not received an explicit licence approval.
