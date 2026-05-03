# Sunfish Weblate plugins

**Status:** Scaffolds only. Wave-2-Plan3-Cluster-C.
**Owner:** Translator-assist workstream (Plan 3).
**Companion:** `infra/weblate/README.md` (stack ops), `waves/global-ux/week-2-weblate-ops-runbook.md` (procedures).

This directory holds the Python source for Sunfish's custom Weblate plugins.
Two stubs are present today; both must be wired against a running Weblate
5.17.x instance before they have any effect.

## Plugin model

Weblate ships two extension surfaces we use:

1. **Checks** (`weblate.checks.Check`) — block approval of a segment when a
   condition is violated. Used for hard gates (placeholder loss, glossary
   "do-not-translate" violation).
2. **Addons** (`weblate.addons`) — augment the editor / review UI without
   blocking. Used for suggestions (glossary autocomplete, MT pre-fill, MT
   post-edit confidence display).

Each plugin lives in its own file in this directory; loading is configured
in `infra/weblate/docker-compose.yml` via `WEBLATE_ADDITIONAL_APPS` and
mounted into the Weblate container at `/app/data/python/`.

## Stub status

| File | Surface | Status | Wires to |
|---|---|---|---|
| `placeholder-validator.py` | Check | **STUB** | Plan 3 §Task 1.4 (C# `PlaceholderValidator`) |
| `glossary-integration.py` | Addon | **STUB** | Plan 3 §3B (autocomplete from `localization/glossary/sunfish-glossary.tbx`) |

A third plugin — `sunfish_glossary_enforcement` (Check, *blocking*) — is
called for in Plan 3 Success Criteria but is not scaffolded here; it will
arrive in a later cluster of the Phase 1 Finalization Loop.

## How to extend a stub into a wired plugin

1. **Spin up a local Weblate** per `infra/weblate/README.md` and confirm
   the admin UI is reachable.
2. **Subclass the appropriate base** (`weblate.checks.Check` for gates,
   `weblate.addons.base.BaseAddon` for surfaces).
3. **Mount this directory** into the container; restart Weblate; confirm the
   plugin appears in *Manage → Add-ons* (addons) or fires on a fixture
   segment (checks).
4. **Add Python tests** under a new `infra/weblate/plugins/tests/` tree
   using `pytest`. The test container should pin to the same Weblate
   minor version (`weblate==5.17.*`).
5. **Update `docker-compose.yml`** to register the plugin's app label
   in `WEBLATE_ADDITIONAL_APPS`.
6. **Update this README's status table** when each stub leaves STUB.

## Why these are stubs today

The Phase 1 Finalization Loop (Plan v1.0) explicitly defers full plugin
wiring because the wiring requires a running Weblate instance, which is
out of scope for the current parallel-cluster sweep. The stubs exist so
that:

- Plan 3's file-list status (NOT-STARTED → SCAFFOLDED) is accurate.
- A future agent (or human contributor) has the docstrings and the
  shape of the eventual API to fill in, rather than a blank file.
- The directory exists in git history before any Weblate VM is provisioned,
  so the runbook's "mount this dir into the container" step is non-empty.
