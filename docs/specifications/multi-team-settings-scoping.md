# Multi-Team Settings Scoping

**Status:** Normative reference for [ADR 0032](../adrs/0032-multi-team-anchor-workspace-switching.md)
**Date:** 2026-04-23
**Scope:** Defines global-vs-per-team scope for every user-facing and
install-level setting Anchor v2 is expected to expose.

---

## Purpose

ADR 0032 introduces multi-team Anchor as the Slack-style workspace-switcher
model. One install hosts membership in N teams via `TeamContext` scoping.
A natural question follows: for each setting the user can change, is it
global to the install, or scoped to the active team?

This document answers that question authoritatively. It is the **tiebreaker
for future UI work** — when a disagreement arises about whether a setting
should override globally or per-team, the row below resolves it.

---

## Scoping table

| Setting category | Scope | Rationale |
|---|---|---|
| UI theme / provider selection (Bootstrap 5 / Fluent v9 / Material 3) | Global | Visual consistency across teams; swapping themes per team creates whiplash. |
| Notifications: schedule (do-not-disturb hours) | Global | User-level policy — "I don't want any work contact between 6pm and 8am" applies regardless of which team. |
| Notifications: per-team enablement | Per-team | User picks which teams interrupt. "Mute Team B while working on Team A" is the motivating case. |
| Notifications: per-team do-not-disturb override | Per-team | Exists as a narrow override on top of the global DND schedule (e.g., emergency on-call team pierces DND). |
| Keyboard shortcuts | Global | Muscle memory — one binding for "open command palette" across the whole app. |
| Plugin / block enablement | Per-team | Different teams use different bundles. Team A enables `blocks-rent-collection`; Team B enables `blocks-medical-office`. |
| Role attestations | Per-team | Cryptographic scope is per-team by construction (paper §11.3 — attestations are issued against a team's key space). |
| Sync-bucket subscriptions | Per-team | Defined by the team's bucket YAML (paper §10). A user belongs to different buckets in different teams. |
| Storage budget | Global with per-team budget enforcement | Install-level total cap (e.g., 10 GB on this device); per-team share enforced by the team's bucket config + LRU eviction. |
| Language / locale | Global | User's language is user-scoped — same person writing in the same language across teams. |
| Auto-update channel (stable / beta / nightly) | Global | Install-level — you can only install one binary at a time. |
| OS integration (autostart, tray icon, protocol handlers) | Global | Install-level — these touch the OS, not the team data plane. |
| Camera / biometric auth enrollment | Global | Device-level — the device has one camera and one set of platform-authenticator credentials. |
| CRDT document sharding preferences | Per-team | Team policy — a team with 50 GB of blueprints shards differently than a team with 500 KB of contracts. |
| Conflict auto-resolution rules | Per-team | Each team decides its own policy (e.g., "last-writer-wins on inspection fields" vs. "always-prompt on contract fields"). |
| Default report format (PDF / XLSX / DOCX) | Per-team | A legal-services team defaults to DOCX; a property-management team defaults to PDF. |
| Export destination (local filesystem path, cloud drive link) | Global | Device-level filesystem preference, same across teams. |
| Log verbosity / diagnostics level | Global | Install-level — the log subsystem is one process's logger. |
| Font size / accessibility preferences (high-contrast, reduced motion) | Global | User-level — accessibility is about the user, not the team. |
| Offline / sync-toggle per-bundle | Per-team | Bundles are per-team; sync opt-in is per-bundle therefore per-team. |

---

## Tiebreaker note

Where a setting could plausibly live at either scope, this document is the
canonical answer. Future UI work that proposes to flip a row's scope must
open an ADR amendment and get sign-off — implicit scope changes in code
reviews are out of scope for this doc's purpose.

Edge cases like "per-team notification override of global do-not-disturb"
are explicitly resolved above: the global schedule is authoritative; the
per-team override is a narrow pierce-through (on-call, emergency) and must
be opt-in per team, never the default.

---

## Open questions (deferred; not currently blocking)

1. **Cross-team clipboard / command palette scoping.** Global by
   construction (the palette is an install-level surface), but what does it
   show? Listed under ADR 0032's flagged follow-up ADRs ("cross-team
   features surface").
2. **Per-team branding (logo, accent color) in the switcher.** Per-team;
   out of scope for v2 MVP but the scoping is unambiguous.
3. **Per-team telemetry opt-in.** Global in v2 (install-level opt-in only).
   Future per-team override deferred.

---

## References

- [ADR 0032](../adrs/0032-multi-team-anchor-workspace-switching.md) — the ADR this doc is the normative scoping reference for.
- [Paper v12.0 §10](../../_shared/product/local-node-architecture-paper.md) — sync-bucket definitions are per-team.
- [Paper v12.0 §11.3](../../_shared/product/local-node-architecture-paper.md) — role attestations are per-team cryptographic scope.
- [Paper v12.0 §13.2](../../_shared/product/local-node-architecture-paper.md) — notification surface (NodeHealthBar etc.) drives the global-vs-per-team split for notifications.
