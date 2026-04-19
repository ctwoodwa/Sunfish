# Sunfish Ingestion

Framework-agnostic ingestion pipeline. Six modality adapters converge on one canonical `(entity, event[])` output that the kernel consumes identically regardless of source. Spec §7 (unified ingestion pipeline).

## Packages shipped in Platform Phase C

| Package | Modality | Size-first? | Key dependency |
|---|---|---|---|
| `Sunfish.Ingestion.Core` | Shared contract, middleware, error model | — | `Microsoft.Extensions.DependencyInjection` |
| `Sunfish.Ingestion.Forms` | `FormBlock<TModel>` submissions | No (inline body) | `Sunfish.Blocks.Forms` |
| `Sunfish.Ingestion.Spreadsheets` | CSV + XLSX | No (inline per-row) | `CsvHelper`, `ClosedXML` |
| `Sunfish.Ingestion.Voice` | Audio → transcript | Yes (audio in `IBlobStore`) | `HttpClient` (Whisper/Azure) |
| `Sunfish.Ingestion.Sensors` | JSON/NDJSON time-series batches | Yes (full batch in `IBlobStore`) | `System.Text.Json` (BCL) |
| `Sunfish.Ingestion.Imagery` | Drone/robot images | Yes (image in `IBlobStore`) | `MetadataExtractor` |
| `Sunfish.Ingestion.Satellite` | API-driven imagery | Yes (image in `IBlobStore`) | `HttpClient` + downstream provider pkgs |

## Wiring

```csharp
builder.Services.AddSunfishIngestion()
    .WithForms()                                   // enable forms modality
    .WithFormModel<WorkOrderModel>()               // register per TModel
    .WithSpreadsheets()
    .WithVoice(o => { o.Kind = TranscriberKind.NoOp; o.NoOpTranscript = "…"; })
    .WithSensors()
    .WithImagery()
    .WithSatellite();
```

`IBlobStore` must be registered separately (typically `FileSystemBlobStore` or a future S3/IPFS backend). Voice/sensors/imagery/satellite pipelines take `IBlobStore` via DI.

## Resolving a pipeline

Every modality implements `IIngestionPipeline<TInput>` for its modality-specific input:

```csharp
var pipeline = provider.GetRequiredService<IIngestionPipeline<AudioBlob>>();
var result = await pipeline.IngestAsync(audio, IngestionContext.NewCorrelation(tenantId, actorId));
if (result.IsSuccess)
{
    var entity = result.Value!; // IngestedEntity with body + events + blob CIDs
}
```

`IngestionResult<T>` is a discriminated outcome. Consumers branch on `result.Outcome` (`Success`, `ValidationFailed`, `Duplicate`, `TooLarge`, `Quarantined`, `ProviderUnavailable`, `ProviderFailed`, `UnsupportedFormat`, `InternalError`).

## Post-ingest hooks

Every modality emits a `IPostIngestHandler<T>` slot where consumer-owned ML inference plugs in (drone roof-damage detection, sensor anomaly detection, LLM mutation synthesis from transcripts). Phase C ships no default handlers.

## Parking lot

Real-time streaming, ML inference, quotas, MessagePack, BIM, AI form authoring, streaming-blob-write path, and satellite provider impls are explicit follow-ups — see `docs/superpowers/plans/2026-04-18-platform-phase-C-parking-lot.md`.

## End-to-end scenario

A quarterly inspection of Unit 4B ties all four active modalities to the same `Inspection` entity via `IngestionContext.CorrelationId`:

1. **Form** — dispatcher fills "Schedule Inspection" → `FormIngestionPipeline` mints `Inspection:inspection-abc-001` with scheduling body.
2. **Imagery** — drone flies the exterior → each JPEG routed through `ImageryIngestionPipeline` mints an `Imagery` entity with GPS + CID; correlated to the inspection.
3. **Voice** — inspector dictates per-room notes → `VoiceIngestionPipeline` mints `VoiceNote` entities with audio CID + inline transcript; LLM mutation-synthesis (parking-lot) can propose deficiency entries from the transcript.
4. **Sensor** — a leak sensor posts its batch → `SensorIngestionPipeline` mints a batch entity with 12 readings; one exceeds threshold → fires `sensor.threshold_breach` for the workflow orchestrator.

All four flows produce the same kernel-ready `(entity, event[])` shape. Downstream consumers (workflows, UI, federation, audit) treat them identically — the spec §7.7 "single canonical stream" promise, realized.

## Middleware extension points

`IIngestionMiddleware<TInput>` is a pre-ingest slot for cross-cutting concerns such as virus scanning, quota enforcement, and content moderation. Middleware is registered via `.AddMiddleware<T>()` on the builder. No default AV or quota middleware ships in `Sunfish.Ingestion.Core`; consumers wire their own.

**Example — plugging in a ClamAV scanner:**

```csharp
// 1. Implement the interface in your application or a private library:
public sealed class ClamAvMiddleware : IIngestionMiddleware<FileBlob>
{
    public async Task<MiddlewareResult> InvokeAsync(FileBlob input, IngestionContext ctx,
        MiddlewareDelegate<FileBlob> next, CancellationToken ct)
    {
        var scanResult = await _clamClient.ScanAsync(input.Stream, ct);
        if (scanResult.Infected)
            return MiddlewareResult.Quarantine(scanResult.ThreatName);
        return await next(input, ctx, ct);
    }
}

// 2. Register during service configuration:
builder.Services.AddSunfishIngestion()
    .WithImagery()
    .AddMiddleware<ClamAvMiddleware>();
```

A quarantined result surfaces as `IngestOutcome.Quarantined` in the returned `IngestionResult<T>`, which callers can branch on without further setup.
