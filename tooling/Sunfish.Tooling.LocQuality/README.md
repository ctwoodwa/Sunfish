# Sunfish.Tooling.LocQuality

Translator-quality CLI for the Sunfish localization pipeline. Plan 3 Task 2.1
scaffold — stub subcommands today; implementations land in Plan 3 Tasks 2.4
and 3.x.

## Purpose

`sunfish-locquality` is the CLI surface that the Plan 3 (translator-assist)
quality stack plugs into. Its responsibilities, once implemented, are:

1. Validate translator-context completeness on XLIFF 2.0 inputs (notes,
   glossary references, screenshot links, ICU placeholder coverage).
2. Run post-edit quality heuristics (length-ratio anomaly, placeholder count
   mismatch, glossary miss, high edit-distance from MADLAD draft).
3. Drive MADLAD-400-MT pre-publish draft generation against staged XLIFF
   units, emitting `state="needs-review"` entries that translators then
   refine.

Sister tool to [`Sunfish.Tooling.LocExtraction`](../Sunfish.Tooling.LocExtraction)
(Plan 3 Task 1.1), which handles the upstream RESX → XLIFF extraction step.

## Subcommands

| Subcommand | Status | Tracked at |
|---|---|---|
| `--help` (or no args) | Implemented (System.CommandLine default) | — |
| `--version` | Implemented (prints `Sunfish.Tooling.LocQuality v0.1.0-scaffold`) | — |
| `validate <path>` | **Scaffold stub** — exits 0 with placeholder message | Plan 3 Task 2.x |
| `quality-check <path>` | **Scaffold stub** — exits 0 with placeholder message | Plan 3 Task 3.x (will integrate MADLAD draft generation) |

Exit-code contract (sysexits.h-aligned, mirrors `Sunfish.Tooling.LocExtraction`):

- `0` — success
- `1` — validation / quality-check failure
- `64` — usage error (unknown subcommand, bad args)
- `70` — not implemented yet (scaffold placeholder)

Stub subcommands intentionally exit `0` (not `70`) so the scaffold does not
accidentally fail downstream CI smoke checks during the staging period
between scaffold and implementation. The contract flips to `70` once the
real implementations begin landing.

## Reference

Specification:
[`docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md`](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md)
— Week 3 Task 2.1 ("Scaffold `Sunfish.Tooling.LocQuality` CLI") and Task 2.4
("MADLAD draft generator").

## Deferred — MADLAD MT integration

MADLAD-400-MT runtime wiring (the llama.cpp server-mode setup, the OpenAI-
compatible REST bridge, the GGUF model fetch + cache, and the actual draft-
generation calls) is **deferred to a separate user-driven task**. It requires
GPU / Apple-Silicon validation against the 15s/100-segment latency budget on a
real reference workstation, which subagents cannot perform.

The CLI surface in this scaffold is the integration point: when MADLAD wiring
lands, it plugs into `quality-check` (or a new subcommand spawned alongside
it) without reshaping the command tree.

## Build

```bash
dotnet build tooling/Sunfish.Tooling.LocQuality/Sunfish.Tooling.LocQuality.csproj
```

## Run

```bash
dotnet run --project tooling/Sunfish.Tooling.LocQuality -- --help
dotnet run --project tooling/Sunfish.Tooling.LocQuality -- --version
dotnet run --project tooling/Sunfish.Tooling.LocQuality -- validate <path>
dotnet run --project tooling/Sunfish.Tooling.LocQuality -- quality-check <path>
```
