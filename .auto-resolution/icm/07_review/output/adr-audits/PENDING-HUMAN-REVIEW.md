# ADR Audit — Pending Human Review

**Generated:** 2026-04-28 (overnight automation run)
**Status:** in-flight (subagents working)
**Owning workstream:** workstreams #10 + #11 in `icm/_state/active-workstreams.md`

---

## How this document works

The research session dispatched 13 audit subagents in background. They write their findings to:

- **Tier 2 (anti-pattern sweep):** `icm/07_review/output/adr-audits/anti-pattern-sweep-batch-{1..5}.md` — 5 batch files, 3 ADRs each (15 ADRs total)
- **Tier 1 (full UPF audit):** `icm/07_review/output/adr-audits/<NNNN>-upf-audit.md` — one per ADR (ADRs 0004, 0008, 0013, 0021, 0028, 0043, 0044, 0046)

When all subagents return, this document gets consolidated by the research session into a master "decisions you need to make" agenda. Until then, this file is a stub. **The individual audit files are the ground truth as findings arrive.**

---

## Subagent dispatch manifest

### Tier 2 — anti-pattern sweep (15 ADRs across 5 batches; sonnet model)

| Batch | ADRs | Output path | Status |
|---|---|---|---|
| 1 | 0001 (Schema Registry), 0002 (Kernel Module Format), 0003 (Event-Bus Distribution) | `anti-pattern-sweep-batch-1.md` | dispatched |
| 2 | 0007 (Bundle Manifest), 0009 (FeatureManagement), 0011 (Bundle Versioning) | `anti-pattern-sweep-batch-2.md` | dispatched |
| 3 | 0012 (Foundation.LocalFirst), 0014 (Adapter Parity), 0017 (Web Components/Lit) | `anti-pattern-sweep-batch-3.md` | dispatched |
| 4 | 0027 (Kernel Runtime Split), 0029 (Federation/Reconciliation), 0031 (Bridge Hybrid SaaS) | `anti-pattern-sweep-batch-4.md` | dispatched |
| 5 | 0032 (Multi-Team Anchor), 0042 (Subagent-Driven Dev), 0048 (Anchor Multi-Backend MAUI) | `anti-pattern-sweep-batch-5.md` | dispatched |

### Tier 1 — full UPF audit (8 ADRs; opus model)

| # | ADR | Output path | Status |
|---|---|---|---|
| 1 | 0004 — Post-Quantum Signature Migration | `0004-upf-audit.md` | dispatched |
| 2 | 0008 — Foundation.MultiTenancy | `0008-upf-audit.md` | dispatched |
| 3 | 0013 — Foundation.Integrations / Provider-Neutrality | `0013-upf-audit.md` | dispatched |
| 4 | 0021 — Document/Report Generation Pipeline | `0021-upf-audit.md` | dispatched |
| 5 | 0028 — CRDT Engine Selection | `0028-upf-audit.md` | dispatched |
| 6 | 0043 — Unified Threat Model | `0043-upf-audit.md` | dispatched |
| 7 | 0044 — Anchor Windows-only Phase 1 | `0044-upf-audit.md` | dispatched |
| 8 | 0046 — Key-Loss Recovery Phase 1 | `0046-upf-audit.md` | dispatched |

---

## When you (the user) come back

Look for a sibling file `CONSOLIDATED-HUMAN-REVIEW.md` in this directory. If it exists, the research session has aggregated findings; that's your reading list.

If `CONSOLIDATED-HUMAN-REVIEW.md` does not yet exist:
- Some subagents may still be running. Check `gh pr list` and `git status` for in-flight work.
- Read the individual audit files directly; they're authoritative.
- Re-engage the research session and ask it to consolidate.

---

## Out-of-scope ADRs (Tier 3)

These were skipped per the triage. They're well-scoped, single-package, no recent activity, no current downstream-drift signal. Review on demand if a consumer hits an issue or an amendment is proposed.

ADRs 0005, 0006, 0010, 0015, 0016, 0018, 0022, 0023, 0024, 0025, 0026 (superseded), 0030, 0033, 0034, 0035, 0036, 0037, 0038, 0039, 0040, 0041, 0045, 0047 (and 0049 which was effectively audited as part of the kernel-audit v0 conversation earlier today).

---

## Phase 1 (template addition)

Already merged or merging via `chore/adr-template-self-audit` PR. Adds:
- `docs/adrs/_template.md` — the standard template + 5-min pre-acceptance self-audit
- `docs/adrs/README.md` update — points contributors at the template

This is the **forward-looking** half of the audit work (preventing future ADRs from skipping the audit). Tiers 1 and 2 are the **backward-looking** half (auditing existing ADRs).
