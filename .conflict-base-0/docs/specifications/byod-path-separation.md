# BYOD Path Separation Specification

**Status:** Draft — Wave 4.5 deliverable of the Paper-Alignment Plan
**Date:** 2026-04-22
**Source paper:** [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §16.3
**Audience:** Platform engineers implementing data paths; MDM operators writing enterprise-wipe policies.

> Paper §16.3 requires that team data live under a named, MDM-targetable path, and that personal application data live under a separate, policy-excluded path. This document is the canonical mapping between those two abstract categories and the concrete per-OS paths.

---

## 1. The Rule

**Enterprise wipe MUST NOT destroy UI preferences or local-only drafts.**

BYOD fleets (bring-your-own-device, contractor laptops, dual-role engineers) are the motivating case: when a team decommissions access, the MDM-driven wipe should remove *team data* but leave the user's personal shell intact. The product reflects that on disk by keeping the two categories in distinct directory trees.

---

## 2. Path Mapping

### 2.1 Enterprise wipe target (MDM-targetable)

All of these paths are team-owned. An MDM policy MAY recursively delete them on offboarding without compromising personal state.

| Platform | Path                                                       | Paper §16.3 role |
|----------|------------------------------------------------------------|------------------|
| Windows  | `%LOCALAPPDATA%\Sunfish\TeamData\`                         | wipe target      |
| Linux    | `$XDG_DATA_HOME/sunfish/team-data/` (fallback `~/.local/share/sunfish/team-data/`) | wipe target      |
| macOS    | `~/Library/Application Support/Sunfish/TeamData/`          | wipe target      |

What lives here:

- Encrypted local store (SQLCipher database; path supplied by `EncryptionOptions.DatabasePath`, Wave 1.4).
- Event log (kernel-event-bus, Wave 1.3).
- Quarantine queue (foundation-localfirst, Wave 1.5).
- Role attestations, key material, projections.
- Cached team-scoped documents.

### 2.2 Personal, policy-excluded (MDM MUST NOT target)

| Platform | Path                                                         | Paper §16.3 role |
|----------|--------------------------------------------------------------|------------------|
| Windows  | `%LOCALAPPDATA%\Sunfish\UserPreferences\`                    | excluded         |
| Linux    | `$XDG_CONFIG_HOME/sunfish/` (fallback `~/.config/sunfish/`)  | excluded         |
| macOS    | `~/Library/Preferences/com.sunfish.anchor.plist`             | excluded         |
| macOS    | `~/Library/Application Support/Sunfish/UserPreferences/`     | excluded         |

What lives here:

- Anchor UI preferences (theme, window layout, last-opened document).
- Local-only drafts (documents the user has not committed to any team).
- Command palette history, recent files, recently-used templates.
- Per-user feature flags / opt-ins.

---

## 3. Implementation Notes

### 3.1 Current wiring (Wave 2.5) vs. target (Wave 4.5)

Today, `LocalNodeOptions.DataDirectory` defaults (see `apps/local-node-host/LocalNodeOptions.cs`) point at:

- Windows: `%LOCALAPPDATA%\Sunfish\LocalNode`
- Linux: `$XDG_DATA_HOME/sunfish/local-node`
- macOS: `~/Library/Application Support/Sunfish/LocalNode`

These paths **conflate team data with personal data**. Wave 4.5 must update those defaults to:

- Windows: `%LOCALAPPDATA%\Sunfish\TeamData`
- Linux: `$XDG_DATA_HOME/sunfish/team-data`
- macOS: `~/Library/Application Support/Sunfish/TeamData`

And introduce a separate `LocalNodeOptions.UserPreferencesDirectory` for the policy-excluded category.

**This code change is intentionally deferred** — it ripples through Wave 1.4 (`EncryptionOptions.DatabasePath`), Wave 1.3 (event-log defaults), and Wave 1.5 (quarantine). Batching it with those packages' next routine revisit minimizes churn. The installer + MDM-config deliverables ship assuming the *target* layout so that when the code catches up, operator-facing docs do not need to change.

### 3.2 Pre-seeded override

The MDM config's `dataDirectory` field (see [`mdm-config-schema.md`](mdm-config-schema.md)) overrides the team-data path only. It MUST NOT redirect the personal-preferences path — that would defeat the separation.

### 3.3 Install-time

The platform installers ([`installers/windows`](../../installers/windows/README.md), [`installers/linux`](../../installers/linux/README.md), [`installers/macos`](../../installers/macos/README.md)) create the team-data directory with appropriate permissions and set it to `DataDirectory`. They do NOT create the user-preferences directory — that is lazy-created by Anchor on first run in user scope.

### 3.4 Uninstall-time

Uninstall removes only program files from the installer's install-target (`/opt/sunfish/`, `%PROGRAMFILES%\Sunfish`, `/Applications/Sunfish Anchor.app`). It does NOT touch either category of user data. Enterprise wipe is a separate, explicit operation.

---

## 4. MDM Wipe Recipes

### Windows (Intune / Config Manager)

```powershell
# Enterprise wipe — removes team data only.
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Sunfish\TeamData"
# Personal data preserved at $env:LOCALAPPDATA\Sunfish\UserPreferences
```

### Linux (Ansible / Puppet)

```yaml
- name: Sunfish enterprise wipe
  file:
    path: "{{ lookup('env', 'XDG_DATA_HOME') | default(ansible_user_dir ~ '/.local/share', true) }}/sunfish/team-data"
    state: absent
```

### macOS (Jamf / Kandji)

```bash
# Run as the target user.
rm -rf "$HOME/Library/Application Support/Sunfish/TeamData"
```

---

## 5. Verification

Post-wipe the following MUST all hold:

1. Anchor still launches.
2. Window layout, theme, recent-files list are intact.
3. Local-only drafts are intact.
4. All team documents are gone.
5. Re-enrolling into the same team via a new MDM config triggers a full re-sync from the team's relay / peers (empty state → populated state).

---

## 6. Change Log

| Date       | Change                                                         |
|------------|----------------------------------------------------------------|
| 2026-04-22 | Initial draft (Wave 4.5). Team-data path update deferred pending coordinated bump in Waves 1.3 / 1.4 / 1.5. |
