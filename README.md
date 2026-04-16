# Sunfish

Sunfish is a framework‑agnostic suite of open‑source and commercial building blocks that lets you rapidly scaffold, prototype, and ship real-world applications with interchangeable UI and domain components.

## Why Sunfish?

- **Framework-agnostic**: Use the same primitives and patterns across Blazor, React, and other stacks via thin adapters.  
- **Prototype to production**: Start with open-source components for fast prototyping, then swap in commercial partners (like Telerik) or Sunfish premium modules without rewriting everything.  
- **Beyond components**: Sunfish includes domain‑level modules (forms, tasks, scheduling, asset management) so you assemble full features, not just UI widgets.  
- **Migration-friendly**: Design your app against stable abstractions so you can change underlying vendors with minimal surface‑area impact.

## High-level architecture

Sunfish is organized into layers:

- **Foundation**  
  Design tokens, utilities, and primitives shared across all packages.

- **UI Components**  
  Framework-agnostic component APIs plus adapters for specific UI frameworks.

- **Compatibility Kits**  
  Optional packages that mirror the public API shape of popular commercial libraries, so you can prototype against Sunfish and later switch vendors with minimal changes.

- **Blocks & Modules**  
  Higher-level building blocks such as dynamic forms, data grids, task boards, and schedulers that encapsulate patterns and cross-cutting logic.

- **Solution Accelerators**  
  Opinionated, ready-to-extend “starter solutions” (e.g., asset management, project operations) composed from Sunfish building blocks.

## Repository layout (planned)

```text
sunfish/
  packages/
    foundation/          # tokens, utilities, core contracts
    ui-core/             # framework-agnostic component contracts
    ui-adapters-blazor/  # Blazor adapter implementation
    ui-adapters-react/   # React adapter implementation
    compat-telerik/      # optional, API-compatible surface where permissible
    blocks-forms/        # dynamic form engine + helpers
    blocks-tasks/        # tasks, boards, status flows
    blocks-scheduling/   # calendars, resource scheduling
    blocks-assets/       # asset/catalog primitives
  apps/
    docs/                # documentation site + live examples
    kitchen-sink/        # playground for all components and blocks
  tooling/
    scaffolding-cli/     # Sunfish CLI for scaffolding apps and modules
```

> Note: This structure is aspirational and will evolve as the project matures.

## Example use cases

- Quickly prototype a line-of-business app using open-source Sunfish components, then selectively replace specific grids or charts with commercial equivalents.  
- Standardize UX across multiple applications by building against Sunfish contracts instead of directly against vendor-specific libraries.  
- Compose domain features like “asset management” or “project planning” from reusable Sunfish blocks and extend them with your own business rules.

## Status

Sunfish is currently in early design and prototyping.  
APIs, package names, and structure are subject to change until a 1.0 release is tagged.

Planned milestones:

1. Foundation + core UI abstractions  
2. First framework adapter (likely Blazor)  
3. Forms + tasks blocks  
4. Initial solution accelerator (TBD domain)

## Contributing

Contributions, ideas, and discussion are very welcome—even at this early stage.

- Open an issue to propose new building blocks, adapters, or solution accelerators.  
- Share real-world scenarios where a compatibility or abstraction layer would simplify your stack.  
- Help refine the API design so Sunfish is pleasant to use from multiple frameworks.

## License

The core Sunfish packages will be released under an open-source license, with optional commercial add-ons for advanced modules, adapters, and enterprise support.

(Exact licensing details are still being finalized and will be documented here before the first public release.)
