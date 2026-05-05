# W#52 Stage 06 Hand-off — Tactical Anomaly Detection + Threat-Trigger Surface

**Workstream:** W#52 — Tactical Anomaly Detection + Threat-Trigger Surface
**ADR:** [ADR 0081](../../../docs/adrs/0081-tactical-anomaly-detection.md) — Accepted 2026-05-05
**Owner (implementation):** sunfish-PM (COB)
**Status:** `ready-to-build`
**Effort estimate:** ~16-22h / ~6 PRs

---

## Prerequisites — verify before beginning any phase

| Prerequisite | Check | Notes |
|---|---|---|
| ADR 0077 W#46 Phase 1 merged | `git log --oneline origin/main \| grep "W#46"` — Phase 1 must exist | ShipAction catalog + IPermissionResolver required by Phase 2 |
| ADR 0065 W#42 built | `ls packages/foundation-wayfinder/` exists on origin/main ✓ (already built) | IStandingOrderRepository.AppendAsync required by Phase 2 |
| ADR 0077 W#46 Phase 3 merged | Look for ILiveAnnouncer / IFocusTrap in foundation-ship-common | Required by Phase 3a (UI blocks) |
| ADR 0080 W#51 Phase 1+ merged | `packages/foundation-quarterdeck/` exists | IQuarterdeckAlertSource required by Phase 4 (LookoutQuarterdeckAlertSource) |

**H1 — NuGet binary halt:** This project has NOT shipped a NuGet binary (pre-v1). No binary-compat halt applies. Confirm before every Phase 1 PR: `find packages/ -name "*.nupkg" | wc -l` must be 0.

---

## Phase 1 — `foundation-tactical` substrate (contracts + data model)

**Gate:** standalone; no prerequisites beyond existing origin/main.
**Estimated effort:** ~3-4h
**PR scope:** `packages/foundation-tactical/` new package

### 1.1 Project file

`packages/foundation-tactical/Sunfish.Foundation.Tactical.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Foundation.Tactical</RootNamespace>
    <AssemblyName>Sunfish.Foundation.Tactical</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NodaTime" Version="$(NodaTimeVersion)" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="$(MicrosoftExtensionsOptionsVersion)" />
  </ItemGroup>
  <ItemGroup>
    <!-- foundation (no UI dep) -->
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\kernel-audit\Sunfish.Kernel.Audit.csproj" />
  </ItemGroup>
</Project>
```

### 1.2 Data model types

Create `packages/foundation-tactical/` with these files (full types per ADR 0081 §1):

**`TacticalSignalKind.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public enum TacticalSignalKind
{
    DecryptionFailureSpike,
    PeerConnectivityLoss,
    MergeConflictRate,
    CrdtGrowthAnomaly,
    AuthorizationFailureSpike,
    BulkAccessPattern,
    ServiceDegradation,
    ProbeTimeout,
    StandingOrderViolation,
    Custom
}
```

**`AlertSeverity.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public enum AlertSeverity { Critical, High, Medium, Low, Informational }
```

**`AlertRoutingPolicy.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public enum AlertRoutingPolicy { HighPriorityLookout, InformationalSonar }
```

**`AlertStatus.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Active → Acknowledged; Active → Expired; Active → Superseded.
/// Once Expired or Superseded, AcknowledgedBy/AcknowledgedAt retained if previously set.
/// </summary>
public enum AlertStatus { Active, Acknowledged, Expired, Superseded }
```

**`IncidentStatus.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

/// Open → Resolved (directly or via Investigating — reserved future transition).
public enum IncidentStatus { Open, Investigating, Resolved }
```

**`TacticalSignal.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;
using System.Text.Json.Nodes;
using NodaTime;

namespace Sunfish.Foundation.Tactical;

public sealed record TacticalSignal(
    TenantId             TenantId,
    TacticalSignalKind   Kind,
    Instant              OccurredAt,
    /// Freeform payload. Must not be null; use empty JsonObject for kinds with no payload.
    JsonNode             Payload
);
```

**`TacticalAlert.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;
using NodaTime;

namespace Sunfish.Foundation.Tactical;

public sealed record TacticalAlert(
    /// Format: "{RuleName}:{source-local-id}". Validated by IAlertRouter.
    string               AlertId,
    TenantId             TenantId,
    string               RuleName,
    AlertSeverity        Severity,
    AlertRoutingPolicy   RoutingPolicy,
    string               Title,
    string               Summary,
    Instant              DetectedAt,
    AlertStatus          Status,
    bool                 RequiresAcknowledgement,
    IReadOnlyList<string> RunbookStepIds,
    ActorId?             AcknowledgedBy,
    Instant?             AcknowledgedAt
);
```

**`IncidentRecord.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;
using NodaTime;

namespace Sunfish.Foundation.Tactical;

public sealed record IncidentRecord(
    string               IncidentId,
    TenantId             TenantId,
    string               Title,
    string               RootAlertId,
    IncidentStatus       Status,
    Instant              OpenedAt,
    Instant              LastUpdatedAt,
    Instant?             ClosedAt,
    ActorId              OpenedBy,
    ActorId?             ClosedBy,
    string?              ResolutionNote,
    IReadOnlyList<string> RunbookStepIds,
    IReadOnlyList<string> LinkedAlertIds
);
```

