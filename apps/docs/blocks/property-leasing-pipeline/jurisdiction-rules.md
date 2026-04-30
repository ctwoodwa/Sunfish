# Jurisdiction Rules

`Sunfish.Leasing.JurisdictionRules@1.0.0` is an Authoritative-regime taxonomy (per [ADR 0056](../../../docs/adrs/0056-foundation-taxonomy-substrate.md)) enumerating the fair-housing + consumer-financial-protection rule families that the leasing pipeline + showing-compliance code paths observe. Charter authored at [`icm/00_intake/output/starter-taxonomies-v1-leasing-2026-04-30.md`](../../../icm/00_intake/output/starter-taxonomies-v1-leasing-2026-04-30.md).

## How it's consumed

Each node identifies *what* compliance applies, not *how* to apply it. Downstream consumers map nodes to executable policy:

- `IApplicationDecisioner` references FHA protected-class nodes (`us-fed.fha.race`, etc.) — though the structural quarantine in [FHA Defense](./fha-defense.md) is the actual enforcement.
- `FcraAdverseActionNoticeGenerator` materializes `us-fed.fcra.adverse-action-notice` + `us-fed.fcra.dispute-window-60d`.
- `IJurisdictionPolicyResolver` (per ADR 0060, gated; ship in W#22 Phase 6 compliance half once ADR 0060 lands) consumes the per-state operational rules.

## Loading the taxonomy

```csharp
var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
// pkg.Definition.Id   → Sunfish.Leasing.JurisdictionRules
// pkg.Definition.Version → 1.0.0
// pkg.Nodes          → 30 TaxonomyNode entries (7 roots + 23 children)
```

`pkg.Definition` carries Authoritative governance regime + the Sunfish ownership marker. Civilians may **clone** to derive their own variant or **extend** with locally-scoped child nodes (e.g., a city ordinance), but cannot **alter** the Sunfish-shipped node set.

## Node breakdown (30 total)

### `us-fed.fha` — US Fair Housing Act (root + 7 children)

The seven federally-protected classes: `race`, `color`, `religion`, `sex`, `familial-status`, `national-origin`, `disability`. HUD now interprets `sex` to include gender identity + sexual orientation since 2021.

### `us-fed.fcra` — US Fair Credit Reporting Act (root + 4 children)

- `adverse-action-notice` — §615(a) mandatory notice
- `dispute-window-60d` — §612(a) 60-day right to free report + dispute
- `consent-required` — §604(b) written-consent requirement
- `permissible-purpose` — §604(a) tenant-screening qualifies

### `us-fed.fha-source-of-income` — Source-of-Income Rules (root + 2 children)

- `section-8-voucher` — Section 8 acceptance mandate
- `housing-assistance` — disability/veterans' housing programs

### `us-state.ca.unruh` — California Unruh Civil Rights Act (root + 5 children)

CA Civil Code §51 extends FHA's seven with: `sexual-orientation`, `gender-identity`, `marital-status`, `ancestry`, `medical-condition`.

### `us-state.ca.fehc` — California FEHC Regulations (root + 2 children)

- `prohibited-question-list` — Cal. Code Regs. §12181
- `application-fee-cap` — CA Civil Code §1950.6

### `us-state.ny.tpa` — New York Tenant Protection Act (root + 3 children)

- `application-fee-cap-20usd` — RPL §238-a ($20 cap)
- `security-deposit-cap-1mo` — RPL §7-108
- `tenant-blacklist-prohibition` — RPL §227-f

### `us-state.ny.adverse-action-extended-window` — NY 90-Day FCRA Extension (root)

NY-state extension of the FCRA dispute window for source-of-income decisions.

## Out of v1.0 (deferred)

- Per-city local ordinances (Seattle FIT, Berkeley source-of-income, NYC Local Law 71)
- Non-US jurisdictions
- Sub-state county-level rules
- Section 8 administrative-plan specifics (vary per local PHA)

These land in v1.1 minor (additive) or as civilian-regime extensions.

## Versioning

- **MAJOR** (2.0.0) — any node `code` removed or renamed
- **MINOR** (1.1.0) — new nodes added (additive only)
- **PATCH** (1.0.1) — display revisions, parent-reorganization within same `code` set, tombstones with successor mappings

Tombstoning is the deprecation marker; removing a tombstoned node requires a major bump.

## See also

- [Charter](../../../icm/00_intake/output/starter-taxonomies-v1-leasing-2026-04-30.md)
- [ADR 0056](../../../docs/adrs/0056-foundation-taxonomy-substrate.md) — Foundation.Taxonomy substrate
- [FHA Defense](./fha-defense.md) — Structural enforcement of the FHA nodes
- [FCRA Workflow](./fcra-workflow.md) — Materializes the FCRA-family nodes
