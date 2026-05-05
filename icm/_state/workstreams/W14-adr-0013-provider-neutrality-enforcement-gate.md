---
sort_order: 13
number: 14
slug: adr-0013-provider-neutrality-enforcement-gate
title: "ADR 0013 provider-neutrality enforcement gate (Roslyn analyzer + BannedSymbols)"
status: "built"
status_cell: "`built` (merged)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/196 (merged 2026-04-28 14:35Z)"
---

## Notes

Resolves audit finding C-1. Phase-2 `providers-*` scaffolds can now proceed with mechanical vendor-isolation gate active. SUNFISH_PROVNEUT_001 + RS0030 (BannedApiAnalyzers) auto-attached.
