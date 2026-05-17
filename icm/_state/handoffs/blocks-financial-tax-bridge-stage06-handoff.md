---
workstream: blocks-financial-tax-bridge
cluster: blocks-financial-tax-bridge (new tiny package)
pipeline: sunfish-feature-change
routing: substrate-bridge
owner: dev (Sunfish overflow) OR cob (fallback)
state: built
stage-02-source: dev's blocks-financial-ap-stage06-handoff completion note + AR's `ITaxCalculator` XML doc reference to "bridge adapter ... separate package"
depends-on:
  - blocks-financial-tax ✓ (shipped on main)
  - blocks-financial-ar  ✓ (shipped on main; ITaxCalculator interface present)
  - blocks-financial-ap  ✓ (PR #960 + #963 MERGED 2026-05-17; gate cleared)
authored-by: XO
authored-at: 2026-05-17T09-20Z
estimated-effort: ~30 LOC + tests (one short PR)
co-pre-authorized: granted
co-pre-authorized-rationale: |
  Single-PR bridge adapter; ~30 LOC. Pure delegation to canonical tax service.
  No security surface, no new abstraction. Matches pattern-005 (DI extension umbrella).
  Pilot workstream for standing-approved-patterns + pre-authorization protocol rollout.
co-pre-authorized-scope:
  - PR 1 (the only PR in this workstream) if changeset matches standing-approved-patterns.md
    pattern-005 criteria and carries @standing-pattern: pattern-005 in PR description
  - PR-count maximum: 1. Any second PR requires XO + CO re-authorization.
  - @deviation-from-spec flag triggers immediate escalation to CO before merge
co-pre-authorized-at: 2026-05-17T12-00Z
merge-tier: pre-authorized
---

# `blocks-financial-tax-bridge` — Stage 06 Hand-off

## 1. Context & scope

AR and AP both declare a local **`ITaxCalculator`** abstraction (`Task<decimal> CalculateAsync(string? taxCodeId, decimal taxableBase, DateOnly transactionDate, CancellationToken)`) — intentionally decoupled from `blocks-financial-tax`'s richer surface (`ITaxCalculationService` returns a full `TaxCalculationResult` with breakdown rows, jurisdiction info, error channel). This was the right call during the AR + AP build: AR/AP shipped without taking a hard dependency on the tax cluster.

AR's `ITaxCalculator.cs` XML doc explicitly anticipates this hand-off:

> A bridge adapter between this interface and `Sunfish.Blocks.FinancialTax.Services.ITaxCalculationService` **can land in a separate package**; AR consumers register that adapter when they want real tax. Without it, `NoOpTaxCalculator` returns zero.

**This hand-off lands that separate package.** Tiny — ~30 LOC of adapter + ~50 LOC of tests + ~10 LOC of DI. Single PR.

### What this package is

- A bridge: two adapter classes (one for AR's local interface, one for AP's) that both wrap the canonical `Sunfish.Blocks.FinancialTax.Services.ITaxCalculationService`
- A DI extension `AddBlocksFinancialTaxBridge()` that overrides AR's + AP's `NoOpTaxCalculator` registrations with the real adapters
- Mapping: local `(taxCodeId, taxableBase, transactionDate)` → canonical `TaxCalculationInput`; canonical `TaxCalculationResult.TaxAmount` → local `decimal` return; canonical errors → return `0m` per local interface contract

### What this package is NOT

- Not a new tax engine — pure adapter
- Not a replacement for `NoOpTaxCalculator` in AR/AP packages — those stay as default (no transitive dependency on tax cluster from AR/AP)
- Not opinionated about which jurisdiction model to apply — that's still inside `blocks-financial-tax`
- Not a place for AR/AP-specific tax extensions — only the canonical adapter

### Why a separate package (vs. living inside `blocks-financial-tax`)

- **Tax cluster cannot depend on AR or AP** (consumer dependencies must point toward substrate, never away)
- **AR/AP cannot depend on tax cluster** (intentional decoupling so AR/AP ship without tax)
- The bridge package is the *only* place all three can coexist

## 2. Architecture

```
blocks-financial-ar      blocks-financial-ap
  └─ ITaxCalculator        └─ ITaxCalculator
        ▲                        ▲
        │                        │
        │    implements          │
        │                        │
        └────┐                ┌──┘
             │                │
   ┌─────────┴────────────────┴─────────┐
   │  blocks-financial-tax-bridge       │
   │   ├─ ArTaxCalculatorAdapter        │
   │   ├─ ApTaxCalculatorAdapter        │
   │   └─ AddBlocksFinancialTaxBridge() │
   └────────────┬──────────────────────┘
                │
                │ delegates to
                ▼
   ┌──────────────────────────────────┐
   │  blocks-financial-tax             │
   │   └─ ITaxCalculationService       │
   └──────────────────────────────────┘
```

## 3. PR breakdown — one PR total

### PR 1 — Scaffold + 2 adapters + DI extension + docs

**Files to create:**

```
packages/blocks-financial-tax-bridge/
├── Sunfish.Blocks.FinancialTaxBridge.csproj
├── README.md
├── NOTICE.md  (MIT, clean-room original)
├── Adapters/
│   ├── ArTaxCalculatorAdapter.cs   (~15 LOC)
│   └── ApTaxCalculatorAdapter.cs   (~15 LOC; identical shape to ArTaxCalculatorAdapter)
├── DependencyInjection/
│   └── FinancialTaxBridgeServiceCollectionExtensions.cs   (~20 LOC)
└── tests/
    ├── Sunfish.Blocks.FinancialTaxBridge.Tests.csproj
    ├── ArTaxCalculatorAdapterTests.cs   (~6 cases)
    └── ApTaxCalculatorAdapterTests.cs   (~6 cases)
```

**Project references (`Sunfish.Blocks.FinancialTaxBridge.csproj`):**

```xml
<ProjectReference Include="..\blocks-financial-tax\Sunfish.Blocks.FinancialTax.csproj" />
<ProjectReference Include="..\blocks-financial-ar\Sunfish.Blocks.FinancialAr.csproj" />
<ProjectReference Include="..\blocks-financial-ap\Sunfish.Blocks.FinancialAp.csproj" />
```

**Adapter shape (both follow this pattern — `ArTaxCalculatorAdapter` shown; `ApTaxCalculatorAdapter` is the same body in the AP namespace):**

```csharp
using Sunfish.Blocks.FinancialAr.Services;
using CanonicalTax = Sunfish.Blocks.FinancialTax.Services;
using TaxCodeId    = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using TaxModels    = Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTaxBridge.Adapters;

/// <summary>
/// Bridges AR's local <see cref="ITaxCalculator"/> to the canonical
/// <see cref="CanonicalTax.ITaxCalculationService"/>. Returns 0 when
/// the canonical call reports an error (per local interface contract).
/// </summary>
public sealed class ArTaxCalculatorAdapter : ITaxCalculator
{
    private readonly CanonicalTax.ITaxCalculationService _canonical;

    public ArTaxCalculatorAdapter(CanonicalTax.ITaxCalculationService canonical)
        => _canonical = canonical ?? throw new ArgumentNullException(nameof(canonical));

    public async Task<decimal> CalculateAsync(
        string? taxCodeId,
        decimal taxableBase,
        DateOnly transactionDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taxCodeId))
            return 0m;

        var input = new TaxModels.TaxCalculationInput(
            TaxCodeId:        new TaxCodeId(taxCodeId),
            Subtotal:         taxableBase,
            TransactionDate:  transactionDate);

        var result = await _canonical.CalculateAsync(input, cancellationToken).ConfigureAwait(false);
        return result.Error is null ? result.TaxAmount : 0m;
    }
}
```

**DI extension:**

```csharp
namespace Sunfish.Blocks.FinancialTaxBridge.DependencyInjection;

public static class FinancialTaxBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the canonical-tax bridge. AR's + AP's
    /// <c>NoOpTaxCalculator</c> registrations are REPLACED (not
    /// merely augmented) with adapters that delegate to
    /// <see cref="ITaxCalculationService"/>. Call AFTER
    /// <c>AddBlocksFinancialAr()</c> + <c>AddBlocksFinancialAp()</c>
    /// + <c>AddBlocksFinancialTax()</c>.
    /// </summary>
    public static IServiceCollection AddBlocksFinancialTaxBridge(this IServiceCollection services)
    {
        // Replace AR + AP NoOp registrations (Replace<TService, TImpl>() preserves lifetime).
        services.Replace(ServiceDescriptor.Singleton<
            Sunfish.Blocks.FinancialAr.Services.ITaxCalculator,
            Adapters.ArTaxCalculatorAdapter>());

        services.Replace(ServiceDescriptor.Singleton<
            Sunfish.Blocks.FinancialAp.Services.ITaxCalculator,
            Adapters.ApTaxCalculatorAdapter>());

        return services;
    }
}
```

**Tests (per adapter — 6 cases × 2 adapters = 12 tests total):**

1. `Returns_Zero_When_TaxCodeId_Null`
2. `Returns_Zero_When_TaxCodeId_WhiteSpace`
3. `Returns_TaxAmount_When_Canonical_Succeeds` (mocks canonical to return `new TaxCalculationResult(TaxAmount: 7.50m, Error: null, Breakdown: [...])`)
4. `Returns_Zero_When_Canonical_Errors` (mocks canonical to return `new TaxCalculationResult(Error: "unknown-code")`)
5. `Forwards_TransactionDate_Verbatim`
6. `Forwards_CancellationToken` (uses a pre-cancelled token; asserts canonical sees it)

Use the simplest mock — a hand-rolled `FakeTaxCalculationService` implementing `ITaxCalculationService` with public assertions. No Moq/NSubstitute dependency added.

## 4. Acceptance criteria

- [ ] 12/12 tests pass; build clean
- [ ] DI extension order documented in `README.md`: tax-bridge must be called AFTER AR/AP/Tax registrations
- [ ] `README.md` includes the "what / why / when to use" sections plus a single 10-line wiring example showing AR invoice posting picking up real tax
- [ ] Apps/docs page at `apps/docs/blocks/financial-tax-bridge/overview.md` (mirrors AR's overview structure; ~30 lines)
- [ ] `Sunfish.slnx` updated to include the new project + test project
- [ ] No new package adds a dependency on this bridge — it's opt-in via DI

## 5. Pre-merge council requirements

- **security-engineering:** NOT required (pure delegation; no auth/crypto/policy surface)
- **.NET architect:** NOT required (single-purpose adapter mirroring existing pattern; no new abstractions)

## 6. Idempotency-key catalog

N/A — adapter is stateless and emits no events.

## 7. Dependencies + sequence

- **Blocking dependency:** PR #960 (AP PR 2) must merge first — AP's `ITaxCalculator` interface lands there. Until then, the AP adapter side cannot reference the type.
- **No other blockers.** AR side can be built immediately; deferring until AP also lands keeps the bridge single-PR.
- **Sequence:** wait for AP #960 + #963 to merge → build this single PR → 1 hour total dev time

## 8. License posture (per ADR 0088 §3)

- **MIT clean-room.** Original adapter pattern; no FOSS source studied.
- Adapter pattern itself is industry-standard (GoF; not borrowed from any specific source).
- `NOTICE.md` declares MIT.

## 9. Done conditions

- [ ] Single PR merged
- [ ] AR + AP posting-service tests rerun green when wired via this bridge (existing tests still use `NoOpTaxCalculator`; bridge adds an integration-style test)
- [ ] Bridge does NOT change AR/AP package contracts — no version bump on those
- [ ] No ledger row needed (single-PR chore-grade scope; can be tracked under W#60 Phase 5 or under a fresh "Wxx-tax-bridge" if CO prefers a row)

## 10. Notes for the picker-upper

- This is a chore-grade PR. Mechanical mirror of existing patterns.
- Do NOT widen scope to "rewrite AR/AP to use canonical tax directly" — that violates the intentional decoupling.
- If `ITaxCalculationService.CalculateAsync` signature changes after this lands, only this package needs the update (that's the point of the bridge).
- If a third consumer (rent collection? payroll?) needs the same bridge, add a third adapter here — don't fan out new packages.