**`TacticalOptions.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

/// Bounds normative: implementations MUST throw InvalidOperationException on violation
/// during DI registration or Validate().
public sealed record TacticalOptions(
    TimeSpan HeartbeatInterval,
    int      MaxActiveAlerts,
    TimeSpan AlertTtl,
    int      SignalBatchSize,
    int      MaxActiveIncidents,
    int      MaxEmergencyOrdersPerMinute,
    int      MaxAlertsPerMinutePerRule
) {
    public static TacticalOptions Default => new(
        HeartbeatInterval:           TimeSpan.FromSeconds(30),
        MaxActiveAlerts:             200,
        AlertTtl:                    TimeSpan.FromHours(24),
        SignalBatchSize:             100,
        MaxActiveIncidents:          50,
        MaxEmergencyOrdersPerMinute: 3,
        MaxAlertsPerMinutePerRule:   60
    );
}
```

**`TacticalSnapshot.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;
using NodaTime;

namespace Sunfish.Foundation.Tactical;

public sealed record TacticalSnapshot(
    Instant                          CapturedAt,
    TenantId                         TenantId,
    IReadOnlyList<TacticalAlert>     ActiveAlerts,
    IReadOnlyList<TacticalAlert>     LookoutAlerts,
    IReadOnlyList<IncidentRecord>    ActiveIncidents,
    bool                             CanAccessFireControl,
    bool                             CanAcknowledgeAlerts,
    int                              RegisteredRuleCount,
    int                              SignalRatePerMinute,
    bool                             IsPartialSnapshot,
    IReadOnlyList<string>?           DegradedSubsystems
);
```

**`ThreatTriggerTemplate.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public sealed record ThreatTriggerTemplate(
    string     RuleName,
    AlertSeverity MinimumSeverity,
    string     OrderContent,
    TimeSpan?  ExpiresAfter
);
```

**`TacticalUnauthorizedException.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

/// <inheritdoc/>
/// Inherits UnauthorizedAccessException — MUST NOT be caught by retry logic.
public sealed class TacticalUnauthorizedException : UnauthorizedAccessException
{
    public TacticalUnauthorizedException(string message) : base(message) { }
}
```

### 1.3 AuditEventType constants

In `packages/kernel-audit/AuditEventType.cs`, add the following 13 constants in a `// ── Tactical ──` block:

```csharp
// ── Tactical (ADR 0081) ─────────────────────────────────────────────────────
public static readonly AuditEventType AnomalyDetected                        = new("AnomalyDetected");
public static readonly AuditEventType AlertRouted                             = new("AlertRouted");
public static readonly AuditEventType TacticalAlertExpired                    = new("TacticalAlertExpired");
public static readonly AuditEventType LookoutAlertEvicted                     = new("LookoutAlertEvicted");
public static readonly AuditEventType TacticalAlertAcknowledgementRequested  = new("TacticalAlertAcknowledgementRequested");
public static readonly AuditEventType TacticalAlertAcknowledged               = new("TacticalAlertAcknowledged");
public static readonly AuditEventType IncidentOpenRequested                   = new("IncidentOpenRequested");
public static readonly AuditEventType IncidentOpened                          = new("IncidentOpened");
public static readonly AuditEventType IncidentCloseRequested                  = new("IncidentCloseRequested");
public static readonly AuditEventType IncidentClosed                          = new("IncidentClosed");
public static readonly AuditEventType EmergencyStandingOrderIssued            = new("EmergencyStandingOrderIssued");
public static readonly AuditEventType EmergencyStandingOrderIssuanceFailed    = new("EmergencyStandingOrderIssuanceFailed");
public static readonly AuditEventType TacticalAuthorizationDenied             = new("TacticalAuthorizationDenied");
```

**Before adding:** grep for any existing collision:
```bash
grep -r "AnomalyDetected\|AlertRouted\|TacticalAlert\|IncidentOpen\|IncidentClose\|EmergencyStanding\|TacticalAuthorization" packages/kernel-audit/
```
All must return zero results.

### 1.4 Rule engine + routing interfaces

**`ITacticalRule.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public interface ITacticalRule
{
    string             RuleName             { get; }
    AlertSeverity      DefaultSeverity      { get; }
    AlertRoutingPolicy DefaultRoutingPolicy { get; }

    /// Synchronous. MUST NOT do I/O. MUST NOT throw on unexpected signal shapes.
    bool Evaluate(TacticalSignal signal, out TacticalAlert? alert);
}
```

**`ITacticalRuleEngine.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public interface ITacticalRuleEngine
{
    void RegisterRule(ITacticalRule rule);
    IReadOnlyList<TacticalAlert> Evaluate(TacticalSignal signal);
    IAsyncEnumerable<TacticalAlert> EvaluateStreamAsync(
        IAsyncEnumerable<TacticalSignal> signals,
        CancellationToken ct = default);
    IReadOnlyList<ITacticalRule> GetRegisteredRules();
}
```

**`IAlertRouter.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public interface IAlertRouter
{
    /// Order of operations per §2 (ADR 0081): validate AlertId → rate-limit →
    /// AnomalyDetected audit → AlertRouted audit → route to ILookout or ISonarStore.
    ValueTask RouteAsync(TacticalAlert alert, CancellationToken ct = default);
}
```

**`ISonarStore.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

public interface ISonarStore
{
    ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default);
    IReadOnlyList<TacticalAlert> GetActiveAlerts(TenantId tenantId);
}
```

**`ILookout.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

public interface ILookout
{
    ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default);
    IReadOnlyList<TacticalAlert> GetActiveLookoutAlerts(TenantId tenantId);

    /// Yields on: new alert written; alert expires/superseded; heartbeat.
    /// Acknowledged status-changes yield on heartbeat only (not immediately) per §2.
    IAsyncEnumerable<IReadOnlyList<TacticalAlert>> SubscribeLookoutAsync(
        TenantId tenantId, CancellationToken ct = default);
}
```

