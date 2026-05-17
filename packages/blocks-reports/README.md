# Sunfish.Blocks.Reports

Read-side report cartridge cluster. Substrate for `IReportCartridge<TParams, TResult>` + `IReportRunner` + `ReportCartridgeRegistry` per Stage 02 §6.1. Cartridges (Trial Balance, AR/AP Aging, P&L by Property, Rent Roll) ship as additive PRs in subsequent hand-offs (PRs 2–6 in this workstream).

## What this package is

- A typed cartridge contract (`IReportCartridge<TParams, TResult>`) that every report implementation satisfies.
- A registry (`ReportCartridgeRegistry`) keyed by `(ReportKind, TParams, TResult)` for type-safe dispatch.
- A canonical runner (`ReportRunner`) that resolves the cartridge, captures a snapshot marker, invokes the cartridge inside a stopwatch, and wraps the result in a `ReportRunResult<TResult>` envelope carrying duration + provisionality + warnings.
- An opt-in `IReportProvisionalityCarrier` interface that cartridge results implement when they need to signal Open / SoftClosed period crossings.

## What this package is NOT

- Not a write-side concern — cartridges MUST NOT inject `IDomainEventPublisher` or any write-capable repository. Read-side discipline is the structural rule that keeps the cluster scoped to a single failure mode.
- Not a cartridge of its own — only the substrate. Real cartridges land in PRs 2–6.
- Not a snapshot-isolation engine — the Phase 1 `InMemorySnapshotMarkerSource` emits monotonic counters; upstream cluster read APIs currently ignore the marker argument. When per-cluster marker honor lands (future hand-off), cartridges automatically get coherent snapshots without any code change here.

## DI wiring

Substrate-only registration (cartridges register separately):

```csharp
using Sunfish.Blocks.Reports.DependencyInjection;

services.AddBlocksReportsSubstrate(o =>
{
    o.MaxWarnings = 64;
    o.HardTimeout = TimeSpan.FromSeconds(120);
});
```

After PRs 2–6 land + PR 7 ships the umbrella `AddBlocksReports()`, the typical host call collapses to:

```csharp
services.AddBlocksReports();   // PR 7 — calls substrate + all 5 cartridges
```

## Cartridge contract — quick reference

```csharp
public interface IReportCartridge<TParams, TResult>
    where TParams : class
    where TResult : class
{
    ReportKind Kind { get; }

    Task<TResult> ExecuteAsync(
        ReportExecutionContext context,
        TParams parameters,
        CancellationToken ct = default);
}
```

Implementations MUST:

1. **Honor tenant isolation.** `context.TenantId` is the sole tenant scope; cartridge parameters carrying entity IDs MUST validate those IDs belong to the same tenant.
2. **Be deterministic.** Two runs with the same `context` + `parameters` MUST produce equal results — the shared `ReportCartridgeDeterminismTests<,,>` base in the test project pins this.
3. **Throw `ReportParameterValidationException`** for invalid parameters; the runner passes it through unwrapped so callers see the original type.
4. **NEVER inject a write-side surface.** Code review on every cartridge PR rejects publishers + write-capable repositories.

## Convention deviations

- **`DateTimeOffset` + `TimeProvider`** instead of `NodaTime.Instant` + `IClock` per cohort precedent (W#34 / W#35 / W#40 / W#41 / W#49). `NodaTime` is not on `Directory.Packages.props`; migration would be a single follow-up ADR.

## License

MIT (see `NOTICE.md`).
