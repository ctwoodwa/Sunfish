# Platform Phase C: Input Modalities — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

---

## Platform Context

> **Read this before executing.** Platform Phase C implements spec §7 — the **unified ingestion pipeline** — as a family of six framework-agnostic .NET packages that convert arbitrary external inputs into kernel `(entity, event[])` tuples. Every modality converges on the same canonical flow (spec §7.7 ASCII diagram): source → modality adapter → raw artifact store → normalizer → proposed mutations → confirmation gate → kernel. The leverage point is spec-grade: a deficiency minted from a paper form, a voice note, a drone flyover, or a sensor threshold breach all look **identical** to downstream consumers by the time they reach the kernel.
>
> **Where this Phase C plan fits in the platform roadmap:**
>
> - **Platform Phase A** — (prior) Kernel primitives (entity storage, versioning, audit trail, schema registry — referenced by spec §3). Not yet landed in the migration branch; future work.
> - **Platform Phase B-blobs** — (shipped) `Sunfish.Foundation.Blobs` (`Cid`, `IBlobStore`, `FileSystemBlobStore`) — the content-addressed blob substrate all large binary modalities route through.
> - **Platform Phase C — this plan** — the ingestion pipeline: six modality adapters + shared infrastructure + DI integration. No UI, no Blazor coupling. Pure data-layer.
> - **Platform Phase D** — (future) Federation + cross-jurisdictional ingestion, real-time streaming (IoT MQTT, Azure IoT Hub), multi-tenant quotas.
>
> **What makes Phase C a platform phase (not a migration phase):** Earlier `Phase 1..9` plans lift Marilo / PmDemo code into Sunfish package shapes. Phase C is net-new — there is no Marilo ingestion code to migrate. These are greenfield packages authored from spec §7 and the research notes (external-references.md §1 for forms; ipfs-evaluation.md §2 for blob-boundary rationale).

---

**Goal:** Deliver six modality-specific ingestion adapters plus shared infrastructure that together normalize arbitrary inputs (forms, spreadsheets, voice, sensors, drone imagery, satellite imagery) into a single canonical `(entity, event[])` stream ready for the kernel. Each modality is its own packable library under `packages/ingestion-*/`; cross-cutting concerns (validation, deduplication, middleware, error model) live in `Sunfish.Ingestion.Core`. All large binaries route through `Sunfish.Foundation.Blobs.IBlobStore` and are referenced by CID. No Blazor dependency on any ingestion package.

**Architecture context:** The ingestion packages sit above `foundation` (specifically `Sunfish.Foundation.Blobs`) and parallel to `blocks-*`. They depend on `foundation` but NOT on `ui-core`, `ui-adapters-blazor`, or `blocks-*` — ingestion is a pure pipeline/data layer. The one soft coupling is `Sunfish.Ingestion.Forms`, which takes a project reference to `Sunfish.Blocks.Forms` to bind to the already-shipped `FormBlock<TModel>` surface; this is acceptable because `blocks-forms` is the canonical form primitive in the Sunfish stack and re-implementing the form surface inside an ingestion package would fracture the single-source-of-truth rule.

```
foundation (+ Sunfish.Foundation.Blobs)
  ↓
ingestion-core         ← shared pipeline contract, middleware, error model
  ↓                          (depends on foundation only)
ingestion-forms        ← depends on ingestion-core + blocks-forms
ingestion-spreadsheets ← depends on ingestion-core
ingestion-voice        ← depends on ingestion-core + foundation.Blobs
ingestion-sensors      ← depends on ingestion-core + foundation.Blobs
ingestion-imagery      ← depends on ingestion-core + foundation.Blobs
ingestion-satellite    ← depends on ingestion-core + foundation.Blobs
```

**Tech Stack:** .NET 10, C# 13, `System.IO.Pipelines` (sensor streaming), ClosedXML (XLSX), CsvHelper (CSV), MetadataExtractor (EXIF / GPS), `HttpClient` (voice + satellite external APIs — no provider SDK coupling), xUnit 2.9.x, NSubstitute for mocks. All version-pinned centrally in `Directory.Packages.props` (Task 0).

**Source references:**
- **Spec §7** (`docs/specifications/sunfish-platform-specification.md` lines 1586–1668) — the Unified Ingestion Pipeline; each modality section provides the property-management reference example.
- **Spec §6** (lines 1418–1582) — the Property Management MVP feature set. §6.3 Inspections and §6.4 Maintenance Workflows are the end-to-end exercise of Phase C (form + voice + drone imagery all attributed to one `Inspection` entity — see end-to-end scenario at the bottom of this plan).
- **external-references.md §1** — Form-builder design references (Typeform AI, Formstack, Feathery, Budibase) that inform the form-ingestion surface and the parked AI-authoring extension.
- **ipfs-evaluation.md §2** — IPFS-style CIDs as the canonical blob identifier, rationale for why voice / imagery / sensors batches route through `IBlobStore` and not inline entity bodies.

---

## Scope

### In Scope

Six new packages under `packages/ingestion-*/`, plus shared infrastructure:

- `ingestion-core` — `IIngestionPipeline<TInput>`, `IngestionContext`, `IIngestionMiddleware`, `IngestionResult<T>`, `IngestOutcome<T>` (discriminated failure model)
- `ingestion-forms` — wraps `FormBlock<TModel>` submissions; small structured inputs; no blob store
- `ingestion-spreadsheets` — CSV (CsvHelper) + XLSX (ClosedXML) with header-row → schema mapping; per-row entity minting
- `ingestion-voice` — `IVoiceTranscriber` contract + OpenAI Whisper (HttpClient) + Azure Speech (HttpClient) + NoOp adapters; audio blob routes through `IBlobStore`
- `ingestion-sensors` — `System.IO.Pipelines` streaming decoder for JSON + MessagePack batches; chunked blob archival + per-reading projection
- `ingestion-imagery` — drone/robot large-image ingest; EXIF + GPS metadata via MetadataExtractor; image bytes in blob store, metadata in entity body
- `ingestion-satellite` — `ISatelliteImageryProvider` contract + NoOp adapter; scaffold for provider-specific integrations (Planet Labs, Maxar)
- One shared DI extension package presence: `AddSunfishIngestion()` in `Sunfish.Ingestion.Core` with per-modality toggles (`.WithForms()`, `.WithSpreadsheets()`, etc.)
- xUnit tests for each package (counts per task section); mocked `HttpClient` / mocked `IBlobStore` via NSubstitute; bundled sample fixtures (3 spreadsheets, 1 JPEG with known EXIF, 1 sensor-batch JSON)
- `Sunfish.slnx` updated with 12 new csproj entries (6 ingestion packages + 6 tests)
- `Directory.Packages.props` pinned versions for the three third-party libraries (ClosedXML, CsvHelper, MetadataExtractor)

### Out of Scope — Deferred to Future Platform Phases

Phase C is **ingestion pipeline substrate only**. The following are explicitly out of scope:

- **Real-time streaming** (IoT MQTT, CoAP, Azure IoT Hub, AWS IoT Core) — batch ingestion only in Phase C. Each sensor-batch is a file or byte-sequence handed to the adapter, not a persistent subscription. Real-time deferred to Platform Phase D.
- **ML-driven inference at ingestion time** (object detection on drone imagery, anomaly detection on sensor batches, LLM-driven mutation synthesis from voice transcripts per spec §7.3 sample) — each adapter exposes a typed **post-ingest hook** (`IPostIngestHandler<T>`) where ML integrations plug in; the actual ML implementation is consumer-owned.
- **Multi-tenant input-volume quotas / rate limits** — the middleware pipeline exposes a `IIngestionMiddleware` slot where a consumer can insert a quota middleware; Phase C ships no default implementation (unbounded).
- **Federation — cross-jurisdiction ingestion** — Platform Phase D concern. All Phase C adapters assume a single-tenant ingestion target.
- **AI-assisted form authoring** (Typeform-AI-style "describe the form → get schema + layout") — shipped as a parking-lot item, NOT in Phase C MVP. A future `Sunfish.Ingestion.Forms.Ai` package will wrap a pluggable LLM adapter; Phase C delivers only the `FormBlock<TModel>`-backed runtime submission path.
- **BIM / CAD imports** (spec §7.6, §9) — covered by a dedicated Platform Phase (`ingestion-bim`) because the IFC parsing + two-way sync surface is substantially larger than any Phase C modality. Explicitly NOT in this plan.
- **Kitchen-sink / docs pages** for any ingestion modality — lives in the `apps/*` phases, not here.
- **React adapter parity** — ingestion is framework-agnostic data layer. There is no adapter variant. `ingestion-forms` binds to `blocks-forms` (Blazor); a future `blocks-forms-react` would warrant a sibling `ingestion-forms-react` or a generalization; out of scope.

This restraint is deliberate (see D-SCOPE-RESTRAINT).

---

## Key Decisions

**D-PIPELINE-SHAPE** — Ingestion adopts a single generic contract with modality-specific implementations: `IIngestionPipeline<TInput>` where `TInput` is the modality-specific input (e.g., `FormSubmission<TModel>`, `SpreadsheetUpload`, `AudioBlob`, `SensorBatch`, `ImageUpload`). Each modality binds one or more `IIngestionPipeline<TInput>` implementations. Cross-cutting concerns (validation, dedup, virus-scanning) compose as `IIngestionMiddleware<TInput>` in an ordered chain.

Rationale: modality-specific contracts (`IFormIngestor`, `ISpreadsheetIngestor`, etc.) would force consumers to wire N different middleware chains for N modalities. A single generic contract + middleware model mirrors ASP.NET Core's middleware chain and keeps cross-cutting concerns DRY. The generic parameter keeps inputs strongly typed without a runtime `object`-cast.

**D-BLOB-BOUNDARY** — The threshold for "inline in entity body" vs "stored in blob, referenced by CID" is **64 KiB**. Anything ≤ 64 KiB of binary / Base64 content stays in the entity body JSON; anything > 64 KiB routes through `IBlobStore.PutAsync` and the entity body carries the `Cid`. The threshold is configurable per-schema via a schema-registry hint (`blobThreshold` property on the schema descriptor — spec §3.4 schema registry); 64 KiB is the default.

