# Performance baselines

`benchmarks/PdfBuilder.Benchmarks` uses BenchmarkDotNet with MemoryDiagnoser, which records mean time, allocations, and Gen 0/1/2 collections. The benchmark suite covers plain text, 20/200-line invoices, 1,000 lines, 100/500 page reports, preview, stream, and byte-array output.

Run it manually with `dotnet run -c Release --project benchmarks/PdfBuilder.Benchmarks`. Results are environment-specific; the scheduled workflow stores JSON/Markdown artefacts rather than failing normal PR CI. A future comparison job should flag sustained regressions above 15%; that tolerance avoids cloud-runner noise while remaining material enough to investigate.

Output size, pages per second, documents per second, and peak working set are collected by the benchmark harness/reporting script when available. No QuestPDF comparison is included: no licensing decision has been recorded and it is not a PdfBuilder dependency.
