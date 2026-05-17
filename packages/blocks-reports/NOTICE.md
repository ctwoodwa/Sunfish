# NOTICE — Sunfish.Blocks.Reports

## License

MIT. See repository root `LICENSE` for the full text.

## Origin

Clean-room original substrate. The `IReportCartridge<TParams, TResult>` + `ReportCartridgeRegistry` + `ReportRunner` shape is a routine generic-dispatch registry pattern (GoF Strategy + Type-keyed Registry); no third-party source was consulted in the design or implementation.

## Per-cartridge attribution (forward-looking)

PRs 2–6 will add cartridges whose formulas are derived from established public-domain accounting conventions. The cartridges themselves are clean-room originals; the conventions are uncopyrightable. Per-cartridge attribution will be appended to this NOTICE as each PR lands:

- **Trial Balance (PR 2):** standard accounting formula (sum of debits − sum of credits per account; balance reported by side per account-type). Public-domain convention.
- **AR / AP Aging (PRs 3, 4):** 0/30/60/90+ bucket boundaries from GnuCash convention. Uncopyrightable bucket math; cite GnuCash as inspiration only.
- **P&L by Property (PR 5):** ERPNext cost-center grouping pattern (uncopyrightable taxonomy).
- **Rent Roll v2 (PR 6):** ERPNext `rent_roll` + the Sunfish v1 Rent Roll shipped in `@sunfish/contracts`. Sunfish original.

No FOSS source code (BSL, AGPL, GPL, MPL, etc.) was studied or referenced for the substrate. Per-cartridge clean-room discipline (close any GnuCash / ERPNext / Beancount / Akaunting / Metabase source editor session before authoring) is reaffirmed on every cartridge PR per ADR 0088 §3.2.

## Third-party dependencies

This package depends only on first-party Sunfish packages plus the standard Microsoft.Extensions DI surface:

- `Sunfish.Foundation` (first-party)
- `Microsoft.Extensions.DependencyInjection.Abstractions` (Microsoft, MIT — transitive via the substrate's `IServiceCollection` extension)
