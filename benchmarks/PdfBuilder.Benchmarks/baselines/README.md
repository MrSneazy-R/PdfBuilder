# Benchmark baselines

`windows-x64-net10.json` is the checked-in reference snapshot for the complete benchmark corpus. It records the capture timestamp, OS, runtime, architecture, processor, GC mode, timing, allocations, output bytes, page throughput, maximum retained streams, resource counts, font/image counts, image deduplication, and deterministic-output status.

The reference was captured on the hardware recorded inside the JSON file. Timing is comparable only after reviewing that metadata. The scheduled Windows workflow captures every scenario in an isolated process, compares its timings with a 15% review threshold, emits workflow warnings for regressions, runs BenchmarkDotNet, and uploads both result sets. Timing variance does not block ordinary pull requests.

Normal Windows CI runs `--verify-gates` against the bounded deterministic subset. It requires output-size and resource-count non-regression ceilings, retained-stream ceilings, and allocation limits. Dedicated unit tests verify byte-for-byte determinism within each runtime environment; compressed byte counts may differ slightly between runtime/zlib revisions.

Table performance has a separate ordinary-CI baseline in `windows-x64-net10-tables.json`. The 100-row and 1,000-row fixtures are captured in isolated processes and checked with `--verify-table-gates`. Each allocation result may be at most 120% of its baseline, the 1,000-row result may be at most 12 times the 100-row result, content-factory calls may be at most 110% of the canonical cell count, and row measurements may be at most 110% of the requested body-row count. The 5,000-row fixture remains scheduled-only.

To refresh a scenario without retaining previous-process allocation pressure:

```powershell
dotnet run -c Release --project benchmarks/PdfBuilder.Benchmarks -- --capture-scenario invoice benchmarks/PdfBuilder.Benchmarks/baselines/windows-x64-net10.json 1
```

Review every baseline change. In particular, do not normalize away the 1,000-row allocation result: it is a retained performance signal even though the generated PDF remains compact. No QuestPDF comparison is included because its use has not received an explicit licence approval.
