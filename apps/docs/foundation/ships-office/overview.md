# Ship's Office — Foundation Contracts

`Sunfish.Foundation.ShipsOffice` declares the contracts for the Ship's Office
aggregation surface per
[ADR 0083](../../../docs/adrs/0083-ships-office-content-aggregation.md).

## Contracts

| Type | Description |
|---|---|
| `IShipsOfficeDataProvider` | Browse (`GetSnapshotAsync`) + search (`SearchAsync`) + subscribe (`SubscribeChangesAsync`). Caller MUST check `ShipAction.ViewShipsOffice` before calling (SUNFISH_SHIPSOFFICE_PERM001 analyzer enforces). |
| `IShipsOfficeCommandService` | Publish (`PublishAsync`) + archive (`ArchiveAsync`). |
| `IContentEditorSurface` | Plug-point for a document editor; `NoopContentEditorSurface` is the v1 default (Phase 5 conditional). |

## Data model

| Type | Description |
|---|---|
| `ShipsOfficeSnapshot` | Paginated projection of all documents for a tenant. |
| `ShipsOfficeDocumentView` | Per-document read model: `Id`, `Kind`, `Title`, `Status`, `VersionLabel`, `LastModifiedBy`, `LastModifiedAt`. NO TIN or PII fields. |
| `ShipsOfficeDocumentKind` | `BundleManifest`, `LeaseDocument`, `VendorW9`, `SignatureEnvelope`. |
| `DocumentStatus` | `Draft`, `Published`, `Archived`. |
| `ShipsOfficeOptions` | `SnapshotPageSize` (default 200), `FallbackPollingInterval` (default 60s), `RequireSecondActorPublish` (default false). |

## Analyzer

`SUNFISH_SHIPSOFFICE_PERM001` (Warning) — fires on any call to
`GetSnapshotAsync` or `SearchAsync` not preceded by a verifiable
`IPermissionResolver.AuthorizeAsync(ShipAction.ViewShipsOffice, …)` call site.
Package: `Sunfish.Foundation.ShipsOffice.Analyzers`.

## Roslyn Analyzer Package

Reference `foundation-ships-office.analyzers` in any project that contains
Ship's Office call sites:

```xml
<ProjectReference Include="path/to/foundation-ships-office.analyzers/
    Sunfish.Foundation.ShipsOffice.Analyzers.csproj"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```