### 1.5 Provider + command interfaces

**`ITacticalDataProvider.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Tactical;

public interface ITacticalDataProvider
{
    ValueTask<TacticalSnapshot> GetSnapshotAsync(
        TenantId tenantId, Principal actor, CancellationToken ct = default);

    ValueTask<IReadOnlyList<TacticalAlert>> GetAlertsAsync(
        TenantId tenantId, Principal actor,
        AlertRoutingPolicy? filterPolicy = null,
        CancellationToken ct = default);

    ValueTask<IReadOnlyList<IncidentRecord>> GetActiveIncidentsAsync(
        TenantId tenantId, Principal actor, CancellationToken ct = default);

    IAsyncEnumerable<TacticalSnapshot> SubscribeSnapshotAsync(
        TenantId tenantId, Principal actor, CancellationToken ct = default);
}
```

**`ITacticalCommandService.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Tactical;

public interface ITacticalCommandService
{
    ValueTask AcknowledgeAlertAsync(
        TenantId tenantId, Principal actor, string alertId,
        CancellationToken ct = default);

    ValueTask<IncidentRecord> OpenIncidentAsync(
        TenantId tenantId, Principal actor, string rootAlertId, string title,
        IReadOnlyList<string> runbookStepIds, CancellationToken ct = default);

    ValueTask CloseIncidentAsync(
        TenantId tenantId, Principal actor, string incidentId,
        string resolutionNote, CancellationToken ct = default);
}
```

### 1.6 Threat-trigger interface + system principal seam

**`ISystemPrincipalProvider.cs`:**

```csharp
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Tactical;

/// Resolves the system principal for a tenant.
/// The returned Principal carries ShipRole.System — not assignable to human actors.
/// Registered at DI bootstrap. IPermissionResolver MUST grant IssueEmergencyStandingOrder
/// to ShipRole.System (confirmed at W#46 Phase 1 build).
public interface ISystemPrincipalProvider
{
    ValueTask<Principal> GetSystemPrincipalAsync(
        TenantId tenantId, CancellationToken ct = default);
}
```

**`IThreatTriggerService.cs`:**

```csharp
namespace Sunfish.Foundation.Tactical;

public interface IThreatTriggerService
{
    void RegisterTemplate(ThreatTriggerTemplate template);

    /// Issuer principal is resolved internally from ISystemPrincipalProvider (§4.1, ADR 0081).
    /// Returns null if no template matches, severity threshold not met, or on denial/failure.
    ValueTask<string?> TryIssueAsync(
        TacticalAlert alert, CancellationToken ct = default);
}
```

### 1.7 ShipAction constants

**Gated on W#46 Phase 1 landing.** At Phase 1 build time, verify `packages/foundation-ship-common/` exists on origin/main. Add in the `foundation-ship-common` ShipAction catalog:

```csharp
// ── Tactical (ADR 0081) ─────────────────────────────────────────────────────
public static readonly ShipAction ViewTactical                = new("ViewTactical");
public static readonly ShipAction ViewFireControl             = new("ViewFireControl");
public static readonly ShipAction AcknowledgeTacticalAlert    = new("AcknowledgeTacticalAlert");
public static readonly ShipAction OpenIncident                = new("OpenIncident");
public static readonly ShipAction CloseIncident               = new("CloseIncident");
/// Granted to ShipRole.System only (resolved via ISystemPrincipalProvider).
public static readonly ShipAction IssueEmergencyStandingOrder = new("IssueEmergencyStandingOrder");
/// Reserved for runtime template management — declared for catalog completeness; not used in v1.
public static readonly ShipAction ManageThreatTriggers        = new("ManageThreatTriggers");
```

### 1.8 Phase 1 test project

`packages/foundation-tactical/tests/Sunfish.Foundation.Tactical.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$(MicrosoftNetTestSdkVersion)" />
    <PackageReference Include="xunit" Version="$(XunitVersion)" />
    <PackageReference Include="xunit.runner.visualstudio" Version="$(XunitRunnerVisualStudioVersion)" />
    <PackageReference Include="NSubstitute" Version="$(NSubstituteVersion)" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.Foundation.Tactical.csproj" />
  </ItemGroup>
</Project>
```

**Required Phase 1 tests (contract surface):**

```
ContractSurfaceTests:
  [Fact] ITacticalRule_has_required_members
  [Fact] ITacticalRuleEngine_has_required_members
  [Fact] IAlertRouter_has_required_members
  [Fact] ISonarStore_has_required_members
  [Fact] ILookout_has_required_members
  [Fact] ITacticalDataProvider_has_required_members
  [Fact] ITacticalCommandService_has_required_members
  [Fact] IThreatTriggerService_has_required_members
  [Fact] ISystemPrincipalProvider_has_required_members
  [Fact] TacticalOptions_Default_values_are_within_normative_bounds
  [Fact] AuditEventType_constants_have_expected_string_values
```

Pattern: use `typeof(ITacticalRule).GetMethod(...)` / `typeof(ITacticalRuleEngine).GetMethod(...)` to verify method names and parameter counts match spec. `TacticalOptions.Default` test validates HeartbeatInterval=30s, MaxActiveAlerts=200, AlertTtl=24h, SignalBatchSize=100, MaxActiveIncidents=50, MaxEmergencyOrdersPerMinute=3, MaxAlertsPerMinutePerRule=60.

