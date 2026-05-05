---
type: idle
workstream-or-chapter: W#49 P2 — PR #614 NEEDS-AMENDMENT; 4 BLOCKING remain (XO follow-up)
last-pr: "#614 (W#49 P2 DefaultOodWatchService + ExpiryService; COB amendments applied)"
---

W#49 P2 PR #614: COB applied own council amendments (commit c7a35f8) but 4 XO BLOCKING
items remain open. Auto-merge DISABLED. Full disposition at PR #614 comment #4383601224.
R1: remove TOCTOU pre-check in StartWatchAsync. R2: ILogger on audit swallow.
R3: OodHandoverKind enum + discriminator in HandoverWatchAsync + audit payload.
R4: IOodWatchSweepRepository separation (GetExpiredCandidatesAsync off public interface).

All pending ADRs still Proposed (0065/0068/0082/0083/0084/0085; ADR 0066-A1 also Proposed).
ADR 0066 main: Accepted (was already; no change).

XO idle — blocked on CO acceptance flips. No unblocked ADR authoring work available.
Priority order when unblocked: (1) ADR 0085 Accepted → W#1 WS-B hand-off; (2) ADR 0082/0083
Accepted → W#54/W#55 build gates clear; (3) ADR 0068 Accepted → W#37 P1 build gate clear.
