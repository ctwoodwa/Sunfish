# Sunfish.Analyzers.LocUnused

Roslyn analyzer that emits **SUNFISH_I18N_002** when a Sunfish `.resx` data entry
has no consuming reference in same-package C# / Razor source.

**Why:** Orphaned resource keys waste translator effort, inflate locale bundles, and rot
silently — there's no compiler error today when a key is removed from code but left in
the resx. The analyzer surfaces drift at build time so developers either remove the
unused key or wire up the missing call site before the resource ships.

**Rule severity:** Error. (Previously Warning, which only failed builds because every
Sunfish project sets `TreatWarningsAsErrors=true` — implicit gating. Promoted to Error
per Plan 5 §"CI gates" so the diagnostic blocks builds independently of warnings-as-errors
policy, mirroring the SUNFISH_I18N_001 cascade pattern.)

**What counts as a reference:** the analyzer recognizes the two canonical
`IStringLocalizer<T>` access patterns:

```csharp
// Pattern A — indexer
var greeting = localizer["Greeting"];

// Pattern B — method call
var greeting = localizer.GetString("Greeting");
```

Both patterns require the resource key as a quoted string literal. `nameof()` references
are not matched (rare in practice for localizer call sites — file an issue if needed).

**Same-package boundary:** the analyzer scopes its search to the compilation's own C#
syntax trees. Each csproj has its own compilation, so cross-package leakage is impossible
by construction. Razor (`.razor`) source contributes via the Razor source generator —
its emitted `.cs` lives in the same compilation, so razor call sites are captured without
a separate file scan.

**How to enable:**

```xml
<ItemGroup>
  <PackageReference Include="Sunfish.Analyzers.LocUnused" Version="..." PrivateAssets="all" />
  <AdditionalFiles Include="Resources/**/*.resx" />
</ItemGroup>
```

**How to suppress:**

```ini
# Per-file via .editorconfig
[*.cs]
dotnet_diagnostic.SUNFISH_I18N_002.severity = none
```

Or remove the unused resource. The analyzer's purpose is to make stale entries impossible
to ignore — suppression is a workflow smell.

See [Plan 5 spec](../../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-6-ci-gates-plan.md)
and [Plan 5 implementation plan](../../../docs/superpowers/plans/2026-04-25-plan-5-ci-gates-implementation-plan.md)
for the rollout context.
