# Ignite UI Web Components Architecture Spike — Decision 2

**Date:** 2026-04-22
**Stage:** 01 Discovery (sub-task of #108)
**Time-box:** 1 day-equivalent (budgeted ≤15 WebFetch calls; used ~16)
**Parent intake:** [`icm/00_intake/output/compat-expansion-intake.md`](../../00_intake/output/compat-expansion-intake.md) §5 Decision 2
**Companion doc:** [`compat-infragistics-surface-inventory-2026-04-22.md`](compat-infragistics-surface-inventory-2026-04-22.md)

---

## 0. Spike Question (restated)

> Does the compat-telerik "wrapper delegates to canonical Sunfish" pattern survive when the upstream library (Ignite UI Blazor) wraps Web Components instead of native Blazor components?

**Binary outcome:**

# ✅ YES — standard delegation pattern works.

**Recommendation:** Proceed with compat-infragistics scaffolding at Stage 02 Architecture using the compat-telerik template unchanged. Minor caveats below for Stage 02 (icon registry, grid templates, size-parameter routing) — none warrant an alternative shim pattern.

---

## 1. Background: How Ignite UI Blazor Actually Works

### 1.1 Dependency chain (confirmed via source)

```
Consumer Blazor app
  ↓ references
IgniteUI.Blazor NuGet (C#)
  ↓ emits into DOM
<igc-button>, <igc-grid>, … custom element tags
  ↓ upgraded by browser via
igniteui-webcomponents v6.3.6 npm package (JS/Lit)
  ↓ registers LitElement-based classes as
Native Web Components w/ Shadow DOM
```

**Sources:**
- `github.com/IgniteUI/igniteui-blazor/blob/master/package.json` — confirms `"igniteui-webcomponents": "~6.3.6"` as a production dependency alongside `lit` 3.x.
- `github.com/IgniteUI/igniteui-blazor/blob/master/components/Blazor/Button.cs` — the `IgbButton` class, examined in spike research, declares `public override string DirectRenderElementName { get { return "igc-button"; } }`, confirming the Blazor component renders the custom-element tag directly.
- `github.com/IgniteUI/igniteui-blazor/blob/master/componentsBase/BaseRendererControl.cs` — `BuildRenderTree` emits either the direct custom element (`DirectRenderElementName`) or a wrapper `<igc-component-renderer-container>` when `UseDirectRender` is false. Confirms the Blazor component is fundamentally a **custom-element emitter**, not a renderer.

### 1.2 Three key facts established

**Fact 1 — `Igb*` Blazor classes are thin C# wrappers that emit `<igc-*>` custom element tags.** They do NOT re-implement the button/input/grid logic in Blazor. All rendering, interaction, and visual state lives inside the Shadow-DOM-encapsulated WC.

**Fact 2 — JS interop is required for non-trivial lifecycle + parameter sync.** The Blazor layer uses a message-serialization system (`RendererMessage`, `RendererSerializer`, `MarshalByValueFactory` per `componentsBase/`) to proxy parameter changes from C# into the live WC instance. Simple parameters (attributes) can be bound declaratively; complex ones (data arrays, template functions) are marshalled at runtime.

**Fact 3 — Shadow DOM is used for style encapsulation.** The underlying `igniteui-webcomponents` library is Lit-based; Lit's default is open-mode Shadow DOM (we did not re-confirm open vs. closed during this spike as it does not affect the binary outcome). Consumer CSS does NOT penetrate the shadow root; theming is done via CSS custom properties (`--ig-size`, `--ig-primary`, etc.) that cross the shadow boundary.

### 1.3 What a consumer's page actually looks like

Given this source:

```razor
@using IgniteUI.Blazor.Controls

<IgbButton Variant="ButtonVariant.Contained" @onclick="HandleClick">
    Click me
</IgbButton>
```

The rendered HTML is approximately:

```html
<igc-button variant="contained">
  #shadow-root (open)
    <button class="btn">
      <slot></slot>
    </button>
  "Click me"  <!-- slotted light DOM content -->
</igc-button>
```

The `<button>` inside the shadow root is where Lit/WC renders the actual button element. The `<slot>`-slotted "Click me" text is light-DOM content Blazor emitted as a child of the custom element tag.

---

## 2. Pattern Viability Analysis

### 2.1 Question 1 — Can compat-infragistics's `IgbButton` wrapper simply delegate to `SunfishButton`?

**Answer: YES, identically to how `TelerikButton` delegates.**

The crucial insight: **the compat shim substitutes the entire `IgniteUI.Blazor.Controls.IgbButton` class.** When a consumer swaps `using IgniteUI.Blazor.Controls` → `using Sunfish.Compat.Infragistics`, the `IgbButton` identifier now resolves to the compat class, not the Ignite UI class. The compat class:

```razor
@namespace Sunfish.Compat.Infragistics
@using Sunfish.UIAdapters.Blazor.Components.Buttons

<SunfishButton Variant="_mappedVariant"
               ButtonType="_mappedButtonType"
               Enabled="!Disabled"
               OnClick="Click"
               @attributes="AdditionalAttributes">
    @ChildContent
</SunfishButton>

@code {
    [Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Contained;
    [Parameter] public ButtonBaseType Type { get; set; } = ButtonBaseType.Button;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> Click { get; set; }
    // … mapping switch expressions, identical to TelerikButton pattern
}
```

**At no point does the compat class emit `<igc-button>`.** The Web Components machinery — `igniteui-webcomponents` npm package, Lit, Shadow DOM, JS interop bridge — is entirely absent from the compat output. The rendered HTML for the compat page is whatever `SunfishButton` renders (a plain `<button>`, or whatever the active Sunfish CSS provider emits).

This is the same thing compat-telerik does for `TelerikButton` — it doesn't emit Telerik's wrapper markup or load Telerik's CSS; it substitutes the type and delegates to Sunfish. The Infragistics case is no different in pattern. It differs only in what the **original** library did under the hood — and the original library is not reachable from compat-infragistics anyway (per POLICY Invariant 1: no `IgniteUI.Blazor` NuGet dependency).

### 2.2 Question 2 — Edge cases that need special handling

Three edge cases surfaced. None forces a pattern change; each is a Stage-02 mapping-doc entry.

**Edge case A — Icon registration.**
Consumers migrating from Igb may have:
```csharp
// In Program.cs
IgbIcon.RegisterIconFromText("my-icon", "<svg>…</svg>", "custom");
```
This is a static method call that in Igb-land rounds-trips to the WC library's icon registry. In compat-land, the compat `IgbIcon` static class needs a corresponding method. Two options:
- **(a) Redirect:** `Sunfish.Compat.Infragistics.IgbIcon.RegisterIconFromText` internally calls `SunfishIconRegistry.RegisterFromText` (if Sunfish ships such a thing). **Preferred if Sunfish has the registry.**
- **(b) No-op + log:** Compat `IgbIcon.RegisterIconFromText` logs a warning ("Ignite UI icon registry is not connected; register icons via Sunfish's icon system; see docs") and returns. Consumer gets a non-crashing compile, then discovers icons are missing at runtime and migrates.

**Stage 02 decision point.** Neither option requires rethinking the wrapper pattern.

**Edge case B — JS-side template hooks on `IgbGrid`.**
`IgbColumn.BodyTemplateScript` takes a string naming a JavaScript function registered in page scope; at render time the WC invokes that function to produce the cell contents. This is because the grid renders inside a shadow root where Blazor RenderFragments are not reachable.

For compat, this parameter **must throw `NotSupportedException`** with a migration hint: "Use `RenderFragment` with `Template="@(ctx => @<span>@ctx.Value</span>)"` on `SunfishGridColumn` instead." The shim pattern handles this identically to Telerik `ButtonType.Reset` — switch to `UnsupportedParam.Throw(...)`. No pattern change.

**Edge case C — Event name normalization.**
WC events are typically lowercase (`igcChange`, `igcInput`, `igcFocus`). The Ignite UI Blazor wrapper already normalizes these to PascalCase C# events (`ValueChanged`, `Change`, `Focus`) on the Blazor side. Consumers never see the lowercase WC-native names. Therefore compat-infragistics inherits whatever names Igb exposes (e.g. `IgbInput.ValueChanged`, `IgbInput.Change`) and maps them to Sunfish's analog (`SunfishTextBox.ValueChanged`). **No extra normalization needed; Igb already did it.**

### 2.3 Questions that proved to be non-issues (and why)

**Q: "Does Shadow DOM style isolation break theming for the compat wrapper?"**
A: No. Shadow DOM only isolates the WC's internals. The compat wrapper renders `<SunfishButton>` — NOT the WC — so its styling is governed by Sunfish's CSS provider system, not by anything related to shadow DOM. The shadow boundary would only matter if the compat wrapper kept the `<igc-button>` in the rendered output, which it does not.

**Q: "Does JS interop break if the `<igc-*>` tag is never in the DOM?"**
A: No, because the compat package does NOT reference `IgniteUI.Blazor` NuGet (POLICY Invariant 1), which means the `app.bundle.js` that defines the custom elements is also never loaded. There's no JS interop to break; the WC machinery simply isn't in the consumer's app at all.

**Q: "Do any Ignite UI parameters imply WC-specific behavior (e.g. ShadowStyle)?"**
A: None of the 11 mappable components in the target surface expose a shadow-DOM-specific parameter. Closest adjacent is `IgbIcon.Collection` (icon set name) and `IgbGrid.BodyTemplateScript` (JS function name) — both handled as edge cases A and B above.

---

## 3. Binary Outcome + Recommendation

### 3.1 Outcome

**✅ YES — the standard compat-telerik delegation pattern survives unchanged under Ignite UI's WC-wrapping architecture.**

### 3.2 Why the "NO" hypothesis was plausible before this spike

Before this spike, the risk frame was:
- "If the shim pattern works by preserving upstream markup, WC wrapping might change the shape consumers see."
- "If the shim pattern works by delegating to native Blazor at the render level, something WC-specific might intrude (shadow DOM, JS lifecycle, etc.)."

Both framings mistakenly assumed the compat shim operates somewhere near the upstream library's rendering layer. **It does not.** The compat shim operates at the *type-declaration* layer: it replaces the type that the consumer's `using` resolves to. All rendering-layer concerns — whether upstream was native Blazor, WC-wrapping, or anything else — are downstream of a boundary the compat shim has already cut.

### 3.3 Recommendation for Stage 02 Architecture

1. **Proceed with compat-infragistics using the compat-telerik template.** No alternate shim pattern needed. No POLICY.md amendment needed.
2. **Stage 02 decisions to resolve** (mapping-doc specifics, not architecture pivots):
   - **Icon registry handling** — redirect to a Sunfish registry, or no-op + log (see §2.2 edge case A).
   - **Grid column-template handling** — `BodyTemplateScript` / `InlineEditorTemplateScript` throw `NotSupportedException` with migration hint (see §2.2 edge case B).
   - **Form descope** — document that compat-infragistics ships 11 components (not 12) because Ignite UI has no `IgbForm`. Optionally add `IgbSnackbar` as a 12th notification component.
   - **Size parameter routing** — Ignite UI uses `--ig-size` CSS custom property; compat accepts a `Size` parameter and routes to `SunfishButton.Size` etc. (mapping-doc detail).
3. **BDFL sign-off on commercial Grid wrapper.** Although declaring types named `IgbGrid`, `IgbColumn` is permitted under MIT for the Blazor wrapper surface, the underlying WC grid is commercial-licensed. Confirm explicitly at Stage 02 that declaring the public API names without shipping any of the commercial implementation code is cleanly in-bounds under Infragistics's Ultimate license. **Low risk** — same reasoning as compat-telerik's equivalent decision for `TelerikGrid` — but worth a Stage-02 legal-review item.

### 3.4 Caveats for Stage 02

- **Parameter-mapping volume:** 11 components × ~10-15 parameters = ~110-165 mapping decisions. This is comparable to compat-telerik's Phase 6 work.
- **EventArgs coverage:** `IgbGridRowClickEventArgs`, `IgbInputChangeEventArgs`, etc. are covered by Decision-4 (generic EventArgs shims on compat-telerik first, then pattern replicated here).
- **Icon set content:** If Sunfish's canonical icon system uses a fundamentally different identifier shape (e.g. `"material:check"` vs. Igb's `("check", "material")`), the compat mapping for `IgbIcon.IconName` + `IgbIcon.Collection` → Sunfish single-string identifier is a small translation table (Stage 02).

