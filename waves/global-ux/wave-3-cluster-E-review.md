# Wave 3 Review — Cluster E (composition root)

**Date:** 2026-04-25
**Cluster commit:** 33ec91fe
**Report commit:** ca840121
**Reviewer:** Wave 3 reviewer (v1.3 trust boundary)

## Per-criterion results

**(a) Expected files present — PASS**
`git show --name-only 33ec91fe` produces exactly four paths:
- `apps/kitchen-sink/Localization/SharedResource.cs`
- `apps/kitchen-sink/Program.cs`
- `apps/kitchen-sink/Resources/Localization/SharedResource.ar-SA.resx`
- `apps/kitchen-sink/Resources/Localization/SharedResource.resx`

All four match the specification. (Note: the kitchen-sink csproj sits at `apps/kitchen-sink/Sunfish.KitchenSink.csproj`, i.e. the project root *is* `apps/kitchen-sink/`, not a nested `Sunfish.KitchenSink/` subdirectory. The brief's path examples assumed a Bridge-style nested project; the repo's actual layout is flat. Files are correctly placed relative to the csproj — `Resources/Localization/` and `Localization/` siblings to `Program.cs`.)

**(b) RESX schema matches foundation byte-for-byte — PASS**
`diff` of the `<xsd:schema>…</xsd:schema>` block of both kitchen-sink resx files against `packages/foundation/Resources/Localization/SharedResource.resx` returned zero deltas. Output: `SCHEMA-IDENTICAL en-US` and `SCHEMA-IDENTICAL ar-SA`.

**(c) ar-SA key count == en-US key count == 8 — PASS**
`grep -c '<data '` returned 8 for both files. `diff` of sorted `data name="…"` lists produced no output (`KEYS IDENTICAL`). The eight keys: `severity.{info,warning,error,critical}`, `action.{save,cancel,retry}`, `state.loading`.

**(d) Every `<data>` has non-empty `<comment>` matching `^\[scaffold-pilot — replace in Plan 6\]` — PASS**
`grep -c '<comment>\[scaffold-pilot — replace in Plan 6\]'` returned 8 for both files. All sixteen `<comment>` bodies inspected (en-US lines 47, 51, 55, 59, 63, 67, 71, 75; ar-SA lines 31, 35, 39, 43, 47, 51, 55, 59) start with the exact bracketed token followed by translator context.

**(e) Composition-root has BOTH AddLocalization() AND TryAddSingleton(ISunfishLocalizer<>, SunfishLocalizer<>) — PASS**
`apps/kitchen-sink/Program.cs:53–54`:
```
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));
```
Exactly one of each call (verified by line-by-line grep). The TryAddSingleton form matches every Pattern A block (blocks-leases, blocks-inspections, blocks-maintenance, blocks-businesscases, blocks-tenant-admin, ui-adapters-blazor renderers) — confirming kitchen-sink correctly layers the AddLocalization composition-root call on top of the canonical localizer registration. Idempotent if Bridge or Anchor later compose the kitchen-sink graph (TryAddSingleton no-ops; AddLocalization itself is internally idempotent in Microsoft.Extensions.Localization).

**(f) Namespace matches kitchen-sink convention — PASS**
`SharedResource.cs:1` declares `namespace Sunfish.KitchenSink.Localization;`. Csproj sets `<RootNamespace>Sunfish.KitchenSink</RootNamespace>` and `<AssemblyName>Sunfish.KitchenSink</AssemblyName>`. The `.Localization` suffix mirrors the foundation precedent (`Sunfish.Foundation.Localization.SharedResource`).

**(g) `dotnet build apps/kitchen-sink/Sunfish.KitchenSink.csproj` clean — PASS**
Build output: `Build succeeded. 1 Warning(s) 0 Error(s)`. The single warning is `CS0162: Unreachable code detected` in a generated _Imports/Razor temp file — pre-existing, explicitly noted as acceptable in the brief and the subagent report. NETSDK1057 (preview SDK) messages appear but are informational, not warnings. **No SUNFISH_I18N_001 emitted**, which is the load-bearing analyzer signal — confirming all 16 `<comment>` entries satisfy the non-empty rule and the analyzer is wired through the Directory.Build.props cascade (per d4dc625e).

**(h) Commit message contains `wave-2-cluster-E` token — PASS**
`git log --format=%B -n 1 33ec91fe` body line: `Token: wave-2-cluster-E`. Subject also references `wave-2-cluster-E`.

**(i) Diff-shape regex — PASS**
After stripping the commit header (`git show --name-only --format= 33ec91fe`), the v1.3 negative-match grep printed `DIFF-SHAPE OK`. All four touched paths match an allowed pattern (two RESX, one Localization/SharedResource.cs, one Program.cs). No out-of-scope files.

**(j) `<comment>` content has no unescaped `<`, `>`, `&` — PASS**
`grep -nP '<comment>[^<]*[<>&][^<]*</comment>'` filtered for non-entity occurrences returned no hits in either file (`no unescaped < > & in comments`). All comments use plain prose plus em-dash (U+2014) and horizontal-ellipsis (U+2026); no entity-eligible characters appear.

## Bridge-precedent mirror check — PASS

Compared `apps/kitchen-sink/Program.cs:53` against `accelerators/bridge/Sunfish.Bridge/Program.cs:298`:

- Bridge (line 298): `builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");`
- Kitchen-sink (line 53): `builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");`

Identical call shape and `ResourcesPath = "Resources"` literal. Both place the call inside the composition root *after* core service registration (Bridge: after `AddRazorComponents`/`AddSignalR`; kitchen-sink: after `AddSunfish().AddSunfishInteropServices()`). Kitchen-sink registers the localizer via direct `TryAddSingleton`; Bridge layers an additional `AddSunfishLocalizedProblemDetails()` wrapper because Bridge owns ProblemDetails culture flow (a SaaS-posture concern kitchen-sink does not have). The TryAddSingleton ISunfishLocalizer<> registration is the canonical Pattern A line shared with every blocks-* package, so the kitchen-sink form is the framework-agnostic floor and Bridge's wrapper is an additive accelerator concern — no contradiction.

The block comment at lines 47–52 of Program.cs explicitly cites the Cluster A sentinel ratification ("Pattern B packages … cannot self-register; ISunfishLocalizer<> is a TryAddSingleton so accelerator hosts (Bridge/Anchor) that already wired it remain idempotent if they later compose the kitchen-sink graph") — narrative aligns with intent.

## Final verdict: GREEN