### 1.9 Phase 1 halt conditions

| Halt | Action |
|---|---|
| **H1** — `AuditEventType` collision found during grep | Stop. File COB question in `research-inbox/`. Do NOT add duplicate constant. |
| **H2** — `ShipAction` collision found for ViewTactical etc. | Stop. File COB question. W#46 Phase 1 may have shipped with different names. |
| **H3** — `foundation-ship-common` not on origin/main | ShipAction constants must wait for W#46 Phase 1. Add AuditEventType first; defer ShipAction block to Phase 2. |
| **H4** — build fails with unexpected reference errors | Check FORWARD-REF symbols in §A0 of ADR 0081; do not add forward-refs in Phase 1. |

**Pre-merge council required.** Security-engineering subagent mandatory (audit constants + ISystemPrincipalProvider seam).

---

## Phase 2 — `DefaultTacticalRuleEngine` + `DefaultAlertRouter` + `DefaultThreatTriggerService`

**Gate:** W#46 Phase 1 merged (IPermissionResolver + ShipAction catalog on origin/main); Phase 1 merged.
**Estimated effort:** ~4-5h
**PR scope:** `packages/foundation-tactical/` implementations + expanded tests

### 2.1 `DefaultTacticalRuleEngine`

Location: `packages/foundation-tactical/DefaultTacticalRuleEngine.cs`

Key behavior per ADR 0081 §2:
- `RegisterRule`: throws `InvalidOperationException` if RuleName already registered; enforces `sunfish.*` prefix restriction (§8.3); MUST NOT be called after first signal processed (flag: `_firstSignalProcessed`).
- `Evaluate`: evaluates all registered rules in registration order; catches per-rule exceptions (§2.1 failure modes); all rules invoked (no short-circuit); returns combined list.
- `EvaluateStreamAsync`: wraps `Evaluate` per-signal with async enumeration; on source fault propagates to caller.
- Signal ordering: partitioned by `TenantId` using `Channel<TacticalSignal>` per tenant (§2.2).
- Rule error rate tracking: if a rule throws >100 times/minute, emit `TacticalAuthorizationDenied(denialReason="rule-evaluation-failure-rate")` once per minute per rule.

### 2.2 `DefaultAlertRouter`

Location: `packages/foundation-tactical/DefaultAlertRouter.cs`

Key behavior per ADR 0081 §2 `IAlertRouter.RouteAsync` contract:
1. Validate `AlertId` regex `^[A-Za-z0-9_\-\.:]{1,128}$`. On failure: emit `TacticalAuthorizationDenied(denialReason="invalid-alert-id")` and return.
2. Enforce `MaxAlertsPerMinutePerRule` per `(TenantId, RuleName)`. On breach: emit `TacticalAuthorizationDenied(denialReason="rule-rate-limit")` and return.
3. Emit `AnomalyDetected` audit event.
4. Emit `AlertRouted` audit event.
5. For `HighPriorityLookout` policy: call `ILookout.WriteAsync`. For `InformationalSonar`: call `ISonarStore.WriteAsync`.
6. `AllowedHighPriorityRulePrefixes` check (§8.3): if RuleName doesn't match prefix allowlist, downgrade to `InformationalSonar` + emit `TacticalAuthorizationDenied(denialReason="high-priority-routing-not-allowlisted")`.
7. Audit events in steps 3-4 committed BEFORE step 5. If step 5 fails: audit records retained; log Warning.
8. MUST complete within 200ms (caller applies 250ms timeout as defense-in-depth).

### 2.3 `DefaultThreatTriggerService`

Location: `packages/foundation-tactical/DefaultThreatTriggerService.cs`

Key behavior per ADR 0081 §4:
- `RegisterTemplate`: throws if RuleName already has a template; throws if RuleName not registered with `ITacticalRuleEngine`.
- `TryIssueAsync` operation order (§4 steps 1-8):
  1. Verify `alert.TenantId` matches ambient `ITenantContext.TenantId`. On mismatch: emit `TacticalAuthorizationDenied(denialReason="tenant-mismatch")`; return null.
  2. Dedup check per `(TenantId, RuleName)` / 60s window (`ConcurrentDictionary` — §4.3). On hit: return cached `orderId`.
  3. `MaxEmergencyOrdersPerMinute` check. On breach: emit `TacticalAuthorizationDenied(denialReason="emergency-order-rate-limit")`; return null.
  4. Per-signal budget check (§4.4): at most 1 order per `(TenantId, signal fingerprint)` per chain. On breach: emit `TacticalAuthorizationDenied(denialReason="signal-order-budget-exceeded")`; return null.
  5. `orderId = Guid.NewGuid().ToString("N")`.
  6. Template substitution (`{AlertId}`, `{RuleName}`, `{Severity}`, `{DetectedAt}`). Post-substitution >2048 chars: throw `ArgumentException`.
  7. Emit `EmergencyStandingOrderIssued` (with orderId from step 5, BEFORE AppendAsync).
  8. Call `IStandingOrderRepository.AppendAsync`. On failure: log error; emit `EmergencyStandingOrderIssuanceFailed`; return null.

**ISystemPrincipalProvider wiring:** constructor-inject `ISystemPrincipalProvider`. Resolve at step 7 (before AppendAsync). IPermissionResolver check for `IssueEmergencyStandingOrder` against the system principal. On denial: emit `TacticalAuthorizationDenied(denialReason="IssueEmergencyStandingOrder", attemptedBy=systemPrincipal.ActorId)`.

