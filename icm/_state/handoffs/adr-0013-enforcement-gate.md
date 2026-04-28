# Hand-off — ADR 0013 Provider-Neutrality Enforcement Gate

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-28
**Status:** `ready-to-build`
**Spec source:** ADR 0013 audit (`icm/07_review/output/adr-audits/0013-upf-audit.md` finding C-1) + UPF plan (in-conversation 2026-04-28 — Quality grade A; user approved)
**Approval:** user said "approved" 2026-04-28
**Estimated cost:** ~3 hours sunfish-PM time, one PR
**Pre-Phase-2 urgency:** must land before the first `providers-*` package is scaffolded

---

## Context (one paragraph)

ADR 0013 declares vendor-neutrality as load-bearing: domain code in `blocks-*` and `foundation-*` must NOT reference vendor SDKs (Stripe, Plaid, SendGrid, Twilio); only `providers-*` packages may. Today this is socially enforced ("reviewers reject violations"). Phase 2 commercial work is about to scaffold the first `providers-*` packages — without a mechanical gate first, vendor references will leak into the wider codebase the moment a developer slips, multiplying future swap costs by N callers.

This hand-off ships the mechanical gate.

---

## Approach (UPF-graded A; layered)

**Layer 1 — Roslyn analyzer:** broad pattern rule. Forbids any project under `packages/blocks-*/` or `packages/foundation-*/` (with explicit exclusions) from referencing vendor SDK namespaces.

**Layer 2 — `BannedSymbols.txt` via `Microsoft.CodeAnalysis.BannedApiAnalyzers`:** one-off symbol bans. Cheap to extend later when a specific landmine surfaces.

The two layers are complementary: Layer 1 covers the broad pattern (no Stripe in `blocks-*`); Layer 2 covers narrow exceptions (e.g., "ban `System.Web.HttpUtility` everywhere — even in `providers-*`").

---

## Phases (binary gates)

### Phase 1 — Scaffold the analyzer project (zero rules)

**Files:**

- **NEW** `packages/analyzers/provider-neutrality/Sunfish.Analyzers.ProviderNeutrality.csproj`
  - Mirror the existing `packages/analyzers/loc-comments/Sunfish.Analyzers.LocComments.csproj` setup (same SDK, same Microsoft.CodeAnalysis.CSharp.Analyzers + Microsoft.CodeAnalysis.Workspaces.Common references, same `IsRoslynComponent` true, same `EnforceExtendedAnalyzerRules` true).
- **NEW** `packages/analyzers/provider-neutrality/ProviderNeutralityAnalyzer.cs` — empty `[DiagnosticAnalyzer]` shell that registers no rules yet.

**Auto-wire via `Directory.Build.props`:**

- **MODIFY** `Directory.Build.props` — add a third `<ItemGroup>` mirroring the existing `loc-comments` and `loc-unused` blocks. Predicate: project is under `packages/blocks-*/` OR `packages/foundation-*/` (with the project-name exclusion list per Phase 2 below). Same `OutputItemType="Analyzer"` + `ReferenceOutputAssembly="false"` shape.

**PASS gate:** `dotnet build packages/analyzers/provider-neutrality/Sunfish.Analyzers.ProviderNeutrality.csproj` exits 0 with zero warnings; auto-attach to a sample `blocks-*` test project shows the analyzer loaded (verifiable via `/p:ReportAnalyzer=true` or similar).

### Phase 2 — Provider-neutrality rule active

**Banned namespace prefixes** (configurable via analyzer options or hard-coded for v0):

- `Stripe.*`
- `Plaid.*`
- `SendGrid.*`
- `Twilio.*`
- (Extensible — these are the Phase 2 vendors named in the Phase 2 commercial intake)

**Project predicate:**

