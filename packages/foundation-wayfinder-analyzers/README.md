# Sunfish.Wayfinder.Analyzers

Roslyn analyzer for the Sunfish Wayfinder system (ADR 0065 / W#42 Phase 3b).

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| `SUNFISH_WAYFINDER001` | Warning | Project calls `IServiceCollection.AddSunfish*()` to wire the Wayfinder substrate but does not declare any `AtlasSchemaDescriptor`. The Atlas form view falls back to inferred kinds for every settable path, which is best-effort and lossy for `Enum` / `Secret` kinds. |

## How it works

The analyzer is purely syntactic — it walks the C# syntax tree looking for:

1. **`InvocationExpressionSyntax`** whose simple method name starts with `AddSunfish` (mirrors the cohort's `AddSunfishX()` DI extension convention).
2. **`ObjectCreationExpressionSyntax`** whose constructed type's simple name equals `AtlasSchemaDescriptor`.

If a compilation contains at least one match for (1) and zero matches for (2), every call site found in (1) gets a `SUNFISH_WAYFINDER001` warning. Adding one descriptor anywhere in the project clears the warning everywhere.

## Cost trade-off

Syntactic detection avoids the live-build chicken-and-egg of resolving `Sunfish.Foundation.Wayfinder.AtlasSchemaDescriptor` against an in-flight build. The cost is some false positives — e.g., a method named `AddSunfishCustomThing` that's unrelated to Wayfinder still triggers the warning. The W#42 P1 council reviewed this trade-off and accepted it (substrate scope; the host project is the false-positive surface, not the substrate).

## References

- ADR 0065 — Wayfinder System + Standing Order Contract
- W#42 hand-off — `icm/_state/handoffs/foundation-wayfinder-stage06-handoff.md` Phase 3b
- Cohort precedent — `packages/analyzers/provider-neutrality/` (similar syntactic-detection pattern)