### 2.4 ShipAction startup registration check (§8.1)

In `blocks-tactical` (Phase 3a), validate all 7 ShipAction values at startup. Throw descriptive `InvalidOperationException` for any unregistered action. This is a Phase 3a concern; document here for completeness.

### 2.5 Tenant context binding (§8.2)

All service implementations (`DefaultAlertRouter`, `DefaultThreatTriggerService`) inject `ITenantContext` and verify `tenantId == ambient.TenantId`. On mismatch: emit `TacticalAuthorizationDenied(denialReason="tenant-mismatch")` + throw `TacticalUnauthorizedException`.

Snapshot and alert-list results: cache key MUST include both `TenantId` and `ActorId`. No cross-actor caching.

### 2.6 Phase 2 required tests

```
DefaultTacticalRuleEngineTests:
  [Fact] RegisterRule_rejects_duplicate_ruleName
  [Fact] RegisterRule_rejects_sunfish_prefix_from_unverified_assembly
  [Fact] Evaluate_returns_empty_when_no_rules_registered
  [Fact] Evaluate_catches_throwing_rule_continues_others
  [Fact] Evaluate_all_rules_invoked_no_shortcircuit

DefaultAlertRouterTests:
  [Fact] RouteAsync_rejects_invalid_alertId_regex
  [Fact] RouteAsync_emits_TacticalAuthorizationDenied_on_rate_breach
  [Fact] RouteAsync_emits_AnomalyDetected_before_routing
  [Fact] RouteAsync_routes_HighPriority_to_ILookout
  [Fact] RouteAsync_routes_Informational_to_ISonarStore
  [Fact] RouteAsync_downgrades_unlisted_prefix_HighPriority_to_InformationalSonar

DefaultThreatTriggerServiceTests:
  [Fact] TryIssueAsync_returns_null_when_no_template_for_ruleName
  [Fact] TryIssueAsync_returns_null_below_minimum_severity
  [Fact] TryIssueAsync_dedup_returns_cached_orderId_within_window
  [Fact] TryIssueAsync_enforces_per_tenant_rate_limit
  [Fact] TryIssueAsync_enforces_per_signal_budget
  [Fact] TryIssueAsync_emits_EmergencyStandingOrderIssued_before_AppendAsync
  [Fact] TryIssueAsync_emits_IssuanceFailed_when_AppendAsync_throws
  [Fact] TryIssueAsync_rejects_tenant_mismatch
  [Fact] TryIssueAsync_throws_on_postcsubstitution_overflow_2048
```

### 2.7 Phase 2 halt conditions

| Halt | Action |
|---|---|
| **H1** — W#46 Phase 1 not yet on origin/main | ShipAction constants must remain a stub; use string literals in IPermissionResolver check if available separately. |
| **H2** — `IStandingOrderRepository.AppendAsync` does not support caller-supplied orderId | Per ADR 0081 §A0 Open Q1: generate orderId independently and store alongside audit record for reconciliation. File COB question. |
| **H3** — council raises concern on system principal authority chain | Halt; update research-inbox before continuing. |

**Pre-merge council required.** Security-engineering subagent mandatory (§4 threat-trigger chain + §8.2 tenant binding + §8.3 prefix enforcement + §8.4 audit-before-action).

---

## Phase 3a — `blocks-tactical` Sonar Room + Lookout UI

**Gate:** W#46 Phase 3 merged (ILiveAnnouncer + IFocusTrap + SunfishA11yAssertions on origin/main); Phase 2 merged.
**Estimated effort:** ~5-6h
**PR scope:** `packages/blocks-tactical/` new package (Sonar Room + Lookout sub-rooms only)

### 3a.1 Project file

`packages/blocks-tactical/Sunfish.Blocks.Tactical.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Blocks.Tactical</RootNamespace>
    <AssemblyName>Sunfish.Blocks.Tactical</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation-tactical\Sunfish.Foundation.Tactical.csproj" />
    <ProjectReference Include="..\foundation-ship-common\Sunfish.Foundation.Ship.Common.csproj" />
    <!-- NOTE: MUST NOT directly reference foundation-wayfinder or foundation-engine-room -->
  </ItemGroup>
</Project>
```

### 3a.2 Sonar Room component

`packages/blocks-tactical/SonarRoomPanel.cs` (or `.razor`):

WCAG 2.2 AA contract per ADR 0081 §7.3 (Sonar Room):
- Container: `<section role="region" aria-labelledby="sonar-room-heading" id="sonar-room" tabindex="-1">`
- Heading: `<h2 id="sonar-room-heading">Sonar Room</h2>`
- Skip-link target: `id="sonar-room"` (reachable via first skip-link in `<main>`).
- Signal-rate gauge: `role="meter"` with `aria-valuemin="0"`, `aria-valuemax` from `TacticalOptions.MaxAlertsPerMinutePerRule` (config-derived), `aria-valuenow`, `aria-valuetext="N signals per minute"`. Rate number rendered as visible text (`aria-hidden="true"` on the numeric span).
- SR-safe rate updates: separate polite sibling `<div aria-live="polite" aria-atomic="true">` — announcement throttled to threshold crossings only (not every heartbeat).
- Registered rules list: `<ul aria-label="Registered rules">` with `<li>` per rule; RuleName displayed.
- InformationalSonar-policy alert list: `<ul aria-label="Sonar room alerts">` — one `<li>` per `TacticalAlert` where `RoutingPolicy == InformationalSonar`.
- `SunfishA11yAssertions.ReducedMotionDefaultsToPaused` REQUIRED (rate gauge animation).

