# Wave 3 Review — Cluster C

**Date:** 2026-04-25
**Cluster commit:** af73c89f
**Report commit:** ab184e7b

## Per-criterion results

(a) **PASS (with deferral noted)** — All 6 packages present.
- Pattern A (4 files): `blocks-businesscases`, `blocks-leases`, `blocks-tenant-admin` — confirmed 4 files each (resx en-US, resx ar-SA, marker `.cs`, DI extensions `.cs`).
- Pattern B (3 files): `blocks-forms`, `blocks-tasks` — confirmed 3 files each.
- `blocks-workflow` ships with **3 files** (Pattern-B-shaped) — DI registration deferred per Deviation 2 documented in cluster report. Total: 4+4+4+3+3+3 = 21 files (matches `git diff-tree` output).

(b) **PASS** — RESX schema matches foundation byte-for-byte. The `xsd:schema` block, `resheader` quartet (resmimetype, version, reader, writer), and namespace structure are identical across all 6 packages and `packages/foundation/Resources/Localization/SharedResource.resx`. Spot-checked workflow + businesscases en-US.

(c) **PASS** — Each package has exactly 8 keys per locale, identical key sets across all 12 RESX files: `severity.{info,warning,error,critical}`, `action.{save,cancel,retry}`, `state.loading`. Verified via `grep -c '<data name='` (returned 8 each) and sorted key-name diff (zero deltas).

(d) **PASS** — Every `<data>` element has a non-empty `<comment>` opening with the exact literal `[scaffold-pilot — replace in Plan 6]`. Verified across all 12 RESX files (96 data entries total) — none missing the prefix.

(e) **PASS for the 3 Pattern-A packages that registered** — `BusinessCasesServiceCollectionExtensions.cs`, `LeasesServiceCollectionExtensions.cs`, `TenantAdminServiceCollectionExtensions.cs` each call `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));` — idempotent open-generic registration. `using Microsoft.Extensions.DependencyInjection.Extensions;` correctly imported. (Workflow's registration is the deferred deviation — see below.)

(f) **PASS** — Namespaces: `Sunfish.Blocks.{BusinessCases,Forms,Leases,TenantAdmin,Tasks,Workflow}.Localization`. Each marker is `public sealed class SharedResource { }` matching the v1.3 amended cluster freeze (`public`, not `internal`).

(g) **PASS** — Built all 6 packages locally at `af73c89f`: 0 errors, 0 warnings, no `SUNFISH_I18N_001`. Workflow builds clean despite the deferred DI line. (The report's mention of NETSDK1206 was not reproduced in my fresh build — pre-existing infra noise either way.)

(h) **PASS** — Commit body contains `Token: wave-2-cluster-C`.

(i) **PASS — DIFF-SHAPE OK** — Ran the v1.3 tightened regex via `git diff-tree --no-commit-id --name-only -r af73c89f`; no out-of-scope files. The non-standard `src/` allowance for workflow's DI was not exercised since that file is not in this commit (deferred). All 21 paths matched the canonical resx/marker/extensions patterns.

(j) **PASS (v1.3 Seat-2 P5)** — No unescaped `<`, `>`, or `&` inside any `<comment>` element across all 12 RESX files. Per-comment Python regex extraction with explicit XML-entity allowlist returned zero hits. Em-dashes (—, U+2014) and ellipses (…, U+2026) are the only non-ASCII characters and are XML-safe.

## Deviation evaluation

### 1. workflow Pattern-A DI deferred — **ACCEPTABLE**

**Verified:** `git show af73c89f:packages/blocks-workflow/Sunfish.Blocks.Workflow.csproj` lists only `Microsoft.Extensions.DependencyInjection` as a `PackageReference` and has zero `ProjectReference` entries. The subagent's claim is true: adding `using Sunfish.Foundation.Localization;` would emit CS0246/CS1574.

The plan at line 559 (Task 2.A brief, inherited by all clusters) is binding: *"If you discover you NEED to touch another file (e.g., add a `<ProjectReference>` to foundation), document the need in your report and STOP without committing."* The subagent followed that directive precisely — they did not add a `.csproj` edit, they documented the gap, and they shipped workflow as Pattern-B-shaped (resources + marker only). Deviation 2 in the report is the prescribed escalation path, not a violation. End-user impact is zero in Wave 2 because no consumer resolves `ISunfishLocalizer<SharedResource>` against workflow yet.

Rejecting on the alternative (committing the `.csproj` edit anyway) would have failed the diff-shape automated check (Task 3.diff) and tripped the wave-level halt — strictly worse than YELLOW deferral. Follow-up commit must add the `ProjectReference` + DI line; tracker should carry an explicit ticket.

### 2. workflow marker `cref` rewritten as `<c>` plain text — **ACCEPTABLE**

Same root cause as Deviation 1 (no foundation/abstractions in transitive closure → `<see cref="IStringLocalizer{T}"/>` triggers CS1574). Rewriting as `<c>Microsoft.Extensions.Localization.IStringLocalizer&lt;T&gt;</c>` is a cosmetic XML-doc fix that preserves reader semantics. Other 5 packages keep `<see cref>`. No coding-standards violation; the symbol is named in full text. Will auto-correct itself when the follow-up commit adds the foundation reference.

## Final verdict: **YELLOW**

YELLOW (mirrors subagent self-verdict). All ten reviewer-criteria pass on what shipped; the cluster builds clean and the diff-shape regex is GREEN. Deviation 1 is the plan-prescribed STOP-and-document path under §559, executed correctly — workflow's resources + marker land safely while the missing `<ProjectReference>` is escalated for human/follow-up authorization. Deviation 2 is cosmetic and self-healing once Deviation 1 closes. Recommend reviewer ratification with a tracker entry: "follow-up commit — add `<ProjectReference Include=\"..\\foundation\\Sunfish.Foundation.csproj\" />` to `Sunfish.Blocks.Workflow.csproj`, then add `TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>))` + restore `<see cref>` form in the marker XML doc." Cluster cascade may proceed.
