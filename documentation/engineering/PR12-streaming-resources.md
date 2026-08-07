# PR 12: streaming and resource planning

## Resource audit

Before this change `PdfWriter` rendered every page into a `List<byte[]>` before it wrote
any content stream. It also retained the per-page annotation and anchor lists until their
references could be resolved. Font glyph registrations were document-scoped, image XObjects
were stored in `PdfResourceManager`, and preview PNG data was retained only by the explicit
preview API, not normal generation.

The normal writer now has two phases:

1. A resource-planning pass renders and discards each page stream while collecting font glyphs,
   annotations, anchors, shared images and opacity states. Page object references are reserved.
2. A writing pass rebuilds one page stream, immediately writes it to the caller-provided stream,
   then releases that page buffer before moving on.

Annotations and anchors still remain until page references are known. This is intentional:
they are small navigation models rather than complete content buffers. Font registrations and
resources are document-local, and object reservation ensures an extra annotation cannot corrupt
page references.

## Ownership and concurrency

`PdfResourceManager`, `EmbeddedFontRegistry`, render contexts and generation metrics are created
per `PdfWriter` invocation. They contain no static mutable document state. Repeated image data is
identified with SHA-256, then confirmed with byte-for-byte equality before the existing resource
is reused. The manager retains its own copy only for collision-safe equality; caller-owned image
arrays are not used as cache keys after registration.

Existing native-object ownership remains local: `SKFont`, `SKData`, `SKImage`, `SKBitmap`,
`SKPath` and HarfBuzz values created by renderers are disposed in their creating scope. Shared
typefaces are not disposed by the document writer.

## Cancellation and asynchronous behaviour

`GenerateBytes`, `Generate(Stream)` and `Save` have cancellation overloads. Cancellation is
checked before generation, on each planning-page iteration and on each writing-page iteration.
Generation remains synchronous; no `Task.Run`-based asynchronous API was introduced.

## Measurement

The `StreamGeneration_DoesNotBufferAllPageContent` test generates 40 pages and records a maximum
of one retained page-content stream. The previous implementation retained 40 streams for the
same workload. The `Cancellation_StopsLargeDocumentGeneration` test uses 100 pages and cancels
after the first destination write, confirming that the writer observes cancellation before the
next page. Allocation measurements are workload- and runtime-dependent, so this repository
records the stable retention bound rather than a machine-specific byte total.

## Deferred work

True asynchronous stream writes require an API designed around `Stream.WriteAsync` and are not
introduced here. Deterministic generation options, compressed-by-default policy, and serializer
encoding changes remain owned by the serializer hardening PR; the streaming tests preserve
byte-equivalence when fixed metadata is supplied.
