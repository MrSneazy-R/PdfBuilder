# Performance baselines

`benchmarks/PdfBuilder.Benchmarks` contains BenchmarkDotNet benchmarks and a deterministic metric harness for:

- minimal document, invoice, multi-page invoice, 1,000-row table, and 500-page report;
- multilingual shaping, image-heavy output, core charts, and advanced charts;
- previews, streaming, concurrent invoices, cancellation, resource deduplication, and deterministic generation.

The checked-in `baselines/windows-x64-net10.json` records timing, allocations, output bytes, pages per second, maximum retained page streams, aggregate resource counts, font resources, image references, unique images, deduplication hits, and hardware/runtime metadata.

Normal Windows CI executes the bounded `--verify-gates` subset. Output byte counts and resource counts may decrease but must not exceed their recorded ceilings, retained streams may not increase, and measured allocations must remain below the recorded guardrail. Dedicated generation tests retain byte-for-byte determinism checks within each runtime environment. Large or platform-sensitive scenarios are retained for scheduled/manual analysis rather than making ordinary CI noisy.

The scheduled workflow captures each scenario in a fresh process so the 1,000-row allocation workload cannot distort later measurements through GC pressure. It emits warnings when reference timing differs by more than 15%, then runs BenchmarkDotNet with one launch, one warmup, and three measured iterations. Compare timing only after checking the recorded runner, processor, runtime, and GC metadata.

The current baseline intentionally records the high cumulative allocation cost of the 1,000-row table. Its output remains compact and page content is streamed one page at a time, but the allocation result is an optimization target and must not be hidden by changing the fixture size.

No QuestPDF comparison is present because no licence approval has been recorded for adding or using it as a dependency.
