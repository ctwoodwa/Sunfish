---
type: question
workstream-or-chapter: W#23 Phase 4 (iOS sync engine)
last-pr: "#517 (W#23 P3.5 JsonCanonical merged)"
---

W#23 P4 hand-off (`property-ios-field-app-stage06-handoff.md`) halts on the
`POST /api/v1/field/event` + `POST /api/v1/field/blob/<sha256>` Bridge endpoints
not being implemented. `grep -rn "field/event"` under `accelerators/bridge/`
returns zero hits today. Per ADR 0028-A2.6 + the W#28 intake, W#28 Public
Listings owns the Bridge route family; the field-event endpoints are not in
W#28's shipped 5c-1 / 5c-2 / 5c-3 / 5c-4 surface.

What would unblock me: either (a) a small W#28 follow-up hand-off authoring
the two field endpoints + their auth + audit emission shape, or (b) a
W#23 Phase 4.5 inline scope expansion. W#23 P4 (URLSession sync engine) is
ready to ship the iOS side as soon as the Bridge target endpoints exist.

W#23 cohort progress in the meantime: P0 (#478) + P1 (#498) + P2 (#511) +
P3 (#516) + P3.5 (#517) all on main; P4 is now the only blocker for the
field app's first end-to-end smoke test (TestFlight at P7).
