# ADR 0023 — Dialog Provider-Interface Expansion (Per-Slot Class Methods)

**Status:** Accepted (2026-04-22)
**Date:** 2026-04-22
**Pre-release modification:** Because Sunfish is pre-v1 with breaking changes approved and third-party provider compatibility relaxed until first public release, the recommended Option A is adopted with two strengthenings: (1) new slot methods are **abstract** (required) rather than C# default-interface-methods returning `""` — this removes the "third-party providers silently render unstyled slots" negative consequence; (2) `DialogClass(bool isDraggable)` is **deleted outright** with no `[Obsolete]` deprecation cycle. See `project_pre_release_latest_first_policy` memory.
**Resolves:** `SunfishDialog` currently exposes only `DialogClass()`, `DialogClass(bool isDraggable)`, `DialogOverlayClass()`, `DialogCloseButtonClass()`, and `DialogCloseMarkup()` on `ISunfishCssProvider`. The Tier 4 re-audit ([`TIER-4-RE-AUDIT.md`](../../icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md)) confirms this is the one remaining P0 from Phase 1 that was explicitly deferred: Bootstrap 5's `.modal` pattern, Fluent v9's surface/body/actions slots, and Material 3's headline/supporting-text/actions slots all require per-slot class emission that the current flat contract cannot express. This ADR decides the shape of the expanded contract, the compatibility plan for third-party providers, and the React-adapter parity requirement.

---

## Context

Each of the three first-party providers requires structurally distinct dialog chrome:

- **Bootstrap 5** — `<div class="modal"><div class="modal-dialog"><div class="modal-content"><div class="modal-header"><h5 class="modal-title">...<button class="btn-close"></button></div><div class="modal-body">...</div><div class="modal-footer">...</div></div></div></div>`. Six distinct classes, four of which are not expressible through `DialogClass()` today.
- **Fluent UI v9** — surface layer + `fui-DialogBody` + `fui-DialogTitle` + `fui-DialogContent` + `fui-DialogActions`. Size modifiers target a specific inner panel element that today's Razor never renders (see SYNTHESIS Theme 1, finding #4).
- **Material 3** — surface-container-high shell + headline-small title + body-medium content + label-large actions row. Typography-per-slot means class distinctions matter even when the structural divs nest similarly.

