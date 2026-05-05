---
sort_order: 16
number: 17
slug: properties-domain
title: "Properties domain (cluster #1 spine)"
status: "built"
status_cell: "`built` (first-slice merged)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/210 (merged 2026-04-28 21:05Z)"
---

## Notes

First-slice shipped: `Sunfish.Blocks.Properties` package — `Property` entity (`IMustHaveTenant`) + `PropertyId` + `PropertyKind` + `PostalAddress` value object + `IPropertyRepository` + `InMemoryPropertyRepository` + `PropertiesEntityModule` (ADR 0015) + `PropertyEntityConfiguration` (OwnsOne PostalAddress) + `AddInMemoryProperties()` DI + 25 tests + `apps/docs/blocks/properties/overview.md`. **Unblocks workstream #24 (Assets first-slice)** — but see project memory `project_workstream_24_assets_handoff_collision` for hand-off ambiguity that halts #24 pending research-session decision on package-name collision (`packages/blocks-assets/` already exists as UI-only catalog). PropertyUnit + ownership log queued as separate follow-up hand-offs. Hand-off OQ #2 (Money type) + OQ #3 (kitchen-sink seed pattern) flagged as deferred — see PR #210 description for resolution detail.
