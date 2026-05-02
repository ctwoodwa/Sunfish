# Council Review — ADR 0009 Amendment A1 (Operator-Issued Feature Toggles; Wayfinder Consumer)

**Review date:** 2026-05-02
**Reviewer:** XO research session (in-thread; per cohort preference for reliability)
**Review posture:** standard adversarial (4 perspectives: Outside Observer / Pessimistic Risk Assessor / Skeptical Implementer / Devil's Advocate) + UPF v1.2 Stage 2 meta-validation + 21 anti-pattern scan. NO WCAG/a11y subagent (API-only amendment; no UI surface). NO Pedantic Lawyer (no regulatory tier).
**Cohort batting average context:** 19-of-19 substrate amendments needed council fixes; structural-citation failure rate ~65% NOT caught by §A0 alone.

---

## Findings (6 total)

### F1 — Major: `FeatureValue { Raw = raw }` object-initializer form may be incorrect

**Perspective:** Skeptical Implementer (structural-citation correctness)
**Issue:** The amendment uses `new FeatureValue { Raw = raw }` in the `WayfinderFeatureProvider` implementation. Verification against `packages/foundation-featuremanagement/FeatureValue.cs` is required. Reading the actual file:

`FeatureValue.cs` content (verified):

```csharp
namespace Sunfish.Foundation.FeatureManagement;

public sealed record FeatureValue
{
    public required string Raw { get; init; }
    // ...
}
```

`Raw` is a `required` init-only property. The object-initializer form `new FeatureValue { Raw = raw }` is the correct and only way to construct `FeatureValue`. The `required` modifier means the property must be set in the initializer — object-initializer is correct. **Self-correction: this is structurally valid.**

However, the amendment text in §A0.3 item (f) says "verified `FeatureValue.cs` which uses `public string Raw { get; init; }`" — this is **slightly wrong**: the actual declaration is `public required string Raw { get; init; }`. The `required` keyword matters: it means the object-initializer form is mandatory, not optional. The code itself is correct; the description of verification is slightly imprecise.

**Disposition:** Mechanical. Update §A0.3(f) to cite `required string Raw` correctly.

---

### F2 — Major: `IAtlasProjector.ProjectAsync` scope parameter type mismatch risk

**Perspective:** Skeptical Implementer (structural-citation correctness)
**Issue:** The amendment's `WayfinderFeatureProvider` code calls:

```csharp
var atlasView = await _projector.ProjectAsync(
    tenantId,
    scopeFilter: StandingOrderScope.Tenant,
    cancellationToken).ConfigureAwait(false);
```

The ADR 0065 §5 `IAtlasProjector` signature is:

```csharp
ValueTask<AtlasView> ProjectAsync(
    TenantId tenantId,
    StandingOrderScope? scopeFilter,
    CancellationToken ct);
```

The parameter name in ADR 0065 is `ct`; the amendment uses `cancellationToken`. Parameter names don't affect the call site. **However**, the named argument `scopeFilter: StandingOrderScope.Tenant` is being used — this is correct and matches ADR 0065's parameter name `scopeFilter`. **No issue here** from a call-site perspective.

The real risk: `IAtlasProjector` is not yet in code (`foundation-wayfinder` package is W#42 and not yet shipped per §A0.1). If W#42 Phase 1 lands before this amendment is merged and the actual `scopeFilter` parameter name differs, the named-argument call will produce a compile error. This is already flagged in §A0.3(c) — the council confirms this as the primary structural-citation risk.

**Disposition:** Non-mechanical (cannot resolve until W#42 Phase 1 ships). **Annotated as a named halt-condition for COB:** "verify `IAtlasProjector.ProjectAsync` parameter name `scopeFilter` matches the W#42 Phase 1 implementation before adding the `ProjectReference` to `Sunfish.Foundation.Wayfinder`."

---

### F3 — Minor: `AddSunfishFeatureManagementWithWayfinder` registers `WayfinderFeatureProvider` via last-wins — correct but undocumented in the code

**Perspective:** Skeptical Implementer
**Issue:** The amendment documents last-wins semantics for `AddSingleton<IFeatureProvider, WayfinderFeatureProvider>()` but doesn't note that Microsoft DI's last-wins behavior is a well-known footgun: if a host calls `AddSunfishFeatureManagement()` before another library also calls `AddSingleton<IFeatureProvider, SomeOtherProvider>()`, the final registration wins. The amendment should add a clarifying note that `AddSunfishFeatureManagementWithWayfinder()` must be called *after* any other `IFeatureProvider` registrations to ensure `WayfinderFeatureProvider` is the effective implementation.
**Disposition:** Mechanical. Add a `// NOTE: must be called after any other IFeatureProvider registrations; last-wins semantics` comment in the `AddSunfishFeatureManagementWithWayfinder()` extension body (in §A1.4 DI registration code snippet).

---

### F4 — Minor: §A1.4 implementation note on `JsonNode.ToString()` for non-boolean values is incomplete

**Perspective:** Devil's Advocate
**Issue:** The amendment explains the round-trip for boolean toggles (`JsonNode.ToString()` → `"true"`/`"false"` → `AsBoolean()`) but `FeatureValue` also supports `String`, `Integer`, `Decimal`, and `Json` kinds (from `FeatureValueKind`). For a `FeatureKey` of kind `Integer`, the Standing Order `NewValue` would be a JSON number node (e.g., `JsonNode` representing `42`). `JsonNode.ToString()` on a number node produces `"42"` — which `AsInt32()` would correctly parse via `int.Parse(Raw)`. For `Json` kind, `JsonNode.ToString()` produces the canonical JSON string — correct for `AsJson()`.

However, for `String` kind where the operator stores a quoted JSON string value (e.g., `"hello"`), `JsonNode.ToString()` produces `hello` (unquoted) — not `"hello"`. `AsString()` returns `Raw` directly, so the value is correctly `hello`, which is what the user intended. No behavioral defect, but the amendment implies the round-trip is only well-defined for booleans. All five kinds are actually well-defined.

**Disposition:** Mechanical. Add a sentence to §A1.4 after the boolean explanation: "For all five `FeatureValueKind` values, `JsonNode.ToString()` produces the raw value that `FeatureValue`'s typed accessors expect: `String` nodes produce the unquoted string; `Number` nodes produce the number literal; `Boolean` nodes produce `"true"`/`"false"`; `Json` nodes produce canonical JSON. The round-trip is correct for all supported kinds."

---

### F5 — Minor: `FeatureKey.cs` actual declaration — verify it is a `readonly record struct` not a `record class`

**Perspective:** Skeptical Implementer (structural-citation)
**Issue:** The amendment's §A0.3(a) states "`FeatureKey` is a `readonly record struct(string Value)`." This needs verification — `FeatureKey.cs` in the actual codebase:

```csharp
namespace Sunfish.Foundation.FeatureManagement;

public readonly record struct FeatureKey(string Value);
```

Verified as `readonly record struct`. The path construction `$"features.{key.Value}"` is correct — `key.Value` is the positional primary constructor property accessor. **No issue.** §A0.3(a) is accurate.

**Disposition:** Pass (no fix needed). Recorded for audit trail completeness.

---

### F6 — Minor: Missing cross-reference in ADR 0009 parent-body §Follow-ups

**Perspective:** Outside Observer
**Issue:** The original ADR 0009 §Consequences → §Follow-ups (items 1–5) does not mention the now-existing Amendment A1. A reader of the original body has no signal that a 5th concept exists. The amendment frontmatter sets `amendments: [A1]` which is correct, but the follow-up list in the base body should have a note pointing down to the amendment section.
**Disposition:** Mechanical. Append a 6th item to the §Follow-ups list: "**Operator-issued feature toggles** (Amendment A1) — extends this ADR with a fifth concept; see `## Amendment A1` below."

---

## UPF v1.2 Stage 2 — 7 meta-validation checks

| Check | Result | Note |
|---|---|---|
| 1. Delegation strategy clarity | PASS | Hand-off to sunfish-PM is clear: 1 PR, additive files only, ~3-5h. No delegation to subagents required. |
| 2. Research needs identified | PASS | §A1.6 follow-up #3 explicitly defers Atlas schema registration to W#42 Phase 3b; no unresolved empirical research questions blocking the amendment itself. |
| 3. Review gate placement | PASS | Pre-merge council canonical (this review); COB halt-condition on `IAtlasProjector` parameter verification named explicitly. |
| 4. Anti-pattern scan (21 patterns) | See below | 2 hits (AP1, AP21). Both addressed. |
| 5. Cold Start Test | PASS | A fresh COB session with this amendment + ADR 0009 base + ADR 0065 §5 can implement `WayfinderFeatureProvider` + DI extensions without further XO context. |
| 6. Plan Hygiene Protocol | PASS | §A1.7 implementation checklist is complete; 8 tests specified; ledger flip included. |
| 7. Discovery Consolidation Check | PASS | W#34 §6.1 cited in §A1.1; ADR 0065 §5 cited throughout; intake cross-referenced in §A1.9. |

**21 anti-pattern scan:** 2 hits:
- **AP1 (Unvalidated assumptions):** assumed `IAtlasProjector` parameter names match ADR 0065 prose — flagged in F2 as named halt-condition. Addressed.
- **AP21 (Assumed facts without sources):** §A0.3(f) description of `FeatureValue.Raw` slightly imprecise (`required` omitted) — flagged in F1. Addressed mechanically.

---

## Mechanical-fix list (Decision Discipline Rule 3 auto-accept)

1. **F1:** §A0.3(f) — update description to cite `required string Raw` correctly.
2. **F3:** §A1.4 DI registration code snippet — add last-wins comment on `AddSunfishFeatureManagementWithWayfinder()`.
3. **F4:** §A1.4 — add sentence clarifying the `JsonNode.ToString()` round-trip is correct for all five `FeatureValueKind` values.
4. **F6:** ADR 0009 base §Consequences → §Follow-ups — append item 6 pointing to Amendment A1.

## Non-mechanical findings (CO discretion)

- **F2:** `IAtlasProjector` parameter name verification — resolved by COB at build time; named halt-condition in §A1.7. No CO action required unless W#42 Phase 1 has already landed with a different parameter name.

---

## Cohort discipline log

- §A0 self-audit caught: 1-of-1 potential structural-citation issue (the `required` description imprecision in F1 — §A0.3(f) was partially incorrect). Council upgraded it to a finding.
- Council caught: 1 additional structural issue (F1 precision) + 4 additional minor findings (F2 halt-condition, F3 DI comment, F4 kind round-trip, F6 cross-reference).
- Cohort batting average: 20-of-20 ADR amendments now needed council fixes (council remains canonical defense; §A0 alone insufficient — confirmed again).

## Verdict

**Mechanical fixes (F1, F3, F4, F6):** apply via Decision Discipline Rule 3. Committed in the amendment PR.

**Non-mechanical (F2):** resolved by COB at build time. Named halt-condition in §A1.7.

Council recommends: PR is mergeable after mechanical fixes are applied. No CO sign-off needed for the non-mechanical finding — it is a build-time verification, not a design decision.
