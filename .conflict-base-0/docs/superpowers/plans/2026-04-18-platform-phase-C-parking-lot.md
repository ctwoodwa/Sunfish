# Platform Phase C — Parking Lot

Deferred items explicitly scoped out of Platform Phase C (ingestion pipeline, shipped 2026-04-18). Each entry names the future package/contract, the source spec reference, and the platform phase most likely to deliver it.

## 1. AI-assisted form authoring (Typeform-AI flow)

- **Package:** `Sunfish.Ingestion.Forms.Ai` (future)
- **Shape:** "Describe the form in natural language → get schema + layout." Wraps a pluggable LLM adapter with `NoOp` default; production consumers configure OpenAI / Anthropic / Azure OpenAI via `HttpClient` (no SDK coupling, same pattern as `Sunfish.Ingestion.Voice`).
- **Why deferred:** AI authoring is orthogonal to ingestion itself. Phase C's `FormBlock<TModel>`-backed runtime path is orthogonal to the design-time authoring surface.
- **Reference:** external-references.md §1.1 (Typeform AI).

## 2. AI-assisted voice mutation synthesis (spec §7.3 LLM orchestrator)

- **Package:** `Sunfish.Ingestion.Voice.MutationSynthesis` (future)
- **Shape:** `IPostIngestHandler<TranscriptionResult>` implementation that runs the transcript through an LLM to propose kernel `(entity, event[])` mutations. Consumer confirms via §7.7 confirmation gate before apply.
- **Why deferred:** consumer-owned; depends on the kernel mutation-proposal API (not yet designed).
- **Reference:** spec §7.3 end-to-end example.

## 3. Real-time streaming sensor ingestion (MQTT, IoT Hub, Kinesis)

- **Target platform phase:** D (federation + streaming)
- **Shape:** persistent subscription-based ingestion. Current Phase C sensors handler is batch-only (stream in, entity out).
- **Why deferred:** requires hosted-service lifecycle and per-tenant quota machinery that Phase C explicitly scopes out.

## 4. ML inference hooks

- **Shape:** consumer-owned via `IPostIngestHandler<T>` registered for each modality's result type. Examples: crack detection on drone images, anomaly detection on sensor batches.
- **Why deferred:** model selection and deployment are domain-specific.

## 5. Multi-tenant input-volume quotas / rate limits

- **Package:** likely a `QuotaMiddleware` in `Sunfish.Ingestion.Core`
- **Why deferred:** requires a quota-store contract (`IIngestionQuotaStore`) and per-tenant configuration surface. Middleware slot already exists.

## 6. MessagePack sensor-batch support

- **Package:** a `MessagePackSensorBatchDecoder` in `Sunfish.Ingestion.Sensors`, gated behind an optional package pin.
- **Why deferred:** adds a non-trivial dependency (`MessagePack-CSharp`) for a workload niche. Phase C ships a `NoOpMessagePackDecoder` stub that returns `IngestOutcome.UnsupportedFormat`.

## 7. BIM / CAD imports

- **Package:** `Sunfish.Ingestion.Bim` (future, dedicated platform phase)
- **Shape:** IFC parsing via Xbim Toolkit; two-way sync with entity store.
- **Why deferred:** IFC 4.3.2 surface is substantially larger than any Phase C modality.
- **Reference:** spec §7.6, §9.2.

## 8. Streaming blob-write path

- **Contract:** new `IBlobStore.PutStreamingAsync(Stream, CancellationToken)` on `Sunfish.Foundation.Blobs.IBlobStore`
- **Why deferred:** Phase C buffers to `MemoryStream` before `IBlobStore.PutAsync(ReadOnlyMemory<byte>)`. Multi-GB imagery hits a practical 2 GB limit. Parking-lot note lives in `ingestion-imagery/ImageryIngestionPipeline.cs` via the skipped `Ingest_100MbStream_StreamsWithoutMemoryExplosion` test.

## 9. Federation / cross-jurisdiction ingestion

- **Target platform phase:** D (federation)
- **Why deferred:** ingestion contracts already accept `IngestionContext.TenantId`; the federation adapter that routes cross-tenant is Phase D work.

## 10. Satellite provider implementations

- **Packages (downstream, outside this repo):** `Sunfish.Ingestion.Satellite.PlanetLabs`, `Sunfish.Ingestion.Satellite.Maxar`, `Sunfish.Ingestion.Satellite.SentinelHub`, `Sunfish.Ingestion.Satellite.AirbusOneAtlas`
- **Why deferred:** provider SDKs are heavy, typically closed-source, require commercial credentials. Phase C ships the contract + `NoOpSatelliteImageryProvider` so the pipeline stands up without provider selection.

## 11. Virus scanning middleware

- **Middleware:** `AntivirusMiddleware` in `Sunfish.Ingestion.Core` (future, ClamAV default)
- **Why deferred:** Phase C ships the middleware slot (`IIngestionMiddleware<TInput>`) and the `IngestOutcome.Quarantined` discriminator, but no default AV integration. Consumers wire their own.

## 12. Per-schema blob-boundary override

- **Shape:** `blobThreshold` property on schema-registry descriptor (spec §3.4). Phase C's D-BLOB-BOUNDARY is a global 64 KiB default.
- **Why deferred:** requires the schema registry primitive, which is a separate platform phase deliverable.

## 13. True atomic batch ingestion for spreadsheets

- **Shape:** needs a kernel `entity.create-batch` transactional API.
- **Why deferred:** Phase C's `SpreadsheetIngestionPipeline` produces a single `IngestedEntity` with N per-row events sharing a `CorrelationId` — the downstream kernel does the atomic write when that API lands.

## 14. Integration tests against real external APIs

- **Shape:** opt-in separate `*.IntegrationTests` projects that hit real Whisper / Azure Speech endpoints with tenant-provided keys.
- **Why deferred:** Phase C tests use mocked `HttpClient` (`StubHandler`) exclusively — no real API calls in CI.

---

*Canonical tracker for Phase C deferrals. When a follow-up phase ships an item, update that item's status here and remove the entry.*
