# `Sunfish.Blocks.FinancialTax`

Tax-domain block for Sunfish: jurisdiction hierarchies, tax codes,
effective-dated rate history, and a calculation engine that handles
OnSubtotal / Compound / Inclusive application per-line per-jurisdiction.

This package implements **Stage 02 §3.12–§3.14 + §6.4** of the
`blocks-financial-*` cluster design + **ADR 0088 §1** (Anchor all-in-
one local-first runtime — no external tax-engine dependency; the
calculation runs in-process). License posture, attribution discipline,
and CRDT conventions follow `ADR 0088` + `_shared/engineering/
crdt-friendly-schema-conventions.md`.

## Hand-off

Workstream hand-off:
`icm/_state/handoffs/blocks-financial-tax-stage06-handoff.md`.

5-PR sequence:

| PR | Scope |
|---|---|
| 1 | Package scaffold + `TaxJurisdiction` + `TaxCode` entities + jurisdiction-tree resolver stubs |
| 2 | `TaxRate` effective-dated history + `ITaxRateLookup` (non-overlap validation + atomic `SupersedeAsync`) |
| 3 | `ITaxCalculationService` — OnSubtotal / Compound / Inclusive algorithm per Stage 02 §6.4 (security-engineering council review before flip-to-open) |
| 4 | `TaxFormLineMap` entity + Schedule E 2026 v1 seed (provisional pending ONR research) |
| 5 | Ledger wiring (`IJournalTaxLineGenerator`) + ERPNext importer Pass 2 + event emission |

## Out-of-scope (forever)

- **Property tax** is not a `TaxCode`. It's a recurring vendor bill via
  `blocks-financial-ap`, mapped to Schedule E Line 16 through the
  chart-of-accounts code 6100. Do not add `TaxKind.PropertyTax` to the
  enum.
- The calculation engine is **not** a tax-policy oracle. It executes
  the rates the user (or ERPNext importer) declares; it does not look
  up ZIP-code-correct rates from a third-party feed.

## Attribution

See `NOTICE.md` — Apache OFBiz `accounting/TaxAuthority` entity-shape
pattern reproduced with attribution (Apache 2.0 §4(c)). Schedule E
line definitions derive from IRS Publication 527 + Schedule E (Form
1040) instructions (public-domain US federal works).
