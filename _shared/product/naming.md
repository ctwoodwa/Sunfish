# Naming Conventions

**Status:** Accepted
**Last reviewed:** 2026-04-21
**Governs:** Every new package, namespace, project, assembly, bundle key, module key, feature key, provider key, template id, JSON property, and fixture filename in the repo.
**Agent relevance:** Loaded by agents creating new packages, namespaces, projects, bundle keys, module keys, or feature keys. High-frequency for scaffolding tasks.

Naming is a load-bearing part of Sunfish's layered architecture. The conventions below are already in use across the repo. When in doubt, copy the shape of a recent adjacent package (`packages/foundation-multitenancy/` for Foundation, `packages/blocks-leases/` for a domain module).

## Repo paths

### Package folders

Kebab-case, prefixed by tier:

| Tier | Folder prefix | Example |
|---|---|---|
| Foundation | `foundation` or `foundation-*` | `packages/foundation-catalog` |
| Kernel | `kernel*` | `packages/kernel-event-bus` |
| UI Core | `ui-core` | `packages/ui-core` |
| UI Adapters | `ui-adapters-<framework>` | `packages/ui-adapters-blazor` |
| Compat shims | `compat-<vendor>` | `packages/compat-telerik` |
| Domain modules | `blocks-*` | `packages/blocks-rent-collection` |
| Ingestion | `ingestion-*` | `packages/ingestion-forms` |
| Federation | `federation-*` | `packages/federation-common` |

Multi-word names use kebab (`blocks-rent-collection`). Never underscores. Never PascalCase.

### Other paths

- `accelerators/<name>/` — kebab-case if multi-word. Today: `accelerators/bridge/`.
- `scripts/` — kebab or snake, `.sh`.
- `docs/adrs/` — `NNNN-short-slug.md`, starting at 0001.
- `_shared/{design,engineering,product}/` — standards and policy docs.

## Namespaces

`Sunfish.<Tier>.<Name>` — kebab folder segments become PascalCase (`rent-collection` → `RentCollection`):

- `packages/foundation-multitenancy` → `Sunfish.Foundation.MultiTenancy`
- `packages/ui-adapters-blazor` → `Sunfish.UIAdapters.Blazor`
- `packages/blocks-rent-collection` → `Sunfish.Blocks.RentCollection`
- `packages/compat-telerik` → `Sunfish.Compat.Telerik`

**Apps and accelerators** use the flat `Sunfish.<Name>` pattern — no `Apps.` or `Accelerators.` tier prefix — because they are top-level deployables ([ADR 0016](../../docs/adrs/0016-app-and-accelerator-naming.md)). Hyphens collapse out:

- `apps/kitchen-sink` → `Sunfish.KitchenSink`
- `accelerators/bridge` → `Sunfish.Bridge` (sub-projects: `Sunfish.Bridge.Data`, `Sunfish.Bridge.Client`, `Sunfish.Bridge.AppHost`, etc.)

**React adapter namespace is unresolved.** Expected `Sunfish.UIAdapters.React` but the shape is pending a React-adapter ADR — do not introduce React code that assumes it.

## Projects, assemblies, tests

The `.csproj` filename matches the namespace (`Sunfish.Foundation.MultiTenancy.csproj`). Test projects are named `tests.csproj` inside a `tests/` subfolder and set `<AssemblyName>` explicitly to avoid `tests.dll` collisions:

```xml
<AssemblyName>Sunfish.Foundation.MultiTenancy.Tests</AssemblyName>
<RootNamespace>Sunfish.Foundation.MultiTenancy.Tests</RootNamespace>
```

## `Sunfish.slnx` folder names

Lowercase, forward-slash, kebab for multi-word segments: `/foundation/feature-management/`, `/blocks/rent-collection/`, `/accelerators/bridge/`. Match the package folder name.

## Reverse-DNS keys

Lowercase segments, dots, kebab slugs for multi-word names:

| Key kind | Pattern | Example |
|---|---|---|
| Bundle | `sunfish.bundles.<slug>` | `sunfish.bundles.property-management` |
| Module | `sunfish.blocks.<slug>` | `sunfish.blocks.rent-collection` |
| Feature | `sunfish.blocks.<module>.<dotted-scope>` | `sunfish.blocks.leases.renewals.autoReminders` |
| Provider | `sunfish.providers.<vendor>` | `sunfish.providers.stripe` |

Feature-key leaves are camelCase when multi-word. Boolean flags end in `.enabled` or an adjective (`.required`). Third-party provider publishers use their own reverse-DNS (`com.example.providers.custom-billing`).

**Extension fields** are camelCase within an entity scope (`moveInChecklist`, `petPolicy`) — no reverse-DNS prefix; the entity type disambiguates.

**Template ids** are URL-style with a version suffix: `https://sunfish.io/schemas/<module-or-bundle>/<template-slug>/<version>`. Tenant overlays: `tenant://<tenant-slug>/<template-slug>`.

## JSON conventions

- Property names: **camelCase** (`requiredModules`, `deploymentModesSupported`).
- Enum values: **PascalCase** via `JsonStringEnumConverter` (`"category": "Operations"`).
- Dictionary keys follow what they identify: edition names lowercase (`lite`, `standard`), feature keys use the reverse-DNS feature-key pattern.

## File names

- ADR: `NNNN-short-slug.md` (`0007-bundle-manifest-schema.md`).
- Bundle seed: `<key-slug>.bundle.json` (`property-management.bundle.json`).
- Meta-schema: `<kind>.schema.json` (`bundle-manifest.schema.json`).
- C# source: `<TypeName>.cs`, one public type per file when practical; interfaces `I<Name>.cs`.

## C# identifier casing

See [coding-standards.md §Identifier conventions](../engineering/coding-standards.md#identifier-conventions). This file retains only name *shapes* (packages, namespaces, keys); intra-file casing and suffix rules live with the rest of the C# style.

## Commit message subjects

See [commit-conventions.md](../engineering/commit-conventions.md) — Sunfish follows Conventional Commits 1.0.0. Legacy `G<num> <letter>:` and `Planning phase:` patterns are no longer accepted.

## Pitfalls observed in the repo

- **Embedded-resource `LogicalName`** needs forward slashes: `<LogicalName>Bundles/property-management.bundle.json</LogicalName>`. MSBuild's `%(RecursiveDir)` yields backslashes on Windows; set `LogicalName` explicitly.

## Never

- Never rename a tier prefix casually (`blocks-*` → `domain-*` is breaking and needs an ADR).
- Never introduce a singular `block-leases` — `blocks-*` is established.
- Never use generic `Helper` / `Utility` / `Common` suffixes on new packages. Cross-cutting concerns get their own Foundation package.
- Never use underscores in package-folder or slnx segment names.
- Never ship a public type without a one-line XML doc comment (CS1591 enforcement).

## Cross-references

- [architecture-principles.md](architecture-principles.md) — the layering these names reflect.
- [package-conventions.md](../engineering/package-conventions.md) — csproj patterns that use these names.
- [coding-standards.md §Identifier conventions](../engineering/coding-standards.md#identifier-conventions) — intra-file C# casing and suffix rules.
- [commit-conventions.md](../engineering/commit-conventions.md) — commit-message format.
- [ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md), [ADR 0013](../../docs/adrs/0013-foundation-integrations.md), [ADR 0016](../../docs/adrs/0016-app-and-accelerator-naming.md).