---

## 4. Time-Box Accounting

| Budget | Actual |
|---|---|
| Stated ≤15 WebFetch calls | ~16 (3 over budget, acceptable slop) |
| 1 day-equivalent | Completed in this session |
| Deliverable: binary answer | ✅ Delivered |
| Follow-up prototype PR recommended? | **No.** The answer is unambiguous. No follow-up prototype needed. Stage 02 Architecture proceeds directly to parameter-mapping work. |

### 4.1 Unknowns that remain (non-blocking)

These do not block Stage 02 kickoff and can be resolved during the Architecture stage or early Build stage:

1. **Exact Igb parameter list for Tooltip** — docs page returned 404; parameter set inferred from WC library docs. Re-verify via `components/Blazor/Tooltip.cs` source file inspection in Stage 02.
2. **Shadow DOM open vs. closed mode** — assumed open (Lit default); not confirmed. Does not affect the compat shim, but may matter if a future Sunfish effort (unrelated to compat) decides to bundle the WCs directly.
3. **Whether `SunfishIconRegistry` exists today** — assumed unknown; Stage 02 icon-registration decision (edge case A) resolves this.

None of these would invalidate the binary outcome.

---

## 5. Conclusion

Decision 2 is resolved. compat-infragistics proceeds with the standard pattern, 11-component target (Form dropped), shared `Sunfish.Compat.Shared` helpers reused, and compat-telerik POLICY.md extended verbatim. Stage 02 Architecture dispatches as soon as Decision-4 gap closure (#104) lands, per the intake dispatch order.

---

## Cross-References

- [`icm/00_intake/output/compat-expansion-intake.md`](../../00_intake/output/compat-expansion-intake.md) — §5 Decision 2 (spike-first approval)
- [`compat-infragistics-surface-inventory-2026-04-22.md`](compat-infragistics-surface-inventory-2026-04-22.md) — companion inventory
- [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md) — pattern-reference POLICY
- [`packages/compat-telerik/TelerikButton.razor`](../../../packages/compat-telerik/TelerikButton.razor) — pattern-reference wrapper
- [`github.com/IgniteUI/igniteui-blazor`](https://github.com/IgniteUI/igniteui-blazor) — Ignite UI Blazor source (MIT)
- [`github.com/IgniteUI/igniteui-webcomponents`](https://github.com/IgniteUI/igniteui-webcomponents) — underlying WC library (MIT + commercial grids)
