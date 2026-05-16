# Sunfish.Blocks.FinancialPeriods

`FiscalYear` + `FiscalPeriod` entities + period-close machinery for the
Sunfish Anchor native financial domain.

PR 1 ships entities only. Service surface (`IPeriodCloseService`,
`SqlitePeriodResolver`) lands in PR 2 + PR 3. ERPNext importer hooks
land in PR 4.

See `apps/docs/blocks/financial-periods/overview.md` for the cluster
overview + per-PR scope notes.