### 3a.3 Lookout component

`packages/blocks-tactical/LookoutPanel.cs` (or `.razor`):

WCAG 2.2 AA contract per ADR 0081 §7.3 (Lookout):
- Container: `<section role="region" aria-labelledby="lookout-heading" id="lookout" tabindex="-1">`
- Heading: `<h2 id="lookout-heading">Lookout</h2>`
- Live region: `<ul aria-live="assertive" aria-atomic="false" aria-relevant="additions" aria-label="High-priority tactical alerts">`.
  - New-item announcements only. Acknowledged status-changes MUST NOT be pushed into this region.
  - Acknowledged-status changes go to a separate `<div aria-live="polite" aria-atomic="true">` sibling.
- Pause control: `<button aria-pressed="false">Pause Lookout ticker</button>`. Static label. `aria-pressed` toggled on click.
- Default-to-paused under `prefers-reduced-motion: reduce`. `SunfishA11yAssertions.ReducedMotionDefaultsToPaused` REQUIRED.
- SC 2.2.2: Lookout MUST auto-pause on hover AND on keyboard focus entering the Lookout region.
- `SunfishA11yAssertions.AssertiveRegionAnnouncesAdditionsOnly` REQUIRED.

### 3a.4 Alert severity presentation (§7.4)

All alert severity rendering uses BOTH color AND non-color indicators:

| Severity | Color token | Icon shape | Text label | aria treatment |
|---|---|---|---|---|
| Critical | `--color-severity-critical` | Octagon | "Critical" | Icon `aria-hidden="true"` |
| High | `--color-severity-high` | Triangle | "High" | Icon `aria-hidden="true"` |
| Medium | `--color-severity-medium` | Diamond | "Medium" | Icon `aria-hidden="true"` |
| Low | `--color-severity-low` | Circle | "Low" | Icon `aria-hidden="true"` |
| Informational | `--color-severity-informational` | Info badge | "Info" | Icon `aria-hidden="true"` |

Icons MUST be visually distinct shapes (not same-shape + color-tinted). Color tokens against card background MUST meet SC 1.4.3 (text ≥ 4.5:1) and SC 1.4.11 (non-text ≥ 3:1). Focus indicators: SC 2.4.7 (≥ 3:1 contrast, ≥ 2px outline).

**Flashing/pulsing indicators:** default MUST NOT include flashing. If used: < 3 Hz. MUST suppress under `prefers-reduced-motion: reduce`.

### 3a.5 Acknowledge button contract (§7.5)

Per-alert acknowledge button:
- Use `aria-disabled="true"` (NEVER native `disabled` — native removes from tab order).
- MUST remain focusable when `aria-disabled="true"`.
- MUST suppress click AND keydown (Enter, Space) when disabled (early return).
- Denial reason on a visible element: `id="ack-denial-reason-{alertId}"` with `aria-describedby` on button.
- CSS: `cursor: not-allowed` when `aria-disabled="true"`.
- `SunfishA11yAssertions.AriaDisabledButtonRemainsInTabOrder` REQUIRED.
- `SunfishA11yAssertions.AriaDisabledSuppressesActivation` REQUIRED.

### 3a.6 Main layout + skip-links

```html
<main id="main-content" tabindex="-1">
  <a href="#sonar-room" class="skip-link">Skip to Sonar Room</a>
  <a href="#lookout" class="skip-link">Skip to Lookout</a>
  <a href="#fire-control" class="skip-link">Skip to Fire Control</a>
  ...
  <section id="sonar-room" tabindex="-1" role="region" aria-labelledby="sonar-room-heading">
    ...
  </section>
  <section id="lookout" tabindex="-1" role="region" aria-labelledby="lookout-heading">
    ...
  </section>
  <!-- Fire Control stub — implemented in Phase 3b -->
</main>
```

`SunfishA11yAssertions.SubRoomsKeyboardReachable` REQUIRED.

### 3a.7 ShipAction startup registration check (§8.1)

In the `blocks-tactical` DI registration extension method, verify all 7 ShipAction values from §6 (ADR 0081) are registered with `IPermissionResolver` at startup. Any unregistered action MUST throw `InvalidOperationException` with descriptive message listing the missing action. Pattern mirrors ADR 0079 §4.3 and ADR 0080 §5.1.

### 3a.8 Phase 3a required tests

```
SonarRoomPanelTests:
  [Fact] SonarRoom_has_section_role_region_with_labelledby
  [Fact] SonarRoom_gauge_has_role_meter_with_aria_attributes
  [Fact] SonarRoom_polite_live_region_present

LookoutPanelTests:
  [Fact] Lookout_live_region_has_assertive_atomic_false_relevant_additions
  [Fact] Lookout_pause_button_has_aria_pressed
  [Fact] Lookout_default_paused_under_reduced_motion (SunfishA11yAssertions.ReducedMotionDefaultsToPaused)
  [Fact] Lookout_acknowledge_button_uses_aria_disabled_not_native_disabled (SunfishA11yAssertions.AriaDisabledButtonRemainsInTabOrder)
  [Fact] Lookout_acknowledge_button_suppresses_click_when_aria_disabled (SunfishA11yAssertions.AriaDisabledSuppressesActivation)
  [Fact] Lookout_assertive_region_announces_additions_only (SunfishA11yAssertions.AssertiveRegionAnnouncesAdditionsOnly)
  [Fact] MainContent_skip_links_keyboard_reachable (SunfishA11yAssertions.SubRoomsKeyboardReachable)
```

