# NOTICE — Sunfish.Blocks.FinancialTax

This package's entity shapes (TaxCode → TaxRate → TaxJurisdiction
three-level decomposition; TaxFormLineMap account-selector pattern;
tax-rate effective-dating semantics) derive from Apache OFBiz's
`accounting/TaxAuthority`, `accounting/TaxAuthorityRateProduct`, and
`accounting/TaxAuthorityGlAccount` entity models
(<https://ofbiz.apache.org/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).

The Schedule E tax-form line definitions (PR 4 of the
blocks-financial-tax-stage06-handoff) are derived from IRS
Publication 527 (Residential Rental Property) and Schedule E
(Form 1040) instructions — public-domain US federal works.

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz entity-shape pattern is reproduced with
attribution per Apache 2.0 §4(c) of the OFBiz License. No OFBiz
source code is vendored into this repository.