Rationale: 64 KiB is the common "JSONB page size" heuristic in Postgres and avoids blob-store churn for small values. Voice recordings (even 10-second clips), drone images, sensor batches, scanned PDFs all exceed 64 KiB; forms and single-field values stay inline. See ipfs-evaluation.md §2 (mapping-to-sunfish table) — "Forms, spreadsheets: overkill" and "Imagery: primary use case" explicitly note the size boundary.

**D-AI-AUTHORING** — Typeform-AI-style form authoring ships as a separate, **optional** package (`Sunfish.Ingestion.Forms.Ai`) in a future phase, not in Phase C. The Phase C `ingestion-forms` package delivers the runtime submission path only (form submitted → entity minted). The AI-authoring extension depends on a pluggable LLM adapter with a `NoOp` default; production consumers configure OpenAI / Anthropic / Azure OpenAI via `HttpClient` (no SDK coupling, same pattern as `ingestion-voice`).

Rationale: AI authoring and AI-assisted ingestion (spec §7.3 voice case) are distinct from **ingestion** itself. Shipping them inside Phase C would bloat scope with an LLM integration surface that doesn't belong in a data-layer package. External-references.md §1.1 (Typeform AI) explicitly classifies this as a Phase-5-blocks-forms follow-up, not a Phase C deliverable.

**D-VOICE-PROVIDER** — Ship **three** `IVoiceTranscriber` adapters:
1. **OpenAI Whisper** — `HttpClient` POST to `https://api.openai.com/v1/audio/transcriptions`. Multipart form upload; JSON response parsed with `System.Text.Json`.
2. **Azure Speech** — `HttpClient` POST to `https://<region>.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1`. WAV/PCM upload; JSON response parsed with `System.Text.Json`.
3. **NoOp** — returns a `TranscriptionResult` whose `Transcript` is provided by the caller (for scenarios where transcription happens elsewhere — on-device, via a different vendor, by the user typing).

**No direct dependency on `Azure.AI.*` or `OpenAI.*` SDKs.** All external communication is `HttpClient`. Rationale: Foundation (and the adjacent ingestion packages) must stay lean and avoid transitively pinning vendor SDK version graphs. `HttpClient` is in the BCL.