**Pre-merge council required.** WCAG/a11y subagent mandatory (assertive live region + reduced-motion + severity non-color encoding + aria-disabled pattern).

---

## Phase 3b — Fire Control UI + Emergency Standing Order dialog

**Gate:** Phase 3a merged; W#49 Phase 1 merged (IOodWatchService on origin/main — for incident-correlation display).
**Estimated effort:** ~4-5h
**PR scope:** Fire Control sub-room + dialog

### 3b.1 Fire Control component

`packages/blocks-tactical/FireControlPanel.cs` (or `.razor`):

WCAG 2.2 AA contract per ADR 0081 §7.3 (Fire Control):
- Container: `<section role="region" aria-labelledby="fire-control-heading" id="fire-control" tabindex="-1">`
- Heading: `<h2 id="fire-control-heading">Fire Control</h2>`
- Incidents: `<ol aria-label="Active incidents">` with `<li>` per `IncidentRecord`.
- Runbook steps: `<ol aria-label="Runbook steps">` with `<li>` per step. Step number rendered as visible text. Accessible name via `aria-labelledby` referencing both the step-number span and step-title span (NOT `aria-label` — suppresses visible content).
- Incident state transitions: announced via `<div aria-live="polite" aria-atomic="true">` sibling to the incident list.
- `SunfishA11yAssertions.IncidentStateTransitionAnnounced` REQUIRED.

### 3b.2 Emergency Standing Order confirmation dialog (§7.6)

When actor manually triggers threat-trigger Standing Order from Fire Control:

```html
<div role="alertdialog" aria-modal="true"
     aria-labelledby="dialog-title"
     aria-describedby="dialog-consequence"
     tabindex="-1">
  <h2 id="dialog-title">Issue Emergency Standing Order</h2>
  <p id="dialog-consequence">
    <!-- Post-substitution content preview (NOT template tokens) -->
    <!-- Truncate to 280 chars primary; >280: <details><summary>Show full text</summary>...</details> -->
  </p>
  <button type="button" aria-disabled="true" id="confirm-btn">Confirm</button>
  <button type="button" id="cancel-btn">Cancel</button>
  <!-- Deliberation-pause polite announcement (injected at t=2000ms) -->
  <div aria-live="polite" aria-atomic="true" class="sr-only" id="deliberation-announce"></div>
</div>
```

Dialog behavior:
- `role="alertdialog"` (NOT generic `dialog` — security-critical destructive confirmation).
- On open: `IFocusTrap.TrapFocus(dialog)`; initial focus → Cancel button.
- Confirm button: `aria-disabled="true"` at open. Enabled exactly at t=2000ms after open.
- At t=2000ms: inject "Confirm available" into `#deliberation-announce` polite region.
- `SunfishA11yAssertions.DeliberationPauseAnnouncesEnablement` REQUIRED.
- On close: `IFocusTrap.RestoreFocus(fallback: MainLandmark)`. Polite region announces "Standing Order issued" or "Cancelled".
- `SunfishA11yAssertions.AlertDialogHasRoleModalLabelDescribedBy` REQUIRED (role="alertdialog" + aria-modal + aria-labelledby + aria-describedby presence).
- `SunfishA11yAssertions.DialogOutcomeAnnouncedOnClose` REQUIRED.
- Consequence text: post-substitution preview (NOT raw template tokens). SC 3.3.4 deliberation-pause.

### 3b.3 Phase 3b required tests

```
FireControlPanelTests:
  [Fact] FireControl_has_section_role_region_with_labelledby
  [Fact] FireControl_incidents_list_is_ol_with_aria_label
  [Fact] FireControl_runbook_steps_use_aria_labelledby_not_aria_label
  [Fact] FireControl_incident_transition_announced_in_polite_region (SunfishA11yAssertions.IncidentStateTransitionAnnounced)

EmergencyStandingOrderDialogTests:
  [Fact] Dialog_uses_role_alertdialog_with_modal_label_described_by (SunfishA11yAssertions.AlertDialogHasRoleModalLabelDescribedBy)
  [Fact] Dialog_initial_focus_is_cancel_not_confirm
  [Fact] Dialog_confirm_aria_disabled_on_open
  [Fact] Dialog_confirm_enabled_at_2000ms_with_announcement (SunfishA11yAssertions.DeliberationPauseAnnouncesEnablement)
  [Fact] Dialog_outcome_announced_on_close (SunfishA11yAssertions.DialogOutcomeAnnouncedOnClose)
  [Fact] Dialog_consequence_text_shows_post_substitution_not_tokens
```

**Pre-merge council required.** WCAG/a11y subagent mandatory (alertdialog + deliberation-pause pattern + focus trap). Security-engineering subagent mandatory (consequence text preview — must not leak unexpanded template syntax to end users).

---

## Phase 4 — `LookoutQuarterdeckAlertSource` + docs + ledger

**Gate:** W#51 Phase 1+ merged (`IQuarterdeckAlertSource` + `QuarterdeckAlert` on origin/main); Phases 3a/3b merged.
**Estimated effort:** ~2-3h
**PR scope:** `LookoutQuarterdeckAlertSource` + XML docs + apps/docs stub + ledger flip

### 4.1 `LookoutQuarterdeckAlertSource`

Location: `packages/blocks-tactical/LookoutQuarterdeckAlertSource.cs`

Normative contract per ADR 0081 §7.2:

