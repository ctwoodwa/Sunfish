# Sunfish.Analyzers.LocComments

Roslyn analyzer that emits **SUNFISH_I18N_001** when a Sunfish `.resx` data entry is missing
a translator-context comment.

**Why:** Translators authoring Sunfish locales need surrounding context for every string —
who says it, when, with what intent. Empty `<comment>` blocks are the leading cause of
mistranslation across global locale rollouts. The analyzer surfaces missing context at
build time so developers add it before the resource ships.

**Rule severity:** Error. (Previously Warning, which only failed builds because every
Sunfish project sets `TreatWarningsAsErrors=true` — implicit gating. Promoted to Error
per Plan 5 §"CI gates" so the diagnostic blocks builds independently of warnings-as-errors
policy.)

**How to enable:**

```xml
<ItemGroup>
  <PackageReference Include="Sunfish.Analyzers.LocComments" Version="..." PrivateAssets="all" />
  <AdditionalFiles Include="Resources/**/*.resx" />
</ItemGroup>
```

**How to suppress:**

```xml
<!-- Per-file via .editorconfig -->
[*.cs]
dotnet_diagnostic.SUNFISH_I18N_001.severity = none

<!-- Per-resource: just author the comment. The analyzer's purpose is to make this
     impossible to forget — suppression is a workflow smell. -->
```

See [spec §8](../../../docs/superpowers/specs/2026-04-24-global-first-ux-design.md) for the
canonical diagnostic ID list and [Plan 2 Task 4.3](../../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md)
for the rollout context.
