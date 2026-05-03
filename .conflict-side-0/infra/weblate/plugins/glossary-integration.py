"""Sunfish Weblate plugin — Glossary autocomplete integration.

STATUS: Stub. Wave-2-Plan3-Cluster-C scaffold.

Planned behavior
----------------
Integrates the Sunfish glossary (``localization/glossary/sunfish-glossary.tbx``)
into Weblate's autocomplete suggestions during translator review. When a
translator types a source-language token that matches a glossary term, the
plugin surfaces the approved target-language rendering inline, alongside any
"do-not-translate" (DNT) flags carried in the TBX entry (e.g., the literal
"Sunfish", "Anchor", "Bridge" must not be translated; "Block" in its
domain sense must preserve capitalization).

Reference: Plan 3 spec §3B and §Task 1.7 (`docs/superpowers/plans/
2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md`).
The companion plugin ``sunfish_glossary_enforcement`` (separate stub —
not in this cluster) handles *blocking* enforcement at approval time;
this plugin handles *suggestion* surface during draft entry.

Implementation deferred — the TBX → Weblate glossary import path and the
autocomplete API surface both need exercising against a running Weblate
instance. Per the Phase 1 Finalization Loop (Plan v1.0), this stub closes
the file-existence gap so Plan 3's task-list reflects scaffolded-but-unwired
status accurately.
"""

from __future__ import annotations


class GlossaryIntegration:
    """Stub for the Sunfish glossary autocomplete Weblate plugin.

    The wired implementation will hook the Weblate addon framework
    (``weblate.addons``) rather than the check framework, since this is
    a suggestion surface rather than a gate. ``setup`` will load the TBX
    once at addon-install time; ``lookup`` will be invoked per-keystroke
    from the translator UI.
    """

    addon_id = "sunfish_glossary_autocomplete"
    name = "Sunfish glossary autocomplete"

    def lookup(self, term: str) -> dict:
        """Return a dict ``{target, dnt, notes}`` for a source-language term.

        TODO: Implementation deferred to user-driven task per Phase 1
        Finalization Loop Plan v1.0; needs Weblate plugin SDK testing
        against a running Weblate 5.17.x instance and the seeded TBX
        fixture from Plan 2 Task 3.3.
        """
        pass