- The rule applies to projects under `packages/blocks-*/` AND `packages/foundation-*/`
- **Exclude:** `Sunfish.Foundation.Integrations` (this IS the contract seam where vendor-neutral integration interfaces live; it must not reference vendor SDKs but it IS in foundation-* — confirm it's excluded by the PROJECT NAME, not the path, since the path matches)
- **Exclude:** any test project (test fixtures may use vendor SDK mocks if needed; revisit if false-positives surface)
- The rule does NOT apply to `packages/providers-*/` (correctly outside the predicate)

**Diagnostic output format:**

```
error SUNFISH_PROVNEUT_001: Type 'Stripe.PaymentIntent' is referenced from 'Sunfish.Blocks.Accounting.Models.Invoice' but vendor SDK references are restricted to packages/providers-* per ADR 0013 provider-neutrality policy.
```

**Files:**

- **MODIFY** `packages/analyzers/provider-neutrality/ProviderNeutralityAnalyzer.cs` — implement the rule. Use a `SymbolAnalysisContext` or `SyntaxNodeAnalysisContext` walker that inspects all type references; check the containing project's `MSBuildProjectName` MSBuild property (available via `AnalyzerConfigOptions`); emit the diagnostic if predicate matches.
- **NEW** `packages/analyzers/provider-neutrality/tests/Sunfish.Analyzers.ProviderNeutrality.Tests.csproj` — Roslyn analyzer tests using `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` (same harness pattern as existing analyzer tests if any; otherwise this is the precedent).
- **NEW** `packages/analyzers/provider-neutrality/tests/ProviderNeutralityAnalyzerTests.cs` — three tests minimum:
  1. **Positive (rule triggers):** simulated `Sunfish.Blocks.Accounting` project with `using Stripe;` → diagnostic emitted at correct line/column.
  2. **Negative (rule does NOT trigger):** simulated `Sunfish.Providers.Stripe` project with `using Stripe;` → no diagnostic.
  3. **Negative-exclusion (foundation-integrations):** simulated `Sunfish.Foundation.Integrations` project with `using Stripe;` → no diagnostic (because integrations is the seam and is excluded).

**PASS gate:** `dotnet test packages/analyzers/provider-neutrality/tests/...csproj` reports all 3+ tests passing.

### Phase 3 — `BannedSymbols.txt` layer

**Files:**

- **NEW** `BannedSymbols.txt` at solution root (or `_shared/BannedSymbols.txt` if the solution-root location conflicts with anything). Empty for v0 — the file's mere presence + the package reference in Phase 3 is enough to ship the layer; specific symbols will be added in follow-up commits as landmines surface.
- **MODIFY** `Directory.Build.props` — add a `<PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all">` to all packageable projects (use a property condition that excludes test projects + the analyzer project itself). Add an `<AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt">` so the analyzer picks up the file.
- **MODIFY** `Directory.Packages.props` — add a `<PackageVersion Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="X.Y.Z" />` per the central package management pattern. Latest stable as of 2026-04-28 is fine; if there's a preview that aligns with .NET 11 better, use it per the latest-first policy.
- **NEW** test: add one banned symbol to the file (e.g., `T:System.Web.HttpUtility;Use System.Net.WebUtility instead per ADR 0013`); confirm a sample reference fails the build with diagnostic `RS0030`. Then revert the test entry — leave the file populated only with intentional bans.

**PASS gate:** `dotnet build` of any package fails with `RS0030` when a banned symbol from `BannedSymbols.txt` is referenced in source.

### Phase 4 — ADR 0013 amendment + commit + PR

**Files:**

- **MODIFY** `docs/adrs/0013-foundation-integrations.md` — add a new top-level **"Enforcement"** section (insert before the existing References / Follow-ups section):

  ```markdown
  ## Enforcement (added 2026-04-28)

  Provider-neutrality is enforced at build time via two layered mechanisms:

  1. **Roslyn analyzer** — `Sunfish.Analyzers.ProviderNeutrality` rejects vendor SDK
     namespace references (`Stripe.*`, `Plaid.*`, `SendGrid.*`, `Twilio.*`, etc.) in
     any project under `packages/blocks-*/` or `packages/foundation-*/`, with the
     `Sunfish.Foundation.Integrations` package explicitly excluded as the contract
     seam. Diagnostic ID: `SUNFISH_PROVNEUT_001`. Auto-attached via
     `Directory.Build.props` (mirrors the `loc-comments` / `loc-unused` analyzer
     auto-wire pattern).

  2. **`BannedSymbols.txt`** at solution root — the
     `Microsoft.CodeAnalysis.BannedApiAnalyzers` rule (`RS0030`) rejects specific
     symbols that should be banned globally (e.g., legacy APIs deprecated by ADR
     amendments). Cheap to extend; one line per banned symbol.

  Both layers fail the build (`TreatWarningsAsErrors=true` repo-wide makes analyzer
  warnings into errors). Social enforcement ("reviewers reject violations") remains
  the fallback for cases the mechanical layers don't cover.
  ```

- **MODIFY** `docs/adrs/0013-foundation-integrations.md` — replace the existing language that says "the policy is reviewers reject violations in PRs" with a cross-reference to the new Enforcement section.

**PASS gate:** ADR 0013 has the new Enforcement section + cross-reference; PR opened with auto-merge enabled per `feedback_pr_push_authorization`; CI greens; merge.

---

## Out of scope (explicit do-NOT-touch list)

- **Don't expand the banned-namespace list** beyond the Phase 2 commercial vendors (`Stripe.*`, `Plaid.*`, `SendGrid.*`, `Twilio.*`) — extension comes when new vendors land per ADR 0013 follow-up #4.
- **Don't auto-attach BannedApiAnalyzers to test projects** — tests may use mocks of vendor SDKs in fixtures.
- **Don't write the secrets-management ADR** (Follow-up #2 from ADR 0013) — that's a separate intake; this hand-off is enforcement-gate only.

---

## Acceptance criteria (PR-level)

- [ ] `dotnet build` of `packages/analyzers/provider-neutrality/Sunfish.Analyzers.ProviderNeutrality.csproj` exits 0, zero warnings
- [ ] `dotnet test packages/analyzers/provider-neutrality/tests/...csproj` — all tests pass (positive + negative + foundation-integrations exclusion)
- [ ] Sample `using Stripe;` added temporarily to `packages/blocks-tenant-admin/` (or any blocks-* package) fails the repo-wide build with `SUNFISH_PROVNEUT_001` diagnostic; revert the test reference
- [ ] Sample `using Stripe;` in a hypothetical `packages/providers-stripe/` test fixture (can stub a project) does NOT trigger the diagnostic
- [ ] `Microsoft.CodeAnalysis.BannedApiAnalyzers` referenced via central package management; `BannedSymbols.txt` at solution root
- [ ] ADR 0013 has new "Enforcement" section + cross-reference; old social-only language replaced
- [ ] PR title: `feat(analyzers): provider-neutrality enforcement gate (ADR 0013)`
- [ ] Auto-merge enabled per `feedback_pr_push_authorization`
- [ ] On completion, update `icm/_state/active-workstreams.md` row for this workstream → status `built`

---

## Branch + PR strategy

Per `feedback_use_worktree_when_gitbutler_blocks` — if the GitButler workspace has accumulated virtual branches, use `git worktree add /tmp/sunfish-provneut-wt origin/main -b feat/analyzers-provider-neutrality-adr-0013` and work there. Plain git or `but` both fine if workspace is clean.

PR title: `feat(analyzers): provider-neutrality enforcement gate (ADR 0013)`
PR body should reference: this hand-off file, the UPF Quality A grade, and the ADR 0013 audit finding C-1.

---

## Kill triggers (halt + report)

- **Roslyn analyzer can't pattern-match project paths via MSBuild metadata** — fall back to per-project `[ProviderNeutralProject]` attribute or per-csproj property; revise Phase 2 spec.
- **`Microsoft.CodeAnalysis.BannedApiAnalyzers` doesn't load on .NET 11 preview SDK** — drop Phase 3 to inline the banned-symbol rule inside the custom analyzer; ship Phase 3 reduced.
- **Wall-clock burn exceeds 6 hours (2× the 3-hour estimate)** — halt; write a memory note describing what's stuck; ask research session for revisions.
- **False positive on legitimate `providers-stripe` use** — Phase 2 negative test should catch this; if it slips and lands, immediate revert + revise the project predicate.

---

## On completion

1. Update `icm/_state/active-workstreams.md` — flip the row for this workstream to `built` with the merged PR link.
2. Optionally write a project memory note (`project_provider_neutrality_gate_built.md`) so the research session sees the state change at next session-start.
3. ADR 0013's audit finding C-1 in `icm/07_review/output/adr-audits/CONSOLIDATED-HUMAN-REVIEW.md` § 1 is **resolved** — note this in any follow-up audit consolidation.
