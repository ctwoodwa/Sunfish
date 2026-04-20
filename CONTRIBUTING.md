# Contributing to Sunfish

Thank you for your interest in contributing. Sunfish is in active early development —
contributions, ideas, and API feedback are very welcome at this stage.

## Before You Start

- Check [open issues](../../issues) and [Discussions](../../discussions) to avoid duplicate work
- For significant changes, open a Discussion or issue first to align on approach
- All changes flow through the [ICM pipeline](#icm-pipeline) — familiarize yourself with the stages

## Getting Started

**Prerequisites:** .NET 10 SDK, Git, a C# IDE (Visual Studio, Rider, or VS Code + C# Dev Kit)

```bash
git clone https://github.com/ctwoodwa/Sunfish.git
cd Sunfish
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj
```

## ICM Pipeline

Sunfish uses an **Integrated Change Management (ICM)** pipeline to stage work through deliberate phases.
See [`/icm/CONTEXT.md`](icm/CONTEXT.md) for a full overview.

All non-trivial changes should have at least a brief intake note in `/icm/00_intake/`.
Trivial fixes (typos, doc corrections) can skip ICM — note this in the PR.

## Package Architecture

Contributions must respect the dependency hierarchy:

```
foundation  (no framework deps — pure C#)
  ↓
ui-core     (framework-agnostic contracts)
  ↓
ui-adapters-blazor / ui-adapters-react
  ↓
blocks-*
  ↓
apps/  ·  accelerators/
```

**Critical rule:** Do not introduce Blazor types (`RenderFragment`, `ComponentBase`,
`ElementReference`, `IJSRuntime`) into `packages/foundation` or `packages/ui-core`.

## Coding Standards

- Target: .NET 10, C# 13, `Nullable enable`, `ImplicitUsings enable`
- XML doc comments on all public APIs
- `TreatWarningsAsErrors` is on — the build must be warning-free
- Namespace: `Sunfish.<PackageName>.<Area>` (e.g., `Sunfish.Foundation.Models`)
- See [`/_shared/engineering/coding-standards.md`](_shared/engineering/coding-standards.md)

## Adapter Parity

If you add a feature to one adapter (Blazor), the same feature must ship for all adapters
(React) unless an explicit exception is approved and documented.

## compat-telerik Policy

Changes to `packages/compat-telerik` are policy-gated. Open an issue before starting work
there — not all Telerik API shapes are appropriate to mirror.

## Pull Requests

1. Fork and create a branch from `main`
2. Write tests — the project uses xUnit and bUnit
3. Ensure `dotnet build` and `dotnet test` pass locally
4. Open a PR using the provided template
5. Link the relevant ICM stage output if applicable

## User-Facing Changes

Any change visible to library consumers **requires**:
- A `kitchen-sink` demo update (`apps/kitchen-sink/`)
- A `docs` update (`apps/docs/`)
- XML doc comments on all new/changed public members
- A changelog entry describing the change from the user's perspective

## Governance

Sunfish is pre-release and currently led by a single maintainer under a BDFL model with
explicit transition triggers. See [`GOVERNANCE.md`](GOVERNANCE.md) for the decision model,
the ODF + UPF + ICM framework stack, how external contributors participate, and the triggers
under which governance evolves (maintainer tier, steering committee, foundation membership).

Contributions are made under the project's [MIT License](LICENSE). Sign commits with
`git commit --signoff` (DCO) — no CLA is required.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).
