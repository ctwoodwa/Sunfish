# ADR 0044 — UPF Audit (post PR #189 amendment)

**Auditor:** Subagent (autonomous overnight run)
**Date:** 2026-04-28
**Framework:** Universal Planning Framework v1.2
**Subject:** ADR 0044 (Anchor ships Windows-only for Phase 1), amended 2026-04-28
**Sibling reviewed for context:** ADR 0048 (Anchor multi-backend MAUI)

---

## Headline

**Grade: B (Solid).** The amendment is well-scoped and honest, but it weakens the original kill-trigger logic in one place without re-deriving the FAILED conditions, leaving a small but real internal inconsistency between the Decision section and the Amendment.

## Most-important amendment in two sentences

The Amendment's "Why this doesn't fully supersede" paragraph carries the load: it reframes "Windows-only" as "Windows is the default CI/conformance target" while the Decision section still reads "Anchor ships Windows-only for Phase 1." That phrasing gap should be patched by inserting a one-line "Decision (as amended 2026-04-28)" pointer at the top of §Decision so a fresh reader of the Decision text alone doesn't get a stale answer.

## Stage 0 findings

Existing Work, Feasibility, and Better Alternatives are all visibly executed (Options A/B/C are real alternatives, not strawmen, and Tauri is named with a one-way-door justification). Official Docs and Factual Verification are implicit in the post-amendment csproj evidence (`maui-maccatalyst` manifest version `26.2.11588-net11-p3`, NETSDK1082, MSB4096) — strong, source-grounded. The "AHA Effect" check actually fired between original ADR and amendment: someone discovered three csproj enhancements that punched through the assumed-blocking Mono runtime gap, which is exactly the discovery-during-execution case UPF Stage 0 is designed for. Constraints check is weak on the Linux side — the amendment notes Linux is still blocked but doesn't confirm whether the same ASP.NET-Core-pack-strip trick would help there or is fundamentally different.

## Stage 1 — 5 CORE sections

1. **Context & Why** — Strong. Three sentences in the original Context plus an Amendment §"What changed" paragraph anchored to a specific MAUI workload version. Sources cited.
2. **Success Criteria + FAILED conditions** — Mixed. Original "Phase 1 deliverable demonstrable on a Win-only fleet" is measurable. The amendment adds new positive scope ("Macs become legitimate workstations") but does **not** re-derive FAILED conditions. There is no kill-trigger for "what if MacCatalyst build regresses on next MAUI workload bump?" — a real risk given preview-tier dependency.
3. **Assumptions & Validation** — Original assumption "MAUI 10 stabilization is the single gate" is now disproven by the amendment itself (three csproj workarounds + host prereqs were the actual gate). The ADR honestly admits this implicitly but doesn't restate the assumption-validation table. UPF expects "Assumption → VALIDATE BY → IMPACT IF WRONG"; neither original nor amendment uses that format.
4. **Phases** — N/A for an ADR (this is a decision record, not a build plan); UPF Phases criterion translates to "revisit triggers," which are present and binary-checkable in both original (4 triggers) and amendment (2 additional triggers). Good.
5. **Verification** — Original ADR has no Verification section. Amendment adds implicit verification ("a runnable `.app` bundle on macOS") but no automated test gate is named. Stage 06 conformance scan is referenced as remaining Win-only — that's the closest thing to ongoing observability.

## Stage 2 — Meta-validation + 21-AP scan

| Check | Status |
|-------|--------|
| Delegation strategy clarity | N/A — ADR, not plan |
| Research needs | Met (Tauri evaluation memo deliverable cited) |
| Review gate placement | Met (G7 conformance scan is the gate) |
| Anti-pattern scan | See below |
| Cold Start Test | **Partial fail** — fresh reader of §Decision alone gets "Windows-only" without seeing the amendment relaxation |
| Plan Hygiene | Met (Status line at top flags Amendment) |
| Discovery Consolidation | Met (csproj enhancements + prereqs doc cross-referenced) |

**Anti-patterns hit:**

- **AP-1 (Unvalidated assumptions):** Original "MAUI 10 stabilization is the gate" assumption was wrong — the gate was actually three csproj tricks. Annotate.
- **AP-3 (Vague success criteria):** Amendment's "Macs become legitimate workstations" lacks a measurable bar. Annotate.
- **AP-11 (Zombie project / no kill criteria):** Tauri-fallback evaluation memo is "deprioritized" but not killed; no explicit close-out criterion. Annotate.
- **AP-13 (Confidence without evidence):** None — the amendment is unusually well-sourced (specific manifest version, error codes, file paths).
- **AP-18 (Unverifiable gates):** "MAUI 10 GA released with stable Mono runtime packages" is verifiable. Pass.

## Cross-check with ADR 0048

ADR 0048 is internally consistent with the **original** ADR 0044 framing ("Phase 1: no change. ADR 0044's Win64 only Phase 1 remains in force"). It does not yet incorporate the 0044 amendment. Minor: ADR 0048's Context paragraph 2 still describes the Mono-runtime gap as the single blocker, which the 0044 amendment partially refutes for MacCatalyst. Not a contradiction (Mac was indeed gated; the workarounds are the resolution), but a forward reference from 0048 to "see ADR 0044 amendment 2026-04-28" would be honest.

## Recommended amendments

| # | Severity | Where | Recommendation |
|---|----------|-------|----------------|
| 1 | major | §Decision (line 50) | Insert "(as amended 2026-04-28 — see Amendment below)" pointer so the Cold Start reader doesn't anchor on stale "Windows-only" wording |
| 2 | major | Amendment §"What this DOES change" | Add explicit FAILED condition: "If MacCatalyst build breaks on next MAUI workload manifest bump and isn't restored within N days, fall back to Win-only-dev-only posture" |
| 3 | minor | §Decision drivers | Annotate original "MAUI 10 stabilization" assumption was disproven (in part) by the amendment's csproj workarounds; preserves discovery-amnesia hygiene |
| 4 | minor | Amendment §"What this DOES change" bullet 2 | Replace "Macs become legitimate workstations" with a measurable bar (e.g., "Mac developer can run `dotnet build` + launch `.app` from local repo without Windows VM") |
| 5 | minor | §Tauri fallback (Amendment) | Add explicit close-out: memo deliverable retained, but if Linux unblocks via ADR 0048 path, Tauri evaluation closes with "no action" — currently zombie-adjacent |
| 6 | minor | ADR 0048 Context para 2 | Add forward reference to 0044 Amendment 2026-04-28 noting MacCatalyst is no longer fully gated on Mono runtime publication |

---

ADR 0044 audit complete; grade B; 6 amendments at 2 major / 4 minor. Findings at /Users/christopherwood/Projects/Sunfish/icm/07_review/output/adr-audits/0044-upf-audit.md.