**D-IMAGERY-METADATA** — EXIF + GPS extraction via **`MetadataExtractor`** (MIT, actively maintained — github.com/drewnoakes/metadata-extractor-dotnet). Supports JPEG, TIFF, PNG, HEIC (Apple's iPhone-era container), DNG (Adobe digital negative), and dozens of RAW formats. Returns structured dictionaries keyed by `Directory/Tag`; Sunfish extracts canonical fields (GPS lat/long, capture timestamp UTC, camera make/model, exposure, focal length) into a typed `ImageryMetadata` record that lands in the entity body.

**D-SENSOR-SERIALIZATION** — Sensor batches accept **two** canonical wire formats:
- **JSON** — array-of-objects or newline-delimited JSON (NDJSON). Decoded via `System.Text.Json.JsonDocument` in streaming mode using `System.IO.Pipelines`.
- **MessagePack** — binary batch format for high-volume senders. Decoded via `MessagePack-CSharp` (MIT). MessagePack is optional (consumers who only produce JSON don't pay the dependency cost); Phase C ships the JSON decoder and a `NoOp` MessagePack decoder with a parked upgrade path.

**Decision:** Ship the JSON decoder in Phase C. MessagePack support is a parked follow-up (reduces Directory.Packages.props footprint for Phase C; real-world JSON-first IoT gateways dominate).

**D-SATELLITE-PROVIDER** — Satellite imagery ingestion is **API-driven**, not file-upload-driven. The `ISatelliteImageryProvider` contract has three methods (`ListAcquisitionsAsync`, `DownloadAsync`, `GetMetadataAsync`) and ships with a `NoOp` default. Providers (Planet Labs, Maxar, Sentinel Hub, Airbus OneAtlas) are implemented in **separate** downstream packages outside this repo; Phase C delivers only the contract + NoOp so consumers can stand up the pipeline without picking a provider upfront.

Rationale: Provider SDKs are heavy, typically closed-source, and require provider-specific credentials. A contract + NoOp keeps Phase C deployable out-of-the-box without a commercial satellite contract.

**D-POST-INGEST-HOOK** — Every modality emits the post-ingest hook as the last step in its pipeline: `IPostIngestHandler<TIngestResult>.HandleAsync(TIngestResult, CancellationToken)`. ML integrations (crack detection on drone images, anomaly detection on sensor batches, LLM mutation synthesis from voice) plug in here. Phase C ships NO default handlers — consumers register their own. The hook fires **after** the entity is minted and **before** the pipeline returns; failures in the hook do not roll back the entity (entities are the source of truth, per spec §3 kernel design).

**D-SCOPE-RESTRAINT** — Each modality ships a **single canonical pipeline implementation plus its contract surface**. No variants (no "streaming forms," no "multi-sheet XLSX with cross-sheet references," no "drone imagery with tile-set stitching," no "Azure Speech with speaker diarization"). Follow-ups go to the parking lot. If an adapter feels thin, that is by design.

**D-NO-BLAZOR-DEPENDENCY** — Phase 2 established the `HasNoBlazorDependency` invariant for ingestion-shaped packages. None of the six ingestion packages reference Blazor (`Microsoft.AspNetCore.Components.*`). The one caveat: `ingestion-forms` takes a project reference to `Sunfish.Blocks.Forms`, which **does** reference Blazor. This is a transitive reference only — `ingestion-forms` itself imports no Blazor types. Task 2 verifies this with an assembly-level reflection test that asserts `typeof(FormIngestionPipeline<>).Assembly.GetReferencedAssemblies()` contains no `Microsoft.AspNetCore.Components.*` entries. If the transitive reference leaks, we split `blocks-forms` into `blocks-forms-contracts` (framework-agnostic `FormSubmission<TModel>` record) + `blocks-forms` (Razor components depend on contracts).

---

## File Structure

After Phase C, the repo layout under `packages/` is:

```
packages/
  foundation/
    Sunfish.Foundation.csproj
    Blobs/ (IBlobStore, Cid, FileSystemBlobStore — shipped)
    tests/tests.csproj
  ui-core/
  ui-adapters-blazor/
  blocks-forms/ (shipped)
  blocks-tasks/
  blocks-scheduling/
  blocks-assets/
  compat-telerik/
  ingestion-core/                              ← NEW
    Sunfish.Ingestion.Core.csproj
    IIngestionPipeline.cs
    IngestionContext.cs
    IngestionResult.cs                         ← discriminated union of Success/Failure
    IngestOutcome.cs                           ← failure reason enum + details
    Middleware/
      IIngestionMiddleware.cs
      IngestionPipelineBuilder.cs
      ValidationMiddleware.cs                  ← default: runs IValidator<TInput> if registered
      DeduplicationMiddleware.cs               ← default: compares Cid of input against seen cache
    Hooks/
      IPostIngestHandler.cs
    DependencyInjection/
      SunfishIngestionServiceCollectionExtensions.cs
    tests/
      tests.csproj
      IngestionResultTests.cs
      MiddlewareChainTests.cs
      DeduplicationMiddlewareTests.cs
  ingestion-forms/                             ← NEW
    Sunfish.Ingestion.Forms.csproj
    FormIngestionPipeline.cs
    FormSubmission.cs                          ← record { Model, FormBlockState, SubmittedAtUtc }
    FormIngestionResult.cs
    DependencyInjection/
      FormsServiceCollectionExtensions.cs      ← IngestionBuilder.WithForms()
    tests/
      tests.csproj
      FormIngestionPipelineTests.cs
      NoBlazorReferenceInvariantTests.cs
  ingestion-spreadsheets/                      ← NEW
    Sunfish.Ingestion.Spreadsheets.csproj
    SpreadsheetIngestionPipeline.cs
    SpreadsheetUpload.cs                       ← record { Stream, Filename, Kind (Csv | Xlsx) }
    ColumnMapping.cs                           ← record { SourceHeader, TargetField, TypeCoercion }
    Importers/
      CsvRowImporter.cs                        ← CsvHelper
      XlsxRowImporter.cs                       ← ClosedXML
      IRowImporter.cs
    Coercion/
      TypeCoercer.cs                           ← string → int/decimal/DateTime/enum
    DependencyInjection/
      SpreadsheetsServiceCollectionExtensions.cs
    tests/
      tests.csproj
      fixtures/
        units-small.csv                        ← 3 rows, clean
        units-with-errors.csv                  ← 3 rows, 1 row has non-integer bedrooms
        units-small.xlsx                       ← same shape as units-small.csv, XLSX
      CsvRowImporterTests.cs
      XlsxRowImporterTests.cs
      TypeCoercerTests.cs
      SpreadsheetIngestionPipelineTests.cs     ← end-to-end CSV + XLSX
  ingestion-voice/                             ← NEW
    Sunfish.Ingestion.Voice.csproj
    VoiceIngestionPipeline.cs
    AudioBlob.cs                               ← record { Stream, Filename, ContentType }
    TranscriptionResult.cs                     ← record { Transcript, Segments[], LanguageCode }
    Transcribers/
      IVoiceTranscriber.cs
      OpenAiWhisperTranscriber.cs              ← HttpClient
      AzureSpeechTranscriber.cs                ← HttpClient
      NoOpTranscriber.cs                       ← returns caller-supplied transcript
    DependencyInjection/
      VoiceServiceCollectionExtensions.cs      ← .WithVoice(Whisper | AzureSpeech | NoOp)
    tests/
      tests.csproj
      fixtures/
        tiny-voice-sample.wav                  ← 1-second WAV, ~44 KB; used by pipeline tests with mocked transcriber
      VoiceIngestionPipelineTests.cs
      OpenAiWhisperTranscriberTests.cs         ← mocked HttpClient
      AzureSpeechTranscriberTests.cs           ← mocked HttpClient
      NoOpTranscriberTests.cs
  ingestion-sensors/                           ← NEW
    Sunfish.Ingestion.Sensors.csproj
    SensorIngestionPipeline.cs
    SensorBatch.cs                             ← record { Stream, Format (Json | MessagePack), ProducerId }
    SensorReading.cs                           ← record { SensorId, TimestampUtc, Kind, Value, Unit }
    Decoders/
      ISensorBatchDecoder.cs
      JsonSensorBatchDecoder.cs                ← System.IO.Pipelines + System.Text.Json
      NoOpMessagePackDecoder.cs                ← returns empty; parking-lot item
    DependencyInjection/
      SensorsServiceCollectionExtensions.cs
    tests/
      tests.csproj
      fixtures/
        batch-small.json                       ← 5 readings, array-of-objects
        batch-ndjson.jsonl                     ← 5 readings, NDJSON format
      JsonSensorBatchDecoderTests.cs
      SensorIngestionPipelineTests.cs
  ingestion-imagery/                           ← NEW
    Sunfish.Ingestion.Imagery.csproj
    ImageryIngestionPipeline.cs
    ImageUpload.cs                             ← record { Stream, Filename, ContentType }
    ImageryMetadata.cs                         ← record { GpsLat?, GpsLong?, CapturedUtc?, Make?, Model?, FocalLengthMm? }
    Metadata/
      IImageryMetadataExtractor.cs
      ExifImageryMetadataExtractor.cs          ← MetadataExtractor
    DependencyInjection/
      ImageryServiceCollectionExtensions.cs
    tests/
      tests.csproj
      fixtures/
        sample-with-gps.jpg                    ← bundled JPEG with known EXIF (docs in test-readme.md)
      ExifImageryMetadataExtractorTests.cs
      ImageryIngestionPipelineTests.cs
  ingestion-satellite/                         ← NEW
    Sunfish.Ingestion.Satellite.csproj
    SatelliteIngestionPipeline.cs
    SatelliteAcquisition.cs                    ← record { ProviderId, AcquiredUtc, Bbox, CloudCoverPct }
    Providers/
      ISatelliteImageryProvider.cs
      NoOpSatelliteImageryProvider.cs
    DependencyInjection/
      SatelliteServiceCollectionExtensions.cs
    tests/
      tests.csproj
      SatelliteIngestionPipelineTests.cs       ← exercises NoOp provider
      NoOpSatelliteImageryProviderTests.cs
```

Files to update:
- `Sunfish.slnx` — add 12 csproj entries (6 packages + 6 tests)
- `Directory.Packages.props` — add pinned versions for `ClosedXML`, `CsvHelper`, `MetadataExtractor`, `System.Text.Json` (already transitive, made explicit), `NSubstitute` (already present)

---

## Task 0: Branch setup and NuGet pinning

- [ ] **Step 1: Create phase branch**

```bash
cd "C:/Projects/Sunfish"
git switch -c feat/platform-phase-C-input-modalities
```

Expected: new branch based on the current workspace branch (main or the Phase 9 integration branch).

- [ ] **Step 2: Verify baseline build**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. All existing tests pass.

- [ ] **Step 3: Add pinned package versions to Directory.Packages.props**

Edit `C:/Projects/Sunfish/Directory.Packages.props`:

```xml
<ItemGroup>
  <!-- Phase C ingestion dependencies -->
  <PackageVersion Include="ClosedXML" Version="0.105.0" />
  <PackageVersion Include="CsvHelper" Version="34.0.0" />
  <PackageVersion Include="MetadataExtractor" Version="2.8.3" />
</ItemGroup>
```

Rationale: Pin mid-2026-compatible stable versions. ClosedXML 0.105 is the first 1.0-track release; CsvHelper 34 is the current LTS line; MetadataExtractor 2.8.x is the active maintenance line.

- [ ] **Step 4: Commit NuGet pinning**

```bash
git add Directory.Packages.props
git commit -m "chore(deps): pin ClosedXML, CsvHelper, MetadataExtractor for Platform Phase C"
```

---

## Task 1: Create ingestion-core package (shared infrastructure)

**Files:**
- Create: `packages/ingestion-core/Sunfish.Ingestion.Core.csproj`
- Create: `packages/ingestion-core/IIngestionPipeline.cs`
- Create: `packages/ingestion-core/IngestionContext.cs`
- Create: `packages/ingestion-core/IngestionResult.cs`
- Create: `packages/ingestion-core/IngestOutcome.cs`
- Create: `packages/ingestion-core/Middleware/IIngestionMiddleware.cs`
- Create: `packages/ingestion-core/Middleware/IngestionPipelineBuilder.cs`
- Create: `packages/ingestion-core/Middleware/ValidationMiddleware.cs`
- Create: `packages/ingestion-core/Middleware/DeduplicationMiddleware.cs`
- Create: `packages/ingestion-core/Hooks/IPostIngestHandler.cs`
- Create: `packages/ingestion-core/DependencyInjection/SunfishIngestionServiceCollectionExtensions.cs`
- Create: `packages/ingestion-core/tests/*`

- [ ] **Step 1: Create Sunfish.Ingestion.Core.csproj**

```xml
<!-- packages/ingestion-core/Sunfish.Ingestion.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Ingestion.Core</PackageId>
    <Description>Shared ingestion pipeline contract, middleware, and error model for Sunfish modality adapters (spec §7).</Description>
    <PackageTags>sunfish;ingestion;pipeline;middleware</PackageTags>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Sunfish.Ingestion.Core.Tests" />
    <InternalsVisibleTo Include="Sunfish.Ingestion.Forms.Tests" />
    <InternalsVisibleTo Include="Sunfish.Ingestion.Spreadsheets.Tests" />
    <InternalsVisibleTo Include="Sunfish.Ingestion.Voice.Tests" />
    <InternalsVisibleTo Include="Sunfish.Ingestion.Sensors.Tests" />
    <InternalsVisibleTo Include="Sunfish.Ingestion.Imagery.Tests" />
    <InternalsVisibleTo Include="Sunfish.Ingestion.Satellite.Tests" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create IIngestionPipeline.cs**

```csharp
// packages/ingestion-core/IIngestionPipeline.cs
namespace Sunfish.Ingestion.Core;

/// <summary>
/// Canonical ingestion pipeline contract. Every modality implements this with its
/// modality-specific <typeparamref name="TInput"/>. Cross-cutting concerns compose via
/// <see cref="Middleware.IIngestionMiddleware{TInput}"/>.
/// </summary>
/// <remarks>
/// See spec §7.7 (Unified Ingestion Pipeline). Each modality's output is an
/// <see cref="IngestionResult{T}"/> whose <c>Entity</c> + <c>Events</c> tuple is
/// ready for the kernel entity API.
/// </remarks>
public interface IIngestionPipeline<TInput>
{
    ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        TInput input,
        IngestionContext context,
        CancellationToken ct = default);
}

/// <summary>
/// A minted entity + the events emitted by the ingestion, plus any blob CIDs created.
/// </summary>
public sealed record IngestedEntity(
    string EntityId,
    string SchemaId,
    IReadOnlyDictionary<string, object?> Body,
    IReadOnlyList<IngestedEvent> Events,
    IReadOnlyList<Sunfish.Foundation.Blobs.Cid> BlobCids);

public sealed record IngestedEvent(
    string Kind,
    IReadOnlyDictionary<string, object?> Payload,
    DateTime OccurredUtc);
```

- [ ] **Step 3: Create IngestionContext.cs**

```csharp
// packages/ingestion-core/IngestionContext.cs
namespace Sunfish.Ingestion.Core;

/// <summary>
/// Per-ingestion context — actor, tenant, correlation. Passed through the middleware chain
/// and available to post-ingest hooks. Immutable; copy-on-write via <see cref="With"/>.
/// </summary>
public sealed record IngestionContext(
    string TenantId,
    string ActorId,
    string CorrelationId,
    DateTime StartedUtc)
{
    public static IngestionContext NewCorrelation(string tenantId, string actorId) =>
        new(tenantId, actorId, Guid.NewGuid().ToString("n"), DateTime.UtcNow);

    public IngestionContext With(string? tenantId = null, string? actorId = null) =>
        this with { TenantId = tenantId ?? TenantId, ActorId = actorId ?? ActorId };
}
```

- [ ] **Step 4: Create IngestionResult.cs + IngestOutcome.cs**

```csharp
// packages/ingestion-core/IngestionResult.cs
namespace Sunfish.Ingestion.Core;

/// <summary>
/// Result of a single ingestion operation. Never throws for domain-level failures —
/// callers discriminate on <see cref="Outcome"/> and inspect <see cref="Value"/> or
/// <see cref="Failure"/> accordingly. Exceptions indicate infrastructure failure only.
/// </summary>
public sealed record IngestionResult<T>(
    IngestOutcome Outcome,
    T? Value,
    IngestionFailure? Failure)
{
    public bool IsSuccess => Outcome == IngestOutcome.Success;

    public static IngestionResult<T> Success(T value) => new(IngestOutcome.Success, value, null);
    public static IngestionResult<T> Fail(IngestOutcome outcome, string message, IReadOnlyList<string>? details = null) =>
        new(outcome, default, new IngestionFailure(outcome, message, details ?? Array.Empty<string>()));
}

public sealed record IngestionFailure(IngestOutcome Outcome, string Message, IReadOnlyList<string> Details);

// packages/ingestion-core/IngestOutcome.cs
namespace Sunfish.Ingestion.Core;

/// <summary>Discriminated failure mode. Consumers branch on this enum.</summary>
public enum IngestOutcome
{
    Success,
    ValidationFailed,        // input failed schema validation
    Duplicate,               // dedup middleware matched a prior input
    TooLarge,                // exceeds configured size limit
    Quarantined,             // virus-scan middleware flagged
    ProviderUnavailable,     // transcriber / satellite API down
    ProviderFailed,          // transcriber / satellite API returned error
    UnsupportedFormat,       // unknown CSV dialect, unknown image codec, etc.
    InternalError,           // unexpected — logged, surfaced as failure
}
```

- [ ] **Step 5: Create Middleware/IIngestionMiddleware.cs + PipelineBuilder + defaults**

```csharp
// packages/ingestion-core/Middleware/IIngestionMiddleware.cs
namespace Sunfish.Ingestion.Core.Middleware;

public delegate ValueTask<IngestionResult<IngestedEntity>> IngestionDelegate<TInput>(
    TInput input, IngestionContext context, CancellationToken ct);

public interface IIngestionMiddleware<TInput>
{
    ValueTask<IngestionResult<IngestedEntity>> InvokeAsync(
        TInput input,
        IngestionContext context,
        IngestionDelegate<TInput> next,
        CancellationToken ct);
}
```

```csharp
// packages/ingestion-core/Middleware/IngestionPipelineBuilder.cs
namespace Sunfish.Ingestion.Core.Middleware;

public sealed class IngestionPipelineBuilder<TInput>
{
    private readonly List<IIngestionMiddleware<TInput>> _middlewares = new();

    public IngestionPipelineBuilder<TInput> Use(IIngestionMiddleware<TInput> middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    public IngestionDelegate<TInput> Build(IngestionDelegate<TInput> terminal)
    {
        IngestionDelegate<TInput> current = terminal;
        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var mw = _middlewares[i];
            var next = current;
            current = (input, ctx, ct) => mw.InvokeAsync(input, ctx, next, ct);
        }
        return current;
    }
}
```

Defaults:

```csharp
// packages/ingestion-core/Middleware/ValidationMiddleware.cs
// Runs IValidator<TInput> if one is registered; otherwise pass-through.
// Validators are NOT defined in this package — consumers supply them.

// packages/ingestion-core/Middleware/DeduplicationMiddleware.cs
// Computes a Cid of the input's canonical bytes; if already seen within the dedup window,
// returns IngestionResult.Fail(Duplicate, ...). Uses an in-memory LRU cache by default.
```

- [ ] **Step 6: Create Hooks/IPostIngestHandler.cs (D-POST-INGEST-HOOK)**

```csharp
// packages/ingestion-core/Hooks/IPostIngestHandler.cs
namespace Sunfish.Ingestion.Core.Hooks;

public interface IPostIngestHandler<TResult>
{
    ValueTask HandleAsync(TResult result, IngestionContext context, CancellationToken ct);
}
```

- [ ] **Step 7: Create DI extensions (SunfishIngestionServiceCollectionExtensions.cs)**

```csharp
// packages/ingestion-core/DependencyInjection/SunfishIngestionServiceCollectionExtensions.cs
namespace Sunfish.Ingestion.Core.DependencyInjection;

public static class SunfishIngestionServiceCollectionExtensions
{
    public static IngestionBuilder AddSunfishIngestion(this IServiceCollection services)
    {
        return new IngestionBuilder(services);
    }
}

public sealed class IngestionBuilder
{
    public IServiceCollection Services { get; }
    public IngestionBuilder(IServiceCollection services) { Services = services; }
}
```

The per-modality `WithX()` extension methods live in each modality's DI namespace (Task 2–7).

- [ ] **Step 8: Create tests/**

`tests.csproj` matches the blocks-forms test project pattern. NSubstitute is a `<PackageReference>`.

Tests:
- `IngestionResultTests.cs` — Success / Fail factories, IsSuccess flag, Failure payload. **4 tests.**
- `MiddlewareChainTests.cs` — chain order, early termination (short-circuit on failure), exception propagation. **5 tests.**
- `DeduplicationMiddlewareTests.cs` — duplicate detection, window expiry, empty input edge case. **3 tests.**

Expected: **12 tests** for ingestion-core.

- [ ] **Step 9: Build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ingestion-core/Sunfish.Ingestion.Core.csproj
dotnet test packages/ingestion-core/tests/tests.csproj
```

Expected: 0 errors, 12 tests passing.

- [ ] **Step 10: Commit**

```bash
git add packages/ingestion-core/
git commit -m "feat(ingestion-core): IIngestionPipeline, middleware, IngestionResult, 12 tests"
```

---

## Task 2: Create ingestion-forms package

**Scope:** Wraps `FormBlock<TModel>` submissions. No blob-store use — form data is small structured JSON. The pipeline takes a `FormSubmission<TModel>` and produces an `IngestedEntity` whose body is the serialized model.

**Files:**
- Create: `packages/ingestion-forms/Sunfish.Ingestion.Forms.csproj`
- Create: `packages/ingestion-forms/FormSubmission.cs`
- Create: `packages/ingestion-forms/FormIngestionPipeline.cs`
- Create: `packages/ingestion-forms/FormIngestionResult.cs`
- Create: `packages/ingestion-forms/DependencyInjection/FormsServiceCollectionExtensions.cs`
- Create: `packages/ingestion-forms/tests/*`

- [ ] **Step 1: Create csproj**

Clone `ingestion-core/Sunfish.Ingestion.Core.csproj` with these substitutions:
- `PackageId` → `Sunfish.Ingestion.Forms`
- `Description` → `Form submission ingestion for Sunfish — wraps FormBlock<TModel> submissions into kernel entity updates.`
- ProjectReferences add: `..\blocks-forms\Sunfish.Blocks.Forms.csproj` AND `..\ingestion-core\Sunfish.Ingestion.Core.csproj`

- [ ] **Step 2: Create FormSubmission.cs**

```csharp
// packages/ingestion-forms/FormSubmission.cs
namespace Sunfish.Ingestion.Forms;

/// <summary>
/// A form submission captured by a <c>FormBlock&lt;TModel&gt;</c>. The model holds the
/// user's entered values; the block's state holder records submission lifecycle.
/// </summary>
public sealed record FormSubmission<TModel>(
    TModel Model,
    string SchemaId,
    DateTime SubmittedAtUtc) where TModel : class;
```

- [ ] **Step 3: Create FormIngestionPipeline.cs**

```csharp
// packages/ingestion-forms/FormIngestionPipeline.cs
using System.Text.Json;
using Sunfish.Ingestion.Core;

namespace Sunfish.Ingestion.Forms;

public sealed class FormIngestionPipeline<TModel> : IIngestionPipeline<FormSubmission<TModel>>
    where TModel : class
{
    public ValueTask<IngestionResult<IngestedEntity>> IngestAsync(
        FormSubmission<TModel> input,
        IngestionContext context,
        CancellationToken ct = default)
    {
        // Serialize the model to a dictionary body; form data is already structured.
        var json = JsonSerializer.SerializeToElement(input.Model);
        var body = json.EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value.Clone());

        var entity = new IngestedEntity(
            EntityId: Guid.NewGuid().ToString("n"),
            SchemaId: input.SchemaId,
            Body: body,
            Events: new[] { new IngestedEvent("entity.created", body, input.SubmittedAtUtc) },
            BlobCids: Array.Empty<Sunfish.Foundation.Blobs.Cid>());

        return ValueTask.FromResult(IngestionResult<IngestedEntity>.Success(entity));
    }
}
```

- [ ] **Step 4: Create DI extension**

```csharp
// packages/ingestion-forms/DependencyInjection/FormsServiceCollectionExtensions.cs
namespace Sunfish.Ingestion.Forms.DependencyInjection;

public static class FormsServiceCollectionExtensions
{
    public static IngestionBuilder WithForms(this IngestionBuilder builder)
    {
        builder.Services.AddSingleton(typeof(IIngestionPipeline<>), typeof(FormIngestionPipeline<>));
        return builder;
    }
}
```

- [ ] **Step 5: Create tests**

- `FormIngestionPipelineTests.cs` — **4 tests:**
  - Ingest(Model) → Success with entity body matching model properties
  - Entity.Events contains one `entity.created` event
  - Entity.BlobCids is empty (form data is inline)
  - Entity.SchemaId propagates from input

- `NoBlazorReferenceInvariantTests.cs` — **1 test:**
  - Reflection check: `typeof(FormIngestionPipeline<>).Assembly.GetReferencedAssemblies()` must not contain any name starting with `Microsoft.AspNetCore.Components`. Asserts D-NO-BLAZOR-DEPENDENCY: even though `blocks-forms` is a project reference, ingestion-forms itself does not import Blazor types.

Expected: **5 tests** for ingestion-forms.

- [ ] **Step 6: Build, test, commit**

```bash
dotnet build packages/ingestion-forms/Sunfish.Ingestion.Forms.csproj
dotnet test packages/ingestion-forms/tests/tests.csproj
git add packages/ingestion-forms/
git commit -m "feat(ingestion-forms): FormIngestionPipeline wraps FormBlock submissions, 5 tests"
```

---

## Task 3: Create ingestion-spreadsheets package

**Scope:** CSV (CsvHelper) + XLSX (ClosedXML) importers. Header row → target field mapping via explicit `ColumnMapping` records (no fancy AI-guessed mapping in Phase C). Per-row entity minting; type coercion from string cells.

**Files:**
- Create: `packages/ingestion-spreadsheets/Sunfish.Ingestion.Spreadsheets.csproj`
- Create: `packages/ingestion-spreadsheets/SpreadsheetUpload.cs`
- Create: `packages/ingestion-spreadsheets/ColumnMapping.cs`
- Create: `packages/ingestion-spreadsheets/Importers/{IRowImporter,CsvRowImporter,XlsxRowImporter}.cs`
- Create: `packages/ingestion-spreadsheets/Coercion/TypeCoercer.cs`
- Create: `packages/ingestion-spreadsheets/SpreadsheetIngestionPipeline.cs`
- Create: `packages/ingestion-spreadsheets/DependencyInjection/SpreadsheetsServiceCollectionExtensions.cs`
- Create: `packages/ingestion-spreadsheets/tests/*` (including fixture files)

- [ ] **Step 1: Create csproj**

Base on ingestion-core csproj. ProjectReferences: `..\ingestion-core\Sunfish.Ingestion.Core.csproj`. PackageReferences: `ClosedXML`, `CsvHelper`.

- [ ] **Step 2: Create ColumnMapping.cs**

```csharp
// packages/ingestion-spreadsheets/ColumnMapping.cs
namespace Sunfish.Ingestion.Spreadsheets;

/// <summary>
/// Maps a source column (spreadsheet header) to a target schema field.
/// <see cref="TypeCoercion"/> drives string→typed conversion.
/// </summary>
public sealed record ColumnMapping(
    string SourceHeader,
    string TargetField,
    CoercionKind TypeCoercion);

public enum CoercionKind
{
    String,
    Integer,
    Decimal,
    DateTimeUtc,
    Boolean,
    EnumIgnoreCase,
}
```

- [ ] **Step 3: Create Importers/IRowImporter.cs + CsvRowImporter.cs + XlsxRowImporter.cs**

`IRowImporter` exposes `IAsyncEnumerable<IReadOnlyDictionary<string, string>> ReadRowsAsync(Stream, CancellationToken)`. Each row is a header→cell-string map.

`CsvRowImporter`: uses `CsvHelper.CsvReader` in `CultureInfo.InvariantCulture`. Reads header row, then streams rows as dictionaries.

`XlsxRowImporter`: uses `ClosedXML.Excel.XLWorkbook.Open(stream)`. Takes the first worksheet. First row = headers. Iterates rows, yielding header→cell-string-value maps. Honors empty cells (emits empty string).

- [ ] **Step 4: Create Coercion/TypeCoercer.cs**

```csharp
// packages/ingestion-spreadsheets/Coercion/TypeCoercer.cs
namespace Sunfish.Ingestion.Spreadsheets.Coercion;

public static class TypeCoercer
{
    public static IngestionResult<object?> TryCoerce(string raw, CoercionKind kind)
    {
        // String: pass through (including empty → null when configured)
        // Integer: int.TryParse(invariant); fail with ValidationFailed on failure
        // Decimal: decimal.TryParse(invariant); same
        // DateTimeUtc: DateTime.TryParse(invariant, Roundtrip); convert to UTC
        // Boolean: accepts "true/false/yes/no/1/0" case-insensitive
        // EnumIgnoreCase: case-insensitive matching (not resolving enum here; coercion produces a string)
        // Each failure returns IngestionResult.Fail(ValidationFailed, $"Could not coerce '{raw}' to {kind}")
        throw new NotImplementedException("Implement per above comment.");
    }
}
```

- [ ] **Step 5: Create SpreadsheetUpload.cs + SpreadsheetIngestionPipeline.cs**

```csharp
// SpreadsheetUpload.cs
public sealed record SpreadsheetUpload(
    Stream Content,
    string Filename,
    SpreadsheetKind Kind,
    string SchemaId,
    IReadOnlyList<ColumnMapping> Mappings);

public enum SpreadsheetKind { Csv, Xlsx }
```

The pipeline:
1. Selects `CsvRowImporter` or `XlsxRowImporter` based on `Kind`.
2. Reads rows; for each row, applies `Mappings` to build the target body.
3. Uses `TypeCoercer` per mapping.
4. On any row's validation failure, collects all row-level failures into a single `IngestionResult.Fail(ValidationFailed, ..., rowErrors)` — the whole batch fails atomically (spec §7.2 says "Import commits 1,200 units atomically").
5. On success: emits ONE `IngestedEntity` per row (returned as a batch — see design note below).

**Design note — batch return shape:** `IIngestionPipeline<T>` returns a single `IngestionResult<IngestedEntity>`. For spreadsheets we need N entities. Option: the spreadsheet pipeline's `IngestedEntity.Events` contains N `entity.created` events, but the top-level `Entity` is a synthetic `bulk_import_session` entity that references the per-row entities. This preserves the single-result contract and matches spec §7.2's "single `bulk_import_session` audit record." Per-row entities travel as nested events with the full row body in the payload.

- [ ] **Step 6: Create fixture spreadsheets (3 files)**

- `tests/fixtures/units-small.csv` — 3 rows, columns: `Building,Unit,Bedrooms,SqFt`. All rows clean.
- `tests/fixtures/units-with-errors.csv` — 3 rows, one row has `Bedrooms="studio"` (matches spec §7.2 example).
- `tests/fixtures/units-small.xlsx` — same shape as `units-small.csv`, built programmatically at test time (via ClosedXML) OR committed as a binary fixture.

Decision: build `units-small.xlsx` **programmatically in a test fixture setup helper** rather than committing a binary — keeps the repo binary-free and guarantees ClosedXML compatibility at the pinned version.

- [ ] **Step 7: Create tests**

- `CsvRowImporterTests.cs` — parses fixture, asserts row count, header names, cell values. **3 tests.**
- `XlsxRowImporterTests.cs` — same against programmatically-built XLSX. **3 tests.**
- `TypeCoercerTests.cs` — Integer, Decimal, DateTimeUtc, Boolean, failure cases. **8 tests.**
- `SpreadsheetIngestionPipelineTests.cs` — end-to-end CSV happy path; XLSX happy path; error-row propagation; empty spreadsheet edge case. **5 tests.**

Expected: **19 tests** for ingestion-spreadsheets.

- [ ] **Step 8: Build, test, commit**

```bash
dotnet build packages/ingestion-spreadsheets/Sunfish.Ingestion.Spreadsheets.csproj
dotnet test packages/ingestion-spreadsheets/tests/tests.csproj
git add packages/ingestion-spreadsheets/
git commit -m "feat(ingestion-spreadsheets): CSV (CsvHelper) + XLSX (ClosedXML) ingest, 19 tests"
```

---

## Task 4: Create ingestion-voice package

**Scope:** Audio blob in → transcript + entity body out. Three `IVoiceTranscriber` adapters: OpenAI Whisper, Azure Speech, NoOp. Raw audio bytes routed through `IBlobStore.PutAsync`; entity body references the CID + carries the transcript.

**Files:**
- Create: `packages/ingestion-voice/Sunfish.Ingestion.Voice.csproj`
- Create: `packages/ingestion-voice/AudioBlob.cs`
- Create: `packages/ingestion-voice/TranscriptionResult.cs`
- Create: `packages/ingestion-voice/Transcribers/{IVoiceTranscriber,OpenAiWhisperTranscriber,AzureSpeechTranscriber,NoOpTranscriber}.cs`
- Create: `packages/ingestion-voice/VoiceIngestionPipeline.cs`
- Create: `packages/ingestion-voice/DependencyInjection/VoiceServiceCollectionExtensions.cs`
- Create: `packages/ingestion-voice/tests/*`

- [ ] **Step 1: Create csproj**

ProjectReferences: `..\ingestion-core\Sunfish.Ingestion.Core.csproj`. No external NuGets — `HttpClient` is in the BCL.

- [ ] **Step 2: Create AudioBlob.cs + TranscriptionResult.cs**

```csharp
// AudioBlob.cs
public sealed record AudioBlob(
    Stream Content,
    string Filename,
    string ContentType,       // e.g., "audio/wav", "audio/mpeg", "audio/m4a"
    string SchemaId);

// TranscriptionResult.cs
public sealed record TranscriptionResult(
    string Transcript,
    IReadOnlyList<TranscriptSegment> Segments,
    string LanguageCode);     // e.g., "en", "en-US"

public sealed record TranscriptSegment(
    double StartSeconds,
    double EndSeconds,
    string Text);
```

- [ ] **Step 3: Create IVoiceTranscriber.cs + adapters**

```csharp
public interface IVoiceTranscriber
{
    ValueTask<IngestionResult<TranscriptionResult>> TranscribeAsync(
        AudioBlob audio, CancellationToken ct);
}
```

**OpenAiWhisperTranscriber:** takes `HttpClient` (configured) + `string apiKey`. `POST https://api.openai.com/v1/audio/transcriptions` with multipart form: `file`, `model=whisper-1`, `response_format=verbose_json` (gives segments). Parses JSON; maps to `TranscriptionResult`. On HTTP failure: `IngestionResult.Fail(ProviderFailed, ...)`.

**AzureSpeechTranscriber:** takes `HttpClient` + `string region` + `string subscriptionKey`. `POST https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language={lang}`. Ocp-Apim-Subscription-Key header. Streams audio as request body. Parses JSON.

**NoOpTranscriber:** constructor takes a pre-supplied transcript; `TranscribeAsync` returns it verbatim. For on-device or third-party-transcribed scenarios.

- [ ] **Step 4: Create VoiceIngestionPipeline.cs**

Flow (ctor `(IBlobStore blobs, IVoiceTranscriber transcriber)`):
1. Buffer `input.Content` to memory bytes (streaming path is a parking-lot item).
2. `cid = await blobs.PutAsync(bytes, ct)` — D-BLOB-BOUNDARY: audio always > 64 KiB.
3. `transcript = await transcriber.TranscribeAsync(audio-with-cloned-stream, ct)`. On `!IsSuccess`, propagate `Outcome`, `Message`, `Details` into a failed `IngestionResult<IngestedEntity>`.
4. Build entity body: `{ audioBlobCid, transcript, segments, languageCode, capturedAtUtc }`.
5. Emit one `voice.ingested` event with the same body.
6. Return `IngestionResult.Success(entity)` with `entity.BlobCids = [cid]`.

- [ ] **Step 5: Create DI extension**

`VoiceServiceCollectionExtensions.WithVoice(IngestionBuilder, TranscriberKind)` registers the selected `IVoiceTranscriber` (switch on `TranscriberKind.OpenAiWhisper | AzureSpeech | NoOp`) plus the `VoiceIngestionPipeline`. `HttpClient` factory + API-key + region come from consumer-configured `IOptions<VoiceOptions>`.

- [ ] **Step 6: Create fixture**

`tests/fixtures/tiny-voice-sample.wav` — 1-second silent WAV, ~44 KB (44.1 kHz, 16-bit, mono). Used by pipeline tests; the WAV is never actually transcribed — tests use mocked `IVoiceTranscriber`. Bundle as binary.

- [ ] **Step 7: Create tests**

- `VoiceIngestionPipelineTests.cs` — **5 tests:**
  - Happy path: mocked transcriber returns `TranscriptionResult`; entity body contains CID + transcript
  - Transcriber failure propagates (ProviderFailed)
  - Audio bytes persisted to IBlobStore with correct CID
  - Multiple invocations with same audio → same CID (IBlobStore dedup verification)
  - Empty audio stream → ValidationFailed
- `OpenAiWhisperTranscriberTests.cs` — **4 tests (mocked HttpClient):** 200 OK happy path, 401 unauthorized → ProviderFailed, 429 rate limit → ProviderUnavailable, response-body JSON parse error → ProviderFailed.
- `AzureSpeechTranscriberTests.cs` — **4 tests (mocked HttpClient):** parallel to Whisper — happy, 401, 429, malformed response.
- `NoOpTranscriberTests.cs` — **2 tests:** returns pre-supplied transcript; empty transcript edge case.

Expected: **15 tests** for ingestion-voice.

- [ ] **Step 8: Build, test, commit**

```bash
dotnet build packages/ingestion-voice/Sunfish.Ingestion.Voice.csproj
dotnet test packages/ingestion-voice/tests/tests.csproj
git add packages/ingestion-voice/
git commit -m "feat(ingestion-voice): IVoiceTranscriber + Whisper/Azure/NoOp adapters + pipeline, 15 tests"
```

---

## Task 5: Create ingestion-sensors package

**Scope:** High-volume time-series batches. JSON format in Phase C (MessagePack parked). Decoder uses `System.IO.Pipelines` for efficient streaming. Batch → `SensorReading[]`; large batches chunked into blobs for archival (per D-BLOB-BOUNDARY) + projected to entities for per-reading query.

**Files:**
- Create: `packages/ingestion-sensors/Sunfish.Ingestion.Sensors.csproj`
- Create: `packages/ingestion-sensors/SensorBatch.cs`
- Create: `packages/ingestion-sensors/SensorReading.cs`
- Create: `packages/ingestion-sensors/Decoders/{ISensorBatchDecoder,JsonSensorBatchDecoder,NoOpMessagePackDecoder}.cs`
- Create: `packages/ingestion-sensors/SensorIngestionPipeline.cs`
- Create: `packages/ingestion-sensors/DependencyInjection/SensorsServiceCollectionExtensions.cs`
- Create: `packages/ingestion-sensors/tests/*`

- [ ] **Step 1: Create csproj**

ProjectReferences: `..\ingestion-core\Sunfish.Ingestion.Core.csproj`. No external NuGets (`System.Text.Json` is in the BCL via `Microsoft.NET.Sdk`).

- [ ] **Step 2: Create SensorBatch.cs + SensorReading.cs**

```csharp
public sealed record SensorBatch(
    Stream Content,
    SensorBatchFormat Format,
    string ProducerId,       // gateway / device / tenant identifier
    string SchemaId);

public enum SensorBatchFormat { Json, JsonNdjson, MessagePack }

public sealed record SensorReading(
    string SensorId,
    DateTime TimestampUtc,
    string Kind,             // "temperature", "humidity", "water_leak", "vibration"
    double Value,
    string Unit);            // "celsius", "percent_rh", "boolean", "g"
```

- [ ] **Step 3: Create ISensorBatchDecoder.cs + implementations**

```csharp
public interface ISensorBatchDecoder
{
    IAsyncEnumerable<SensorReading> DecodeAsync(Stream content, CancellationToken ct);
}
```

**JsonSensorBatchDecoder**: wraps `System.IO.Pipelines.PipeReader.Create(content)` + `JsonDocument.ParseAsync` for array-of-objects, or line-by-line parse for NDJSON. Yields `SensorReading` as each object completes — does NOT buffer the whole batch. This matters for multi-megabyte sensor dumps.

**NoOpMessagePackDecoder**: throws `NotSupportedException` with a pointer to the MessagePack parking-lot item. Registered in DI but explicitly unavailable; consumers who try to ingest MessagePack get a clear "MessagePack support is a parking-lot item" error.

- [ ] **Step 4: Create SensorIngestionPipeline.cs**

Flow (same shape as voice pipeline):
1. Buffer the raw batch bytes; `IBlobStore.PutAsync` yields `batchCid`.
2. Select decoder by `Format` (JSON / JsonNdjson / MessagePack → NoOp throws).
3. Collect readings via `await foreach (var reading in decoder.DecodeAsync(stream, ct))`.
4. Build a synthetic "batch" entity: `body = { producerId, batchBlobCid, readingCount, windowStartUtc, windowEndUtc }`. Window times = `min/max` of reading timestamps.
5. Emit one `sensor.reading` event per reading (payload carries `sensorId`, `timestampUtc`, `kind`, `value`, `unit`).
6. Return `IngestionResult<IngestedEntity>.Success(entity)` where `entity.BlobCids = [batchCid]`.

- [ ] **Step 5: Create fixtures**

- `tests/fixtures/batch-small.json` — 5 readings, JSON array. Committed as text.
- `tests/fixtures/batch-ndjson.jsonl` — 5 readings, NDJSON (one per line). Committed as text.

- [ ] **Step 6: Create tests**

- `JsonSensorBatchDecoderTests.cs` — **5 tests:**
  - Decode 5-reading array-of-objects
  - Decode 5-reading NDJSON
  - Empty array yields empty enumerable
  - Malformed JSON yields decoder exception (surfaced as UnsupportedFormat in pipeline)
  - Streaming: reading enumerable does not fully buffer the source (verified with a cooperative Stream that records position at yield time)
- `SensorIngestionPipelineTests.cs` — **4 tests:**
  - Happy path: 5 readings → 1 entity with 5 events, 1 blob CID
  - Empty batch → Success with 0 events (not a failure)
  - NoOpMessagePackDecoder → UnsupportedFormat
  - Blob archival: batch bytes stored in IBlobStore and referenced by entity

Expected: **9 tests** for ingestion-sensors.

- [ ] **Step 7: Build, test, commit**

```bash
dotnet build packages/ingestion-sensors/Sunfish.Ingestion.Sensors.csproj
dotnet test packages/ingestion-sensors/tests/tests.csproj
git add packages/ingestion-sensors/
git commit -m "feat(ingestion-sensors): JSON sensor-batch decoder via Pipelines + pipeline, 9 tests"
```

---

## Task 6: Create ingestion-imagery package

**Scope:** Drone / robot large images. Blob-first: image bytes stored in `IBlobStore` at ingest start (CID becomes the canonical identifier). Then `ExifImageryMetadataExtractor` (via MetadataExtractor) extracts GPS + capture time + camera specs into structured entity body.

**Files:**
- Create: `packages/ingestion-imagery/Sunfish.Ingestion.Imagery.csproj`
- Create: `packages/ingestion-imagery/ImageUpload.cs`
- Create: `packages/ingestion-imagery/ImageryMetadata.cs`
- Create: `packages/ingestion-imagery/Metadata/{IImageryMetadataExtractor,ExifImageryMetadataExtractor}.cs`
- Create: `packages/ingestion-imagery/ImageryIngestionPipeline.cs`
- Create: `packages/ingestion-imagery/DependencyInjection/ImageryServiceCollectionExtensions.cs`
- Create: `packages/ingestion-imagery/tests/*`

- [ ] **Step 1: Create csproj**

ProjectReferences: `..\ingestion-core\Sunfish.Ingestion.Core.csproj`. PackageReferences: `MetadataExtractor`.

- [ ] **Step 2: Create ImageUpload.cs + ImageryMetadata.cs**

```csharp
public sealed record ImageUpload(
    Stream Content,
    string Filename,
    string ContentType,      // "image/jpeg", "image/png", "image/heic"
    string SchemaId);

public sealed record ImageryMetadata(
    double? GpsLatitude,
    double? GpsLongitude,
    DateTime? CapturedUtc,
    string? CameraMake,
    string? CameraModel,
    double? FocalLengthMm,
    int? WidthPx,
    int? HeightPx);
```

- [ ] **Step 3: Create ExifImageryMetadataExtractor.cs**

Uses `MetadataExtractor.ImageMetadataReader.ReadMetadata(stream)`. Iterates directories; pulls canonical tags:
- GPS: `GpsDirectory.GpsLatitude`, `GpsLongitude` (rational DMS → decimal degrees helper)
- Capture: `ExifSubIfdDirectory.DateTimeOriginal` → UTC (assumes EXIF in local; Phase C treats as UTC if no offset present — edge case parking-lot)
- Camera: `ExifIfd0Directory.Make`, `Model`
- Focal length: `ExifSubIfdDirectory.FocalLength`
- Dimensions: `JpegDirectory.ImageWidth`, `ImageHeight` (or PNG equivalent)

Returns `ImageryMetadata` record. Missing tags → `null` fields.

- [ ] **Step 4: Create ImageryIngestionPipeline.cs**

Flow:
1. Read image stream to memory; `IBlobStore.PutAsync` yields `cid` (imagery always exceeds 64 KiB per D-BLOB-BOUNDARY).
2. `metadataExtractor.Extract(stream)` returns `ImageryMetadata` (missing tags → null fields).
3. Build entity body with `imageBlobCid`, `filename`, `contentType`, plus all `ImageryMetadata` fields (gpsLat, gpsLong, capturedUtc, cameraMake, cameraModel, focalLengthMm, widthPx, heightPx).
4. Emit one `imagery.ingested` event with the full body.
5. `entity.BlobCids = [cid]`. Return `IngestionResult.Success(entity)`.

Note: metadata extraction reads a **copy** of the bytes (MetadataExtractor consumes its input stream); tolerate parse-failure by returning all-null `ImageryMetadata` rather than failing the pipeline (the blob is still valuable even if EXIF is corrupt).

- [ ] **Step 5: Create fixture**

`tests/fixtures/sample-with-gps.jpg` — ~20 KB JPEG with known EXIF (GPS lat/long, capture time, camera make/model). Committed as binary. Alongside, a `tests/fixtures/fixture-readme.md` documents the known values so tests can assert against them.

- [ ] **Step 6: Create tests**

- `ExifImageryMetadataExtractorTests.cs` — **5 tests:**
  - GPS lat/long extracted correctly from fixture
  - Capture timestamp extracted
  - Camera make + model extracted
  - Image with no EXIF yields all-null metadata (no exception)
  - Corrupt JPEG yields graceful null-filled `ImageryMetadata` (ValidationFailed OR pass-through — decision: pass-through with null fields; corruption is not a hard failure at ingest)
- `ImageryIngestionPipelineTests.cs` — **4 tests:**
  - Happy path: fixture ingested → entity body has imageBlobCid + gpsLat + gpsLong
  - Identical image ingested twice → same CID (IBlobStore dedup)
  - PNG (no EXIF) → entity has imageBlobCid + null metadata fields
  - Oversized image (fake: 100 MB) → uses `IBlobStore` streaming path (not in-memory buffer) — verified via a cooperative `IBlobStore` mock. **Parking-lot:** current implementation buffers to memory (`input.Content.CopyToAsync(ms)`); true streaming is a parking-lot item. This test is marked `[Fact(Skip="Parking lot — streaming path")]` in Phase C.

Expected: **9 tests** for ingestion-imagery (8 running + 1 skipped).

- [ ] **Step 7: Build, test, commit**

```bash
dotnet build packages/ingestion-imagery/Sunfish.Ingestion.Imagery.csproj
dotnet test packages/ingestion-imagery/tests/tests.csproj
git add packages/ingestion-imagery/
git commit -m "feat(ingestion-imagery): EXIF + GPS via MetadataExtractor + pipeline, 8 tests + 1 skip"
```

---

## Task 7: Create ingestion-satellite package

**Scope:** API-driven satellite imagery. `ISatelliteImageryProvider` contract + NoOp default. No provider-specific implementations in Phase C — those live in downstream packages (e.g., `Sunfish.Ingestion.Satellite.Planet` maintained by consumers). The pipeline orchestrates: provider returns acquisition metadata → pipeline downloads via provider → blob-stores → mints entity.

**Files:**
- Create: `packages/ingestion-satellite/Sunfish.Ingestion.Satellite.csproj`
- Create: `packages/ingestion-satellite/SatelliteAcquisition.cs`
- Create: `packages/ingestion-satellite/Providers/{ISatelliteImageryProvider,NoOpSatelliteImageryProvider}.cs`
- Create: `packages/ingestion-satellite/SatelliteIngestionPipeline.cs`
- Create: `packages/ingestion-satellite/DependencyInjection/SatelliteServiceCollectionExtensions.cs`
- Create: `packages/ingestion-satellite/tests/*`

- [ ] **Step 1: Create csproj**

ProjectReferences: `..\ingestion-core\Sunfish.Ingestion.Core.csproj`. No external NuGets.

- [ ] **Step 2: Create contract and NoOp**

```csharp
public sealed record SatelliteAcquisition(
    string ProviderId,           // "planet-labs", "maxar", "sentinel-hub"
    string AcquisitionId,        // provider's native identifier
    DateTime AcquiredUtc,
    BoundingBox Bbox,            // { MinLat, MinLong, MaxLat, MaxLong }
    double CloudCoverPct,
    string SchemaId);

public sealed record BoundingBox(double MinLat, double MinLong, double MaxLat, double MaxLong);

public interface ISatelliteImageryProvider
{
    ValueTask<IReadOnlyList<SatelliteAcquisition>> ListAcquisitionsAsync(
        BoundingBox bbox, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    ValueTask<Stream> DownloadAsync(
        SatelliteAcquisition acquisition, CancellationToken ct);

    ValueTask<IReadOnlyDictionary<string, object?>> GetMetadataAsync(
        SatelliteAcquisition acquisition, CancellationToken ct);
}

public sealed class NoOpSatelliteImageryProvider : ISatelliteImageryProvider
{
    // ListAcquisitionsAsync: returns empty.
    // DownloadAsync: throws NotSupportedException("NoOp provider — register a real provider.").
    // GetMetadataAsync: returns empty dictionary.
}
```

- [ ] **Step 3: Create SatelliteIngestionPipeline.cs**

```csharp
public sealed class SatelliteIngestionPipeline(
    IBlobStore blobs,
    ISatelliteImageryProvider provider)
    : IIngestionPipeline<SatelliteAcquisition>
{
    // 1. Download stream via provider.
    // 2. Blob-store (CID).
    // 3. Get provider-specific metadata.
    // 4. Build entity body = { providerId, acquisitionId, acquiredUtc, bbox, cloudCoverPct, imageBlobCid, providerMetadata }.
    // On NoOp provider with NotSupportedException → IngestionResult.Fail(ProviderUnavailable, "NoOp provider registered").
}
```

- [ ] **Step 4: Create tests**

- `NoOpSatelliteImageryProviderTests.cs` — **3 tests:**
  - `ListAcquisitionsAsync` returns empty
  - `DownloadAsync` throws `NotSupportedException` with actionable message
  - `GetMetadataAsync` returns empty dict
- `SatelliteIngestionPipelineTests.cs` — **3 tests:**
  - With a mocked provider: happy path → entity with imageBlobCid + bbox + cloud cover
  - With NoOp provider registered: ingestion returns `ProviderUnavailable`
  - Provider returns corrupt stream → `ProviderFailed`

Expected: **6 tests** for ingestion-satellite.

- [ ] **Step 5: Build, test, commit**

```bash
dotnet build packages/ingestion-satellite/Sunfish.Ingestion.Satellite.csproj
dotnet test packages/ingestion-satellite/tests/tests.csproj
git add packages/ingestion-satellite/
git commit -m "feat(ingestion-satellite): ISatelliteImageryProvider contract + NoOp + pipeline, 6 tests"
```

---

## Task 8: DI integration — `AddSunfishIngestion()` composite extension

- [ ] **Step 1: Wire per-modality `WithX()` extensions onto `IngestionBuilder`**

All six modality packages define extension methods on `Sunfish.Ingestion.Core.DependencyInjection.IngestionBuilder`:
- `WithForms()`
- `WithSpreadsheets()`
- `WithVoice(TranscriberKind)`
- `WithSensors()`
- `WithImagery()`
- `WithSatellite()` (registers NoOp by default; consumers override by registering their own `ISatelliteImageryProvider`)

End-user usage:

```csharp
builder.Services.AddSunfishIngestion()
    .WithForms()
    .WithSpreadsheets()
    .WithVoice(TranscriberKind.OpenAiWhisper)
    .WithSensors()
    .WithImagery()
    .WithSatellite();
```

- [ ] **Step 2: Integration test — composite DI registration**

Create `packages/ingestion-core/tests/DependencyInjectionCompositeTests.cs`. Builds a `ServiceCollection` with all six modalities enabled; resolves `IIngestionPipeline<FormSubmission<SampleModel>>`, `IIngestionPipeline<SpreadsheetUpload>`, etc.; asserts all resolve without `InvalidOperationException`.

**Caveat:** this test lives in `ingestion-core/tests` only if we add project references to all six modality packages from the test project. Alternative: put the integration test in a new `packages/ingestion-integration-tests/` project that references all seven (core + 6 modalities). **Decision:** new `ingestion-integration-tests` project, keeps `ingestion-core/tests` dependency-light.

- [ ] **Step 3: Create `packages/ingestion-integration-tests/tests.csproj`**

Plain `Microsoft.NET.Sdk`. ProjectReferences: all 7 ingestion packages + NSubstitute for mocking `IBlobStore` and `IVoiceTranscriber`.

Tests:
- `CompositePipelineRegistrationTests.cs` — **3 tests:**
  - All six `IIngestionPipeline<T>` types resolve from DI
  - `IngestionBuilder.WithX()` is idempotent (double-registration doesn't throw)
  - Per-modality enable flags are independent (only `WithForms()` → only forms resolves)

Expected: **3 tests** for ingestion-integration-tests.

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build packages/ingestion-integration-tests/tests.csproj
dotnet test packages/ingestion-integration-tests/tests.csproj
git add packages/ingestion-integration-tests/
git commit -m "feat(ingestion): composite AddSunfishIngestion() DI + integration tests, 3 tests"
```

---

## Task 9: Update Sunfish.slnx and verify full solution build

- [ ] **Step 1: Update Sunfish.slnx**

Add 14 new csproj entries (7 ingestion packages + 7 tests):

```xml
<Folder Name="/ingestion-core/">
  <Project Path="packages/ingestion-core/Sunfish.Ingestion.Core.csproj" />
  <Project Path="packages/ingestion-core/tests/tests.csproj" />
</Folder>
<Folder Name="/ingestion-forms/">
  <Project Path="packages/ingestion-forms/Sunfish.Ingestion.Forms.csproj" />
  <Project Path="packages/ingestion-forms/tests/tests.csproj" />
</Folder>
<Folder Name="/ingestion-spreadsheets/">
  <Project Path="packages/ingestion-spreadsheets/Sunfish.Ingestion.Spreadsheets.csproj" />
  <Project Path="packages/ingestion-spreadsheets/tests/tests.csproj" />
</Folder>
<Folder Name="/ingestion-voice/">
  <Project Path="packages/ingestion-voice/Sunfish.Ingestion.Voice.csproj" />
  <Project Path="packages/ingestion-voice/tests/tests.csproj" />
</Folder>
<Folder Name="/ingestion-sensors/">
  <Project Path="packages/ingestion-sensors/Sunfish.Ingestion.Sensors.csproj" />
  <Project Path="packages/ingestion-sensors/tests/tests.csproj" />
</Folder>
<Folder Name="/ingestion-imagery/">
  <Project Path="packages/ingestion-imagery/Sunfish.Ingestion.Imagery.csproj" />
  <Project Path="packages/ingestion-imagery/tests/tests.csproj" />
</Folder>
<Folder Name="/ingestion-satellite/">
  <Project Path="packages/ingestion-satellite/Sunfish.Ingestion.Satellite.csproj" />
  <Project Path="packages/ingestion-satellite/tests/tests.csproj" />
</Folder>
<Folder Name="/ingestion-integration-tests/">
  <Project Path="packages/ingestion-integration-tests/tests.csproj" />
</Folder>
```

- [ ] **Step 2: Full solution build + test**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. Test counts:
- Prior baseline: whatever was green pre-Phase-C
- Phase C additions: 12 (core) + 5 (forms) + 19 (spreadsheets) + 15 (voice) + 9 (sensors) + 8 + 1-skip (imagery) + 6 (satellite) + 3 (composite) = **77 new tests + 1 skipped.**

- [ ] **Step 3: Commit**

```bash
git add Sunfish.slnx
git commit -m "feat(ingestion): register 7 ingestion packages + integration tests in Sunfish.slnx"
```

- [ ] **Step 4: Push branch**

```bash
git push origin feat/platform-phase-C-input-modalities
```

---

## Task 10: Documentation + parking-lot inventory

- [ ] **Step 1: Create per-package README.md files**

Each of the 7 packages gets a `README.md` with: one-paragraph scope (lifted from the relevant spec §7.X subsection), link back to the spec subsection, link to the parking-lot items relevant to the modality, and a minimal "Hello, Ingestion" code sample. The voice README additionally lists the three transcriber adapters (Whisper, Azure Speech, NoOp) with their `HttpClient` targets and their `IOptions<T>` configuration keys.

- [ ] **Step 2: Create a single PHASE_C_PARKING_LOT.md**

Path: `docs/superpowers/plans/2026-04-18-platform-phase-C-parking-lot.md`. Structure: one section per deferred item (AI form authoring, real-time streaming, ML inference, quotas, MessagePack, BIM, streaming-blob-write, federation, satellite provider impls, voice mutation synthesis, virus scanning). Each section lists the separate package name, the contract shape, and the phase it targets. Full enumeration of items lives in the "Parking Lot" section at the bottom of this plan — the separate file is the canonical tracking location; this plan's Parking Lot section is the summary.

- [ ] **Step 3: Commit**

```bash
git add packages/ingestion-*/README.md docs/superpowers/plans/2026-04-18-platform-phase-C-parking-lot.md
git commit -m "docs(ingestion): per-package READMEs + Phase C parking lot"
```

---

## End-to-End Example — Inspection Flow Across Four Modalities

This example exercises four Phase C modalities against a single `Inspection` entity from the Property Management MVP (spec §6.3). Reference usage scenario; NOT a test in this plan — it is documentation for the `apps/kitchen-sink` follow-up.

**Scenario.** Quarterly inspection of Unit 4B at Building 42. `correlationId = "inspection-abc-001"` carries through all four steps to tie outputs together.

1. **Form** — Dispatcher fills the "Schedule Inspection" form (`FormBlock<ScheduleInspectionModel>`). `formIngestion.IngestAsync(new FormSubmission<>(model, "sunfish.pm.inspection", utc), context)` mints `Inspection:inspection-abc-001` with body `{ unitId, scheduledUtc, inspectorId, kind: "Quarterly" }`. No blob CIDs.

2. **Imagery** — Drone flies the building exterior; each JPEG (~2 MB) flows through `imageryIngestion.IngestAsync(new ImageUpload(stream, filename, "image/jpeg", "sunfish.pm.imagery"), context with CorrelationId = "inspection-abc-001")`. Each mints an `Imagery` entity with `imageBlobCid`, GPS coords, capture timestamp. `CorrelationId` ties each to the parent.

3. **Voice** — Inspector records voice notes per room (~1 MB WAV each). `voiceIngestion.IngestAsync(new AudioBlob(...), context with CorrelationId = "inspection-abc-001")`. Each mints a `VoiceNote` entity with `audioBlobCid` + inline transcript. The transcript ("stove front-left burner doesn't light — high priority, fire hazard risk") is picked up by downstream LLM mutation-synthesis (parking-lot item §7.3).

4. **Sensor** — Coincident during the inspection: a leak sensor posts a batch. `sensorIngestion.IngestAsync(new SensorBatch(jsonStream, Json, "gateway-12", "sunfish.pm.sensor-batch"), context)` mints a batch entity with 12 readings; one exceeds threshold → produces a `sensor.threshold_breach` event the workflow orchestrator picks up.

**Result.** Kernel receives: 1 `Inspection` (form) + N `Imagery` entities (drone, each with CID) + M `VoiceNote` entities (each with CID + transcript) + 1 `SensorBatch` + 12 `sensor.reading` events + 1 `sensor.threshold_breach`. All four flows share the same `IIngestionPipeline<T>` shape and produce the same kernel-ready `(entity, event[])` tuple. Downstream consumers (workflows, UI, federation, audit) treat them identically — the spec §7.7 "single canonical stream" promise, realized.

---

## Self-Review Checklist

**Per-package structural checks (apply to all 7 packages):**
- [ ] csproj uses `Microsoft.NET.Sdk`, is packable, has explicit `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
- [ ] `tests/tests.csproj` uses plain `Microsoft.NET.Sdk`, has explicit `AssemblyName` matching `Sunfish.Ingestion.<Modality>.Tests`
- [ ] `InternalsVisibleTo` wired from `ingestion-core` to all 6 modality test projects + core test project

**No Blazor dependency (D-NO-BLAZOR-DEPENDENCY):**
- [ ] None of the 6 ingestion packages reference `Microsoft.AspNetCore.Components.*` directly
- [ ] `ingestion-forms` has the transitive reference (via `blocks-forms`) but no direct import; `NoBlazorReferenceInvariantTests` passes
- [ ] `ingestion-forms` assembly-level reflection test asserts no Blazor assembly is loaded at runtime when only form-ingestion is used

**Blob-boundary (D-BLOB-BOUNDARY):**
- [ ] Voice, imagery, sensors, satellite route large binaries through `IBlobStore.PutAsync`
- [ ] Forms and (small) spreadsheets do NOT use `IBlobStore`
- [ ] Entity bodies reference blobs by `Cid.Value` (string), never inline binary

**External API hygiene (D-VOICE-PROVIDER, D-SATELLITE-PROVIDER):**
- [ ] No direct SDK dependency on `Azure.AI.*`, `OpenAI.*`, or any satellite provider SDK
- [ ] All external calls are via injected `HttpClient`
- [ ] NoOp variants exist for every external-provider contract

**Pipeline shape (D-PIPELINE-SHAPE):**
- [ ] All 6 modalities implement `IIngestionPipeline<TInput>` — not modality-specific contracts
- [ ] Middleware composes via `IngestionPipelineBuilder<TInput>.Use(...)`
- [ ] Post-ingest hook (`IPostIngestHandler<T>`) slot present in every modality; no default handlers registered

**Test counts:**
- [ ] ingestion-core: 12 tests
- [ ] ingestion-forms: 5 tests (including NoBlazorReference invariant)
- [ ] ingestion-spreadsheets: 19 tests
- [ ] ingestion-voice: 15 tests
- [ ] ingestion-sensors: 9 tests
- [ ] ingestion-imagery: 8 tests + 1 skipped (streaming parking-lot)
- [ ] ingestion-satellite: 6 tests
- [ ] ingestion-integration-tests: 3 tests
- [ ] **Phase C total: 77 passing + 1 skipped**

**Build and tests:**
- [ ] `dotnet build Sunfish.slnx` = 0 errors, 0 warnings
- [ ] `dotnet test Sunfish.slnx` = baseline + 77 new, 1 skipped
- [ ] Directory.Packages.props pinned: ClosedXML, CsvHelper, MetadataExtractor

**Git hygiene:**
- [ ] 10 commits total on the phase branch (Task 0 deps + 6 modality commits + core + composite DI + Sunfish.slnx + docs)
- [ ] Branch name: `feat/platform-phase-C-input-modalities`
- [ ] No commits touch files outside `packages/ingestion-*/`, `Directory.Packages.props`, `Sunfish.slnx`, or `docs/superpowers/plans/`

**Spec alignment:**
- [ ] Each modality's README links back to the relevant spec §7.X subsection
- [ ] The end-to-end example traces all 4 active modalities to a single `Inspection` entity (matches spec §6.3)
- [ ] Parking-lot inventory captures every deferred item explicitly listed in spec §7 (BIM, real-time, ML inference, federation)

---

## Known Risks and Mitigations

| Risk | Mitigation |
|---|---|
| `ClosedXML` pinned version has an incompatible API with `net10.0` target | Verify at Task 0 Step 3: `dotnet restore` on a throwaway consumer. If 0.105.0 is not yet net10-compatible, pin the nearest stable net9-compatible version and add a parking-lot item for upgrade |
| `CsvHelper` CultureInfo defaults surprise consumers (US-centric date parsing) | Always pass `CultureInfo.InvariantCulture` explicitly; document in `ingestion-spreadsheets/README.md`; test with dates in multiple locales |
| `MetadataExtractor` fails on exotic camera RAW formats | `ExifImageryMetadataExtractor` catches parse exceptions, returns `ImageryMetadata` with all-null fields; pipeline still succeeds (blob is stored even if metadata extraction fails). Parking-lot: surface "metadata extraction failed but blob stored" as a structured warning in `IngestionResult` |
| Voice/Satellite external APIs rate-limit during tests | Tests use `NSubstitute`-mocked `HttpClient`; no real API calls in Phase C tests. Document in README that integration tests against real APIs live in a separate `*.IntegrationTests` project that is opt-in |
| `IBlobStore.PutAsync` takes `ReadOnlyMemory<byte>`, not a `Stream` — multi-GB imagery allocates whole buffer | Phase C buffers to `MemoryStream` first; this fails for >2 GB files. Explicit parking-lot: "Streaming blob-write path — `IBlobStore.PutStreamingAsync(Stream)`". Phase C documents a 2 GB-per-image hard limit in `ingestion-imagery/README.md` |
| Spreadsheet ingestion's "atomic batch" semantics imply a transactional kernel operation that doesn't exist yet | Phase C's `SpreadsheetIngestionPipeline` produces a single `IngestedEntity` with N events; the actual transactional `entity.create` batching is the kernel's job (spec §3, future platform phase). The pipeline signals "these N events belong together" via a shared `CorrelationId`. Document this handoff in the spec §7.2 implementation note |
| `blocks-forms` transitive Blazor reference leaks into `ingestion-forms` consumers | `NoBlazorReferenceInvariantTests` asserts no runtime Blazor assembly load; if it ever fails, split `blocks-forms` into `blocks-forms-contracts` (FormSubmission record, framework-agnostic) + `blocks-forms` (Razor). That split is a separate plan, not Phase C work |
| Fixture binary (`sample-with-gps.jpg`, `tiny-voice-sample.wav`) bloats repo | Both < 50 KB each; acceptable. If additional fixture binaries accrete, migrate to Git LFS in a follow-up repo-hygiene phase |
| `System.IO.Pipelines` streaming sensor decoder is over-engineered for early Phase C users | The streaming path is used only by `JsonSensorBatchDecoder`; simpler `JsonDocument.Parse` fallback is a 5-line change. Decision: keep Pipelines — it's the spec's recommended path for high-volume sensor batches and the early-user ergonomic cost is zero (the public surface is `IAsyncEnumerable<SensorReading>`) |

---

## Parking Lot — Follow-up Platform Phases

Captured separately in `docs/superpowers/plans/2026-04-18-platform-phase-C-parking-lot.md`. Summary:

1. AI-assisted form authoring (Typeform-AI flow) — `Sunfish.Ingestion.Forms.Ai`
2. AI-assisted voice mutation synthesis (spec §7.3 LLM orchestrator) — `Sunfish.Ingestion.Voice.MutationSynthesis`
3. Real-time streaming sensor ingestion (MQTT, IoT Hub, Kinesis) — Platform Phase D
4. ML inference hooks (drone roof-damage detection, sensor anomaly detection) — consumer-owned via `IPostIngestHandler<T>`
5. Multi-tenant input-volume quotas — `QuotaMiddleware` in ingestion-core
6. MessagePack sensor-batch support — `MessagePackSensorBatchDecoder`
7. BIM / CAD imports — separate `ingestion-bim` plan (spec §7.6, §9)
8. Streaming blob-write path — new `IBlobStore.PutStreamingAsync(Stream)` contract
9. Federation / cross-jurisdiction ingestion — Platform Phase D
10. Provider implementations for satellite (Planet Labs, Maxar, Sentinel Hub) — downstream packages
11. Virus scanning middleware — `AntivirusMiddleware` (ClamAV default)
12. Per-schema blob-boundary override — schema registry descriptor field
13. True atomic batch ingestion for spreadsheets — needs kernel transaction API
14. Integration-tests project against real external APIs (Whisper, Azure Speech) — opt-in separate project

---

*Plan authored 2026-04-18. Source references: spec §6, §7; research-notes/external-references.md §1; research-notes/ipfs-evaluation.md §2.*
