---
type: directive
workstream-or-chapter: W#1 WS-A security follow-up + WS-B sequencing
last-pr: 689
---

**ACTION REQUIRED: Ship W#1 security follow-up PR before starting WS-B or any other work.**

PR #688 (WS-A) merged before security council completed. 6 must-fix items are now on
origin/main. WS-B is HELD until this follow-up ships (MF-1 changes TenantSelection.Matches).

Full code-level spec is in auto-memory `project_workstream_01_adr_0084.md`. Summary:

**MF-1 — `AllAccessible.Matches` must exclude system sentinels**
File: `packages/foundation-multitenancy/TenantSelection.cs`
Add to `TenantId.cs`: `public bool IsSystemSentinel => Value?.StartsWith("__", StringComparison.Ordinal) ?? true;`
Change AllAccessible arm: `AllAccessible => !tenantId.IsSystemSentinel,`
Add test: `Matches_AllAccessible_ExcludesSystemSentinels` (Assert.False for TenantId.System).

**MF-2 — `InMemorySubscriptionService` null context → throw**
File: `packages/blocks-subscriptions/Services/InMemorySubscriptionService.cs`
`private TenantId CurrentTenant => _tenantContext is null ? throw new InvalidOperationException("InMemorySubscriptionService requires ITenantContext.") : new TenantId(_tenantContext.TenantId);`

**MF-3 — `NativeChannelProvider` Phase-1 → dedicated sentinel**
File: `packages/blocks-crew-comms/NativeChannelProvider.cs`
`private static TenantId GetDefaultTenant() => new TenantId("crew-comms-single-tenant-v1");`

**MF-4 — JSON sentinel deserialization rejection test**
File: `packages/foundation/tests/Assets/Common/IdentityTypeJsonConverterTests.cs`
Add: `TenantId_DeserializeSystemSentinel_Throws` asserting
`JsonSerializer.Deserialize<TenantId>("\"__system__\"")` throws.

**MF-5 — `ForMultiple` primary positional ctor must reject empty arrays**
File: `packages/foundation-multitenancy/TenantSelection.cs`
Convert `ForMultiple` from positional record to manual body with
`if (tenantIds.IsDefaultOrEmpty) throw new ArgumentException(...)`.
Add test: `ForMultiple_EmptyImmutableArray_Throws`.

**MF-6 — `TenantSelection.All` static field (ADR spec gap)**
File: `packages/foundation-multitenancy/TenantSelection.cs`
After AllAccessible class: `public static readonly TenantSelection All = new AllAccessible();`

**PR discipline:** create as `--draft`, run council (adversarial + security subagents at
minimum), wait for ALL council agents to return, push amendments, then `gh pr ready`.
This PR is small (~20-30 lines changed) but security-critical. Council mandatory.

**After follow-up ships:** flip W#1 WS-B ledger to `ready-to-build` (WS-B hand-off at
`icm/_state/handoffs/tenant-selection-wsb-stage06-handoff.md` is already authored).

**What would unblock me:** Nothing — all code details are above. Ship this PR first.
