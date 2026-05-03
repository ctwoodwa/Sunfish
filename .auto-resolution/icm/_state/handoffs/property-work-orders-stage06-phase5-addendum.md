# W#19 Work Orders Phase 5 — UX Strategy Addendum

**Addendum date:** 2026-04-29
**Resolves:** COB beacon `cob-question-2026-04-29T21-23Z-w19-p5-block-ux.md` (PR #289)
**Original hand-off:** [`property-work-orders-stage06-handoff.md`](./property-work-orders-stage06-handoff.md)
**Prior addendum:** [`property-work-orders-stage06-addendum.md`](./property-work-orders-stage06-addendum.md) (Phase 3 prereqs)
**Workstream:** #19

---

## What this addendum does

COB has shipped W#19 Phases 1, 2, 0+3, 4 (PRs #267, #269, #281, #284). Phase 5 (api-change-shape WorkOrder schema migration) is ready, but `WorkOrderListBlock.razor` currently uses `wo.RequestId` + `MaintenanceService.GetRequestAsync(wo.RequestId)` to render the originating maintenance request alongside the work order. The Phase 5 schema migration drops `RequestId` per ADR 0053 §"Decision" (polymorphic source via first audit event). Dropping it without a UX-side answer breaks the block's source-display feature.

COB surfaced three options. **XO picks Option A + new Phase 5.1.**

---

## Resolution — Option A + dedicated Phase 5.1 follow-up

### Phase 5 (unchanged scope per the addendum's reordering)

Ship the api-change-shape WorkOrder schema migration **without restoring source-fetching in `WorkOrderListBlock.razor`**. Concretely:

- `WorkOrder.RequestId` field dropped per the original spec
- `WorkOrderListBlock.razor` updated:
  - Remove `MaintenanceService.GetRequestAsync(wo.RequestId)` call
  - Remove the source-display section from the rendered block
  - Add a placeholder `<small class="text-muted">Source: <em>see audit trail</em></small>` (or equivalent) so operators see a clear "this feature is queued for Phase 5.1, not gone permanently"
  - Add an inline TODO comment in the .razor file: `<!-- TODO Phase 5.1: synthesize source from IAuditTrail.QueryAsync first WorkOrderCreated record -->`

This keeps Phase 5 scope tight (api-change-shape only) and avoids tripling the PR size with audit-query refactor.

### Phase 5.1 (NEW; runs after Phase 5 + Phase 6)

**Phase 5.1 — `WorkOrderListBlock.razor` source-display via audit-query** (~2–3h)

Once Phase 5 schema migration ships + Phase 6 cross-package wiring lands (audit emission is wired by Phase 4 already; Phase 6 just adds payment/messaging/signature consumers), restore source-display feature using the audit substrate per ADR 0053 §"Decision":

```razor
@* WorkOrderListBlock.razor — Phase 5.1 implementation *@
@code {
    private async Task<MaintenanceRequest?> ResolveSourceAsync(WorkOrder wo)
    {
        // Find the first WorkOrderCreated audit record for this WO; the payload's
        // body carries the originating MaintenanceRequest reference (per audit
        // emission cardinality rule: 1 record per state transition; first one
        // is creation).
        var query = new AuditQuery
        {
            TenantId = wo.Tenant,
            EventTypeFilter = AuditEventType.WorkOrderCreated,
            CorrelationFilter = wo.Id.Value,
            MaxResults = 1,
        };
        var firstRecord = (await _auditTrail.QueryAsync(query, ct)).FirstOrDefault();
        if (firstRecord is null) return null;

        var sourceKind = firstRecord.Payload.Operation.Body.GetValueOrDefault("source_kind") as string;
        if (sourceKind != "MaintenanceRequest") return null;

        var sourceId = firstRecord.Payload.Operation.Body.GetValueOrDefault("source_id") as string;
        if (sourceId is null || !Guid.TryParse(sourceId, out var requestGuid)) return null;

        return await _maintenanceService.GetRequestAsync(new MaintenanceRequestId(requestGuid), ct);
    }
}
```

This requires:
- `IAuditTrail.QueryAsync(AuditQuery, CancellationToken)` to support `EventTypeFilter` + `CorrelationFilter` — verify in Stage 02 that kernel-audit's `IAuditTrail` API supports these (per the existing `AuditQuery` shape; if not, halt-condition + write `cob-question-*`)
- The Phase 4 `WorkOrderCreated` audit-payload body to include `source_kind` + `source_id` keys (per the original hand-off Phase 4 spec — confirm payload-factory includes these)
- A graceful fallback if no creation audit exists (older work orders pre-Phase-4 wiring) — render "Source unavailable"

**Gate:** integration test covers source-resolution from audit query for a fresh-from-Phase-5 WorkOrder; graceful fallback for missing audit records; existing 100% block tests still pass.

**Estimated:** 2–3h
**PR title:** `feat(blocks-maintenance): WorkOrderListBlock source-display via audit-query (W#19 Phase 5.1)`

### Updated phase summary

| Phase | Subject | Hours | Status |
|---|---|---|---|
| 1 | TransitionTable visibility | 0.5 | ✅ shipped (#267) |
| 2 | WorkOrderStatus enum extension | 2–3 | ✅ shipped (#269) |
| 0 + 3 (bundled) | Foundation stubs + child entities | 0.5 + 3–5 | ✅ shipped (#281) |
| 4 | Audit emission (17 AuditEventType) | 2–3 | 🟡 in flight (#284) |
| 5 | WorkOrder schema migration (Option A: source UI dropped + TODO) | 4–6 | pending |
| **5.1 (NEW)** | **WorkOrderListBlock source-display via audit-query** | **2–3** | **NEW per this addendum** |
| 6 | Cross-package wiring | 1–2 | pending |
| 7 | Tests + apps/docs | 1 | pending |
| 8 | Ledger flip | 0.5 | pending |
| **Total** | | **16.5–24h** | (was 14.5–21h; +2–3h for Phase 5.1) |

The +2–3h for Phase 5.1 keeps Phase 5 PR scope manageable + delivers the same long-term source-display story without forcing audit-query refactor into the api-change PR.

### Why Option A + 5.1 over Options B / C

**Option B (audit-query during Phase 5):** correct long-term shape, but bundles api-change schema migration with UI refactor. Phase 5 PR becomes substantially larger; reviewer burden compounds the api-change risk. Splitting via Phase 5.1 addresses the same end-state with cleaner reviewable units.

**Option C (defer dropping `RequestId`):** keeps `RequestId` field through Phase 5; introduces a follow-up cleanup phase that has to re-touch the schema (api-change again). Two api-change PRs vs one. Worse than Option A.

**Option A + 5.1:** schema migration ships clean, UI gets a clear "TODO Phase 5.1" placeholder + small explanatory text for operators, follow-up Phase 5.1 restores feature parity using the long-term audit-query approach. Operators see a regression for ~one COB session before 5.1 lands; acceptable given Phase 5 itself takes 4–6h.

---

## Halt conditions added by this addendum

- **`IAuditTrail.QueryAsync` doesn't support `EventTypeFilter` + `CorrelationFilter`** at Phase 5.1 → write `cob-question-*` beacon; XO may extend `AuditQuery` ahead of full ADR 0049 v1 work
- **Phase 4 `WorkOrderCreated` payload body doesn't include `source_kind` + `source_id` keys** (or they're named differently) → halt; verify Phase 4's `WorkOrderAuditPayloadFactory` shipped with the right keys; if not, addendum to Phase 4 + retest

---

## What this addendum does NOT change

- Phase 5's api-change-shape classification + MAJOR version bump on `Sunfish.Blocks.Maintenance` — both stand
- The 17 `AuditEventType` constants from Phase 4 — unchanged; the audit-query approach in Phase 5.1 reuses the existing `WorkOrderCreated` event type
- All other §"Acceptance criteria" in the original hand-off + Phase 3 addendum
- Phase 6 (cross-package wiring) scope — unchanged
- Total effort estimate (was 14.5–21h after Phase 3 addendum; now 16.5–24h with Phase 5.1)

---

## How sunfish-PM should pick this up

1. **Resume Phase 5** with the Option A approach (UI placeholder + TODO comment).
2. After Phase 5 PR opens, **archive the cob-question beacon** at `icm/_state/research-inbox/cob-question-2026-04-29T21-23Z-w19-p5-block-ux.md` to `_archive/` in the Phase 5 PR (XO is doing this in the addendum's PR; if already done, no-op).
3. After Phase 5 + Phase 6 land, **execute new Phase 5.1** per the spec above.
4. Continue Phase 7 → Phase 8 per original hand-off.

If any new question surfaces: write `cob-question-*` beacon — same protocol.

---

## References

- Original hand-off: [`property-work-orders-stage06-handoff.md`](./property-work-orders-stage06-handoff.md)
- Phase 3 prereq addendum: [`property-work-orders-stage06-addendum.md`](./property-work-orders-stage06-addendum.md)
- COB beacon (this addendum resolves): [`../research-inbox/_archive/cob-question-2026-04-29T21-23Z-w19-p5-block-ux.md`](../research-inbox/_archive/) (post-archive path)
- ADR 0053 §"Decision": `WorkOrder.RequestId` dropped + polymorphic source via first audit event
- W#31 addendum pattern (precedent for resolution shape): [`foundation-taxonomy-phase1-stage06-addendum.md`](./foundation-taxonomy-phase1-stage06-addendum.md)
