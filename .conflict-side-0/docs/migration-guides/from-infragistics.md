# Migrating from Infragistics Ignite UI for Blazor to Sunfish

Sunfish ships `Sunfish.Compat.Infragistics`, a local-first, fully-open-source shim that
mirrors the Infragistics Ignite UI `Igb*` component surface so you can migrate off Ignite
UI without rewriting markup. The compat package wraps canonical Sunfish components,
carries **zero runtime dependency on the Ignite UI NuGets** (no `<igc-*>` Web Components,
no Lit, no Shadow DOM, no JS interop), and is designed as a drop-in swap for most common
`Igb*` component usages.

## Prerequisites

- A .NET 9 Blazor project that currently references `IgniteUI.Blazor`.
- Install `Sunfish.Compat.Infragistics` from NuGet (version pin TBD — pin against a
  specific prerelease until v1).
- Install `Sunfish.Analyzers.CompatVendorUsings` — the Roslyn analyzer that detects Ignite
  UI usings and offers a code fix. Reference it with `PrivateAssets="all"`.

## Step-by-step

1. **Add the package references:**

   ```xml
   <ItemGroup>
     <PackageReference Include="Sunfish.Compat.Infragistics" />
     <PackageReference Include="Sunfish.Analyzers.CompatVendorUsings" PrivateAssets="all" />
   </ItemGroup>
   ```

2. **Run the analyzer.** Every `using IgniteUI.Blazor;` or child namespace
   (`IgniteUI.Blazor.Controls`, etc.) raises **SF0001**.

3. **Apply the code fix.** The fix collapses every flagged Ignite UI using to the single
   `using Sunfish.Compat.Infragistics;`.

4. **Build and run tests.** Component names (`IgbButton`, `IgbCheckbox`, `IgbGrid`,
   `IgbDialog`, etc.) and most parameter shapes are preserved.

5. **Review the divergence log:** [`docs/compat-infragistics-mapping.md`](../compat-infragistics-mapping.md).

6. **Address manual follow-ups.** The visible divergences you'll hit first:

   - **No `IgbForm` — Ignite UI itself doesn't ship one.** Migrators keep their existing
     `<EditForm>` wrapper; the children (`IgbInput`, `IgbCheckbox`, etc.) continue to work
     because they implement `IComponent` directly and don't require a parent form.
   - **`Checked` vs `Value` on `IgbCheckbox`.** Sunfish's canonical checkbox uses
     `Value`/`ValueChanged`; Ignite UI uses `Checked`/`CheckedChanged`. The compat shim
     preserves Ignite UI's naming — your `@bind-Checked` / `Checked="@isOn"` markup
     continues to compile unchanged.
   - **`--ig-size` CSS variable is a pass-through, not a parameter.** Most Ignite UI
     components size themselves via the `--ig-size` CSS custom property. `IgbButton`
     exposes an explicit `Size` parameter (mapped to Sunfish `ButtonSize`); elsewhere, the
     value rides via `style="--ig-size: 3"` on `AdditionalAttributes` but Sunfish does not
     interpret it — expect no runtime sizing effect outside the Button surface.
   - **`IgbGrid` is shape-parity only.** The underlying `igniteui-webcomponents` Data
     Grid is commercial-licensed — the compat declares the API shape without pulling the
     commercial runtime. The MIT Blazor wrapper is safe; going beyond shape-parity
     delegation requires a BDFL policy sign-off. `BodyTemplateScript` and
     `InlineEditorTemplateScript` throw `NotSupportedException` — migrate to the
     `Template` / `EditorTemplate` `RenderFragment` pattern.
   - **Shadow DOM / Lit / JS interop are absent from rendered output.** The compat does
     not emit `<igc-*>` tags. Code that reaches into the Ignite UI WC registry via JS
     interop will not find the elements; `IgbIcon.RegisterIconFromText` /
     `RegisterIcon` are re-implemented as a process-local dictionary so your registration
     calls at `Program.cs` compile and return some behavior.

## When NOT to use this package

If your product depends on Ignite UI's Web Components runtime, its themes, its imperative
JS interop (the `ShowAsync`/`HideAsync`/`StepUpAsync` surface on `IgbDatePicker` is
unshimmed), the Ignite UI grid's commercial interactive features, or the `<igc-*>` custom
elements specifically, stay on Ignite UI. This compat gives you **source-shape parity**,
not visual or behavioral parity.
