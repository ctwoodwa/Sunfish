# Wave 3 Sentinel Review — Cluster A

**Date:** 2026-04-25
**Reviewer:** Wave 3 Sentinel Reviewer
**Cluster commit:** ffeec1e9
**Report commit:** 9a2ef462

## Per-criterion results

(a) **PASS** — All 4 packages have all 4 expected files. Verified via `git show --name-only ffeec1e9 | sort`: 16 files total = 4 packages × {`Resources/Localization/SharedResource.resx`, `Resources/Localization/SharedResource.ar-SA.resx`, `Localization/SharedResource.cs`, `DependencyInjection/<X>ServiceCollectionExtensions.cs`}.

(b) **PASS** — RESX schema matches foundation byte-for-byte on the namespace structure (`xsd:schema` block, `resheader` quartet) and on the 8-key set (`severity.{info,warning,error,critical}`, `action.{save,cancel,retry}`, `state.loading`). Spot-checked all 4 en-US files; key names and ordering identical to `packages/foundation/Resources/Localization/SharedResource.resx`.

(c) **PASS** — All 8 RESX files have exactly 8 `<data name=...>` entries each. Verified via `grep -c '<data name='` across all 4 packages × 2 locales: 8 / 8 / 8 / 8 / 8 / 8 / 8 / 8.

(d) **PASS** — Every `<data>`-element `<comment>` starts with the exact literal `[scaffold-pilot — replace in Plan 6]`. Precise per-element extraction `grep -oE '<comment>[^<]*</comment>'` filtered through the inverse prefix match returned empty across all 8 files. (Header `<!--…-->` block mentions of `<comment>` are documentation, not data-element comments.)

(e) **PASS** — DI registration is idempotent. All 4 extensions use `services.TryAdd(ServiceDescriptor.Singleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>)))`. `TryAdd` is no-op on duplicate registration by design (vs. `Add`, which would stack). Pre-existing service registrations preserved unchanged in each file.

(f) **PASS** — Namespaces match package convention:
- `Sunfish.Blocks.Accounting.Localization`
- `Sunfish.Blocks.TaxReporting.Localization`
- `Sunfish.Blocks.RentCollection.Localization`
- `Sunfish.Blocks.Subscriptions.Localization`

All four marker classes are `public sealed class SharedResource { }`, aligning with the foundation precedent (and with the cluster-freeze v1.3 amendment correcting `internal` → `public`).

(g) **PASS** — `dotnet build` succeeded for all 4 projects with **0 Warning(s), 0 Error(s)** each. No `SUNFISH_I18N_001` analyzer warnings emitted. Build times: accounting 5.81s, tax-reporting 3.45s, rent-collection 19.75s, subscriptions 10.87s.

(h) **PASS** — Commit message contains `Token: wave-2-cluster-A` (verified via `git log -1 --format=%B ffeec1e9`).

(i) **PASS — DIFF-SHAPE OK** — v1.3 tightened regex run against the clean file list (16 paths) returned no out-of-scope matches. Only the four expected file types per package; no `.csproj`, README, samples, or other files touched.

(j) **PASS (v1.3 Seat-2 P5)** — No unescaped `<`, `>`, or `&` characters inside any `<comment>` element across all 8 RESX files. Verified by extracting per-comment text and grepping for raw metacharacters; result was empty. Em-dashes (`—`, U+2014) and ellipses (`…`, U+2026) are the only non-ASCII content and are XML-safe.

## Deviation evaluation: AddLocalization() not added per-block

**ACCEPTABLE.**

Three converging lines of evidence support ratification:

1. **Bridge precedent is real and explicit.** `accelerators/bridge/Sunfish.Bridge/Localization/ServiceCollectionExtensions.cs` line 22-23 documents: "Caller is responsible for adding `AddLocalization()` + the request-localization middleware." `accelerators/bridge/Sunfish.Bridge/Program.cs` is where `builder.Services.AddLocalization(options => options.ResourcesPath = "Resources")` actually lives. Same pattern in `accelerators/anchor/MauiProgram.cs`. The sentinel's deviation matches the established Bridge contract verbatim.

2. **The v1.3 plan's diff-shape constraint is a hard gate.** Adding `services.AddLocalization()` to a class library that doesn't already reference `Microsoft.Extensions.Localization` (only `.Abstractions`) requires a `<PackageReference>` edit in the `.csproj` — which the v1.3 tightened diff-shape regex (criterion i) explicitly excludes. The two constraints were in direct conflict; sentinel correctly resolved by honoring the architectural precedent and the diff-shape gate, while flagging the brief defect.

3. **Pattern A/B distinction in the freeze doc supports the resolution.** The freeze (lines 39-47) already established that Pattern B packages don't get DI registration (consumers wire it). Cluster A is Pattern A only, but the underlying principle — **blocks contribute the open-generic binding; composition roots wire `AddLocalization()`** — applies uniformly. The deviation generalizes the Pattern B convention to Pattern A, which is architecturally cleaner than the brief's mandate.

The brief was wrong to mandate `AddLocalization` per block. The cluster fan-out should propagate the deviation: subagents B/C/D1 should follow the same TryAdd-only pattern; cluster E (kitchen-sink composition root) should carry the actual `AddLocalization()` call for all consumed blocks.

## Final verdict: GREEN

All 10 mechanical checklist items pass with full evidence. The named deviation (`AddLocalization()` deferred to composition root) is architecturally correct, traces to a documented Bridge precedent, and resolves a real conflict between the original brief and the v1.3 diff-shape gate. The cluster pattern is sound and ready to fan out to clusters B, C, D1, and E. Fan-out subagents should be briefed to inherit the deviation: contribute open-generic `ISunfishLocalizer<>` via `TryAdd` only; never call `AddLocalization()` from a class library; consumers (apps/accelerators) own that call. Cluster E (kitchen-sink composition root) is the natural place to consolidate the `AddLocalization()` wiring for all Pattern-A and Pattern-B blocks together.
