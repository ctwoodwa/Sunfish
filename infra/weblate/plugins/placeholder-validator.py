"""Sunfish Weblate plugin — Placeholder preservation validator.

STATUS: Stub. Wave-2-Plan3-Cluster-C scaffold.

Planned behavior
----------------
Validates that translator-edited strings preserve placeholder syntax
(e.g., ``{0}``, ``{name:format}``, ICU MessageFormat plural/select branches).
Compares source-locale RESX entries against translator drafts inside Weblate's
review surface; flags mismatches as `Check` failures so a segment cannot be
marked ``state="final"`` until placeholders are restored.

Reference: Plan 3 spec §Task 1.4 (`docs/superpowers/plans/
2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md`),
Python port of the C# `IcuPlaceholderRegex` class. The Python validator must
remain in lock-step with the C# extractor — when one changes, the other must
update or CI will diverge from the editor surface.

Implementation deferred — needs a running Weblate instance to test the
``weblate.checks.Check`` extension surface end-to-end. Per the Phase 1
Finalization Loop (Plan v1.0), this stub closes the file-existence gap so
Plan 3's task-list reflects scaffolded-but-unwired status accurately.
"""

from __future__ import annotations


class PlaceholderValidator:
    """Stub for the Sunfish placeholder-preservation Weblate check.

    The wired implementation will subclass ``weblate.checks.Check`` and
    populate ``check_id``, ``name``, ``description``, and ``check_single``.
    For now this class only documents the shape of the eventual API.
    """

    check_id = "sunfish_placeholder_preservation"
    name = "Sunfish placeholder preservation"

    def validate(self, source: str, translation: str) -> list:
        """Return a list of placeholder-mismatch findings.

        TODO: Implementation deferred to user-driven task per Phase 1
        Finalization Loop Plan v1.0; needs Weblate plugin SDK testing
        against a running Weblate 5.17.x instance and the SmartFormat /
        ICU MessageFormat fixture set from Plan 3 Task 1.5.
        """
        pass
