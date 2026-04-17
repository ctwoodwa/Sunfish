# sunfish-scaffolding Pipeline

**Purpose:** Manage changes to generators, CLI tools, and scaffolding templates.

## When to Use

Use this pipeline when the request involves:
- Updates to tooling/scaffolding-cli (CLI commands, options)
- Changes to app templates or generators
- New generator/template for blocks or features
- Scaffolding workflow changes

## Affected Areas

- tooling/scaffolding-cli (primary)
- Generated app structures and examples
- foundation, ui-core, blocks (templates reference these)

## Key Deliverables

| Stage | Key Output |
|---|---|
| 00_intake | Scaffolding change scope |
| 01_discovery | Existing scaffolding patterns, impact on generated apps |
| 02_architecture | Generator design and template structure |
| 03_package-design | API surface of new generators/templates |
| 04_scaffolding | **Generator implementation** (this is the main stage) |
| 05_implementation-plan | Testing plan for generated apps |
| 06_build | Generator code, test apps |
| 07_review | Generated app quality, template quality |
| 08_release | Updated tooling, docs, examples |

## Typical Flow

- **Stage 04 is heavyweight** — generator/template development happens here
- **Stage 06 focuses on testing** — generate sample apps, verify they work
- **Release includes updated tooling docs** — how to use new generators