`SunfishDialog.razor` emits `sf-dialog-title`, `sf-dialog-body`, `sf-dialog-actions`, and `sf-dialog-close` as hardcoded strings. No BS skin rule targets these classes because the consumer-facing class name is not the Bootstrap class name, and the provider contract has no hook to bridge them. The result is a dialog that renders as an unstyled `<div>` under BS5 and partially styled under Fluent/Material — documented as a Theme 1 "dead-CSS cascade" in [`SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md).

This is the same class of problem ADR 0022 solved for demo catalog coverage — a missing contract hook silently makes entire surface areas unshippable. Style-parity work on Dialog is blocked until the contract is widened.

---

## Decision drivers

- **Shippable dialog under every first-party skin.** BS5 especially must round-trip through `.modal-content/header/body/footer` or it's broken.
- **Framework-agnostic contract.** The contract must describe the *slot* semantically; the provider chooses the framework-specific class. This is the [ADR 0014](0014-adapter-parity-policy.md) rule applied one layer down.
- **React-adapter parity.** Per [ADR 0014](0014-adapter-parity-policy.md), any contract change must be implementable in both Blazor and React adapters from day one. React will need the same slot vocabulary.
- **Third-party provider compatibility.** Any external consumer implementing `ISunfishCssProvider` must keep compiling. Adding abstract methods breaks them.
- **Minimal API surface growth.** Don't invent six methods when three will do — match the actual structural divisions the three target frameworks need.

---

## Considered options

### Option A — Add six per-slot methods with default implementations

Extend `ISunfishCssProvider` with:

```csharp
string DialogContentClass();  // BS5 .modal-content; Fluent/Material inner surface
string DialogHeaderClass();   // BS5 .modal-header; Fluent title row; Material headline row
string DialogTitleClass();    // BS5 .modal-title; Fluent fui-DialogTitle; Material headline-small typography
string DialogBodyClass();     // BS5 .modal-body; Fluent fui-DialogContent; Material supporting-text
string DialogFooterClass();   // BS5 .modal-footer; Fluent fui-DialogActions; Material actions row
string DialogDialogClass();   // BS5 .modal-dialog wrapper (size/centered/scrollable lives here)
```

Each method gets a C# 8 default interface method returning `""` so third-party providers don't break. First-party providers override every method. Razor updates to `<div class="@CssProvider.DialogContentClass()">` etc.

**Tradeoffs:**

- Pro: Single PR, no version bump, third-party providers keep working (just render unstyled slots until they override).
- Pro: Mirrors BS5's actual structural vocabulary exactly — every first-party audit maps one-to-one.
- Pro: Symmetric with existing `CardHeaderClass` / `CardBodyClass` / `CardFooterClass` / `CardActionsClass` methods already in the contract.
- Con: Default-of-`""` means third-party providers silently ship broken dialogs (dead-CSS cascade reappears for them). Mitigated by release-note call-out and a parity test.
- Con: Six more methods on an already-large interface. SYNTHESIS flagged a category-split TODO; this grows the pre-split interface further.

### Option B — Add a single `DialogSlots()` struct-returning method

```csharp
DialogSlotClasses DialogSlots();

public readonly record struct DialogSlotClasses(
    string Dialog, string Content, string Header, string Title,
    string Body, string Footer);
```

**Tradeoffs:**

- Pro: One method instead of six. Cleaner call site inside the Razor.
- Con: All providers pay for all slots even when they'd rather inherit defaults. No incremental override path.
- Con: Struct-returning contracts are unusual in `ISunfishCssProvider` — every other member returns `string`. Inconsistent style.
- Con: Harder to evolve — adding a seventh slot later (e.g., scroll container) is a struct-shape change rather than one additional method with a default.

### Option C — Keep flat contract; let consumer apps supply class overrides via parameters

`SunfishDialog` gets `HeaderClass`, `BodyClass`, etc. `[Parameter]` props. Consumers (kitchen-sink, compat-telerik) write provider-specific override logic.

**Tradeoffs:**

- Pro: Zero interface change. Third-party providers unaffected.
- Con: Pushes provider-framework knowledge into every consumer — directly violates the "design-system-agnostic" promise in the interface's doc comment.
- Con: Makes `compat-telerik` and kitchen-sink responsible for Bootstrap/Fluent/Material class vocabulary. That's the opposite of the whole abstraction.
- Con: No way to make a parity test against the provider — every consumer's overrides diverge.

### Option D — Bump the provider contract to a major version (new `ISunfishCssProvider2`)

Ship `ISunfishCssProvider2 : ISunfishCssProvider` with the six new methods abstract. Consumers upgrade at their own pace.

**Tradeoffs:**

- Pro: Clear versioned contract, no default-of-`""` silent breakage for third-party providers.
- Pro: First-party providers migrate in the same PR as the Razor update.
- Con: Sunfish is pre-v1 (per [`reference_efcore_npgsql_preview_lock.md`](../../.wolf/memory.md) / latest-first policy). Versioning a contract surface before the first public API freeze is premature ceremony.
- Con: Two interfaces to document; consumers ask "which one do I implement". Adds surface-area debt that's worse than the six added methods.

---

## Decision (recommended)

**Adopt Option A** — six per-slot methods with C# default interface implementations returning `""`.

First-party provider migration happens in the same PR as the contract change. Third-party providers keep compiling; their dialogs render structurally but unstyled until they override, which is both honest and detectable (a parity test rendering each provider's dialog and asserting non-empty slot classes flags any missing overrides).

Method names chosen to mirror BS5's structural vocabulary (`modal-content`, `modal-header`, etc.) because BS5 is the most structurally-demanding of the three and its names read naturally on Fluent/Material too.

Drop `DialogClass(bool isDraggable)` — replace with a `DialogDialogClass()` that takes no parameter. Draggability moves to a modifier class the Razor appends based on a `Draggable` parameter, per SYNTHESIS cross-cutting decision #8 (drag-and-drop scope still TBD, but the class name shouldn't conflate two concerns).

---

## Consequences

### Positive

- **Dialog BS5 becomes shippable.** The single biggest P0 gap in Dialog × BS5 (SYNTHESIS Theme 1 finding #3) resolves in one focused PR.
- **Parity test becomes possible.** Render each provider's dialog, assert every `DialogXxxClass()` returns non-empty. Wire into CI.
- **Symmetric with Card contract.** Dialog now matches the per-slot shape already used for Card, reducing "special case" cognitive load.
- **Provider author guidance is clear.** Third-party providers get a checklist of six methods to implement, each with a doc-comment describing the target framework's idiomatic slot.
- **Unblocks SYNTHESIS Batch 2e.** Dialog BS5 size/centered/scrollable/focus-trap work downstream of this ADR.

### Negative

- **Third-party providers silently render unstyled slots.** Default-of-`""` means a consumer using a community provider gets the same dead-CSS cascade we just fixed for first-party. Mitigated by: (a) a release-note call-out naming every new method, (b) the parity test as a validation tool consumers can run against their own provider, (c) doc-comment on each method describing the target slot.
- **Interface surface grows.** The `// TODO(phase-2-followup): split by category` comment at the top of `ISunfishCssProvider` becomes more urgent. That split is a separate ADR; this one just widens the feedback category.
- **One micro-breakage risk.** The existing `DialogClass(bool isDraggable)` overload is removed. Any consumer calling that exact overload needs to update to `DialogDialogClass()` + conditional draggable class. Low-risk (no known third-party consumer; grep'd; only first-party providers use it).
- **React adapter's Dialog must implement the same slot vocabulary in parallel.** Per [ADR 0014](0014-adapter-parity-policy.md), the React-equivalent `ISunfishCssProvider` (or React-native idiom — a hook, a context, etc.) must expose the same six slot concepts.

---

## Compatibility plan

1. **Pre-v1 window, latest-first policy in effect.** Per [`project_pre_release_latest_first_policy.md`](../../_shared/product/architecture-principles.md), no compat aliases required for in-tree consumers — first-party providers update in the same PR.
2. **Release note emphasis.** Next release note leads with the new methods and the parity-test snippet consumers can drop into their own test suite to verify coverage.
3. **`DialogClass(bool isDraggable)` overload removal.** Annotate the existing overload with `[Obsolete("Use DialogDialogClass() and set modifier from Draggable parameter in Razor.", error: false)]` for one release cycle before deletion, per SYNTHESIS cross-cutting decision #8.
4. **compat-telerik impact.** compat-telerik delegates `ISunfishCssProvider` to whichever first-party provider the consumer selects, so it inherits the fix automatically. No additional work.
5. **Parity test in `packages/ui-core/tests/CssProviderContractTests.cs`.** Add a test that resolves every first-party provider, calls every `Dialog*Class()` method, and asserts non-empty.

---

## Implementation checklist

- [ ] Update `ISunfishCssProvider` with six new methods, each with default implementation returning `""` and a doc comment describing the target slot.
- [ ] Implement all six methods on `BootstrapCssProvider` (`modal-dialog`, `modal-content`, `modal-header`, `modal-title`, `modal-body`, `modal-footer`).
- [ ] Implement all six methods on `FluentUICssProvider` (fui-Dialog surface + slot classes per Fluent v9 spec).
- [ ] Implement all six methods on `MaterialCssProvider` (sf-dialog + `__header`, `__title`, `__body`, `__footer` mapping to M3 typography tokens).
- [ ] Update `SunfishDialog.razor` to emit the six slot classes via `CssProvider.DialogXxxClass()` calls.
- [ ] Remove `DialogClass(bool isDraggable)` overload; annotate `[Obsolete]` for one release; wire the `Draggable` parameter to a `sf-dialog--draggable` modifier class appended in the Razor.
- [ ] Add parity test in `CssProviderContractTests.cs` asserting each first-party provider returns non-empty for every slot method.
- [ ] Update `_shared/product/architecture-principles.md` or the equivalent contract doc with the new slot vocabulary.
- [ ] React adapter: author the equivalent slot-concept contract (component props, context, or hook shape) in the React-adapter package spec before landing its implementation.
- [ ] Update `.wolf/buglog.json` entry #4 ("Bootstrap dialog class-name mismatch (structural)") to cite this ADR as the resolution.

---

## References

- [ADR 0014](0014-adapter-parity-policy.md) — UI Adapter Parity Policy. This ADR's React-parity requirement derives from 0014.
- [ADR 0022](0022-example-catalog-and-docs-taxonomy.md) — Example Catalog. Dialog demo pages block on this ADR landing.
- [`icm/07_review/output/style-audits/SYNTHESIS.md`](../../icm/07_review/output/style-audits/SYNTHESIS.md) — Theme 1 (dead-CSS cascades), cross-cutting decision #8 (draggable).
- [`icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md`](../../icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md) — Phase 1 remaining P0s; Dialog-BS5 explicitly deferred to this ADR.
- [`packages/ui-core/Contracts/ISunfishCssProvider.cs`](../../packages/ui-core/Contracts/ISunfishCssProvider.cs) — current Dialog contract surface (methods to extend).
- `.wolf/buglog.json` entry #4 — Bootstrap dialog class-name mismatch.
