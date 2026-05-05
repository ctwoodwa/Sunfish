---
sort_order: 30
number: 31
slug: foundation-taxonomy-substrate-phase-1
title: "Foundation.Taxonomy substrate Phase 1"
status: "built"
status_cell: "`built`"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/258 (ActorId.Sunfish, merged 2026-04-29) + https://github.com/ctwoodwa/Sunfish/pull/263 (substrate, merged 2026-04-29)"
---

## Notes

**Shipped 2026-04-29.** PR #258 added `ActorId.Sunfish` sentinel (prerequisite). PR #263 shipped the substrate per addendum: `packages/foundation-taxonomy/` (11 model types + 2 service contracts + InMemoryRegistry/Resolver enforcing 5 governance rules + 9 `AuditEventType` constants + `TaxonomyAuditPayloadFactory` + `Sunfish.Signature.Scopes@1.0.0` seed of 17 root + 7 children + DI extension + 55 tests + apps/docs entry). Kitchen-sink demo deferred per Properties/Equipment/Inspections first-slice precedent (PRs #210, #213, #222). **Unblocks downstream:** ADR 0054 Stage 06 (kernel-signatures — `SignatureScope = IReadOnlyList<TaxonomyClassification>`), ADR 0055 Phase 1 (dynamic-forms `Coding`/`CodeableConcept` primitives), property cluster equipment migration (hardcoded enum → taxonomy ref), future cluster reframes (Receipts, Inspections deficiencies, Vendor specialties), ADR 0057 leasing-pipeline (Jurisdiction policy taxonomy reference).