```csharp
using Sunfish.Foundation.Tactical;
using Sunfish.Foundation.Quarterdeck;  // IQuarterdeckAlertSource, QuarterdeckAlert (W#51)
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Tactical;

public sealed class LookoutQuarterdeckAlertSource : IQuarterdeckAlertSource
{
    public string SourceName => "sunfish.tactical.lookout";

    // Constructor-inject ILookout + ITenantContext + IAuditTrail

    public async ValueTask<IReadOnlyList<QuarterdeckAlert>> GetAlertsAsync(
        TenantId tenantId, CancellationToken ct = default)
    {
        // Step 1: Resolve ambient tenant; verify tenantId == ambient.TenantId.
        //   On mismatch: emit TacticalAuthorizationDenied(denialReason="tenant-mismatch"); return empty list.
        // Step 2: Delegate to ILookout.GetActiveLookoutAlerts(tenantId).
        // Step 3: Filter alert.TenantId == tenantId (defense-in-depth).
        // Step 4: Map TacticalAlert → QuarterdeckAlert with VisibilityPolicy = OmitForDeniedActors.
        // Step 5: Sort DetectedAt DESC; return at most 50 items.
        ...
    }
}
```

### 4.2 XML documentation

Add XML `<summary>` docs to all public interfaces and record types in `foundation-tactical`. Focus on:
- `ITacticalRule.Evaluate`: "MUST NOT do I/O; state needed for evaluation MUST be pre-populated externally."
- `ILookout.SubscribeLookoutAsync`: explicit note about Acknowledged status-changes going to polite channel, not assertive.
- `IThreatTriggerService.TryIssueAsync`: explicit note about internal principal resolution (not caller-supplied).
- `ISystemPrincipalProvider`: explicit note about ShipRole.System not being assignable to human actors.

### 4.3 apps/docs stub

Create `apps/docs/blocks/tactical.md` with API reference stub. At minimum: package name, top-level types, sub-rooms list, dependency note (must not depend on foundation-wayfinder or foundation-engine-room directly).

### 4.4 Ledger flip

In `active-workstreams.md`, flip W#52 row status from `ready-to-build` → `built`. Update the changelog entry with PR number.

### 4.5 Phase 4 required tests

```
LookoutQuarterdeckAlertSourceTests:
  [Fact] SourceName_is_sunfish_tactical_lookout
  [Fact] GetAlertsAsync_rejects_tenant_mismatch
  [Fact] GetAlertsAsync_filters_to_tenantId_defense_in_depth
  [Fact] GetAlertsAsync_maps_TacticalAlert_to_QuarterdeckAlert_with_OmitForDeniedActors
  [Fact] GetAlertsAsync_caps_at_50_items
  [Fact] GetAlertsAsync_sorts_DetectedAt_desc
```

**Pre-merge council recommended** (lighter — no new security surface; WCAG/a11y subagent not required for this phase).

---

## Appendix A — Signal payload schemas (Phase 1 reference)

These are informational for Phase 1 test authoring. Full normative table in ADR 0081 §1.1.

| Kind | Required keys |
|---|---|
| `DecryptionFailureSpike` | documentCount (int), eventCount (int), windowSeconds (int) |
| `PeerConnectivityLoss` | activePeerCount (int), configuredFloor (int) |
| `MergeConflictRate` | conflictsPerMinute (double), thresholdPerMinute (double) |
| `CrdtGrowthAnomaly` | documentId (string), byteEstimate (long), growthRatePerHour (double) |
| `AuthorizationFailureSpike` | actorId (string), failureCount (int), windowSeconds (int) |
| `BulkAccessPattern` | actorId (string), documentCount (int), accessType (string: read/write/delete) |
| `ServiceDegradation` | serviceName (string), previousStatus (string), currentStatus (string) |
| `ProbeTimeout` | probeName (string), timeoutMs (int) |
| `StandingOrderViolation` | orderId (string), violationType (string) |
| `Custom` | Tenant-defined; no enforcement (§1.1 ADR 0081) |

---

## Appendix B — Forward references resolved at each phase

| Phase | Forward-refs needed | Unblocking workstream |
|---|---|---|
| Phase 1 | None from foundation-ship-common required for data model | — |
| Phase 2 | ShipAction catalog + IPermissionResolver (§8.1, §8.3) | W#46 Phase 1 |
| Phase 2 | IStandingOrderRepository.AppendAsync signature | W#42 already built ✓ |
| Phase 3a | ILiveAnnouncer + IFocusTrap + SunfishA11yAssertions | W#46 Phase 3 |
| Phase 3a | ITenantContext namespace confirm (ADR 0008 or ADR 0077) | W#46 Phase 1 |
| Phase 3b | (all from 3a) | W#46 Phase 3 |
| Phase 4 | IQuarterdeckAlertSource + QuarterdeckAlert | W#51 Phase 1 |

---

## Appendix C — Council subagent posture

| Phase | Subagents | Rationale |
|---|---|---|
| Phase 1 | Standard council + security-engineering | ISystemPrincipalProvider seam + AuditEventType additions |
| Phase 2 | Standard council + security-engineering (mandatory) | Threat-trigger authority chain §4 + tenant-binding §8.2 + audit-before-action §8.4 |
| Phase 3a | Standard council + WCAG/a11y (mandatory) | Assertive live region + reduced-motion + aria-disabled + severity non-color |
| Phase 3b | Standard council + WCAG/a11y + security-engineering (both mandatory) | alertdialog + deliberation-pause + consequence-text preview |
| Phase 4 | Standard council (recommended) | No new security/a11y surface |
