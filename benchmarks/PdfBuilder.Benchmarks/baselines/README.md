# Benchmark baselines

BenchmarkDotNet produces the authoritative measurements in `BenchmarkDotNet.Artifacts`.
The scheduled workflow retains those reports so a maintainer can update this directory
after confirming a stable result on the same runner and SDK. CI deliberately does not
fail on ordinary cloud-runner variance.

Review a sustained regression of more than 15% in mean time, allocation, or output
size. The threshold intentionally avoids treating transient hosted-runner noise as a
product regression. The initial baseline is established by the scheduled benchmark
workflow; no comparative QuestPDF benchmark is included because its licensing has not
been reviewed for this repository.
