# ICM Stage Map

Quick reference for all 9 stages of the Sunfish ICM pipeline.

| Stage | Name | Purpose | Reads From | Writes To | Exit Criteria | Next |
|---|---|---|---|---|---|---|
| 00 | Intake | Classify request; choose pipeline variant; frame scope | External request | output/intake-note.md | Clear problem statement, selected variant, list of affected packages | 01 |
| 01 | Discovery | Research scope, affected packages, constraints, existing approaches | intake-note.md, existing codebase | output/discovery-report.md | Known dependencies, constraint list, impact scope documented | 02 |
| 02 | Architecture | Design decisions, ADRs, framework-agnostic contracts, adapter implications | discovery-report.md, package README files | output/architecture-decision.md, ADRs | Cross-package contracts defined; adapter implications clear; breaking changes identified | 03 |
| 03 | Package-Design | Per-package API surface, types, module boundaries | architecture-decision.md | output/package-design-note.md | API signatures defined for each affected package; type system clear; compat-telerik impact assessed | 04 |
| 04 | Scaffolding | Generator/template changes if tooling affected | package-design-note.md | output/scaffolding-plan.md, scripts/ | New generators/templates designed; test strategy for scaffolding defined | 05 |
| 05 | Implementation-Plan | Ordered task list, ownership, acceptance criteria, test strategy | All prior stage outputs | output/implementation-plan.md | Detailed tasks, owners assigned, test coverage planned, dependencies clear | 06 |
| 06 | Build | Code implementation in packages/, apps/, tooling/ | implementation-plan.md | output/implementation-summary.md, code artifacts | Code complete; unit tests passing; integration tests passing; docs updated; kitchen-sink demo (if user-facing) | 07 |
| 07 | Review | Quality gates: API review, compat-telerik policy, test coverage, docs | implementation-summary.md, code artifacts | output/review-approval.md | API approved; docs complete; tests meet coverage threshold; release readiness confirmed | 08 |
| 08 | Release | Changelog, versioning, publish, post-release docs | review-approval.md, code | output/release-checklist.md, published artifacts | Version bumped; changelog written; packages published; post-release docs updated | — |

## Input/Output Folders

- **Stage input/** — Where prior stage's output/ is copied or referenced
- **Stage output/** — Where this stage leaves artifacts for the next stage

## Key Handoff Rules

1. Always review `output/` before advancing to the next stage
2. Each output artifact should be named predictably (see deliverable-templates.md)
3. If a stage is skipped or significantly abbreviated, document why in the next stage's intake notes
4. Breaking changes discovered late trigger a return to 02_architecture

## Sunfish-Specific Concerns by Stage

### Stages 00–01 (Intake & Discovery)
- Identify which Sunfish areas are affected: foundation? ui-core? adapters? blocks? tooling?
- Flag if this is adapter-specific (Blazor, React) vs. framework-agnostic
- Check if compat-telerik will be affected

### Stages 02–03 (Architecture & Package-Design)
- Framework-agnostic contracts (foundation, ui-core) take precedence
- All adapters must maintain feature parity unless explicitly approved to differ
- compat-telerik changes are policy-gated and require explicit sign-off
- Types and module boundaries must be clear across all adapters

### Stages 04–05 (Scaffolding & Implementation-Plan)
- If tooling/scaffolding-cli is affected, generator/template work belongs in stage 04
- Test strategy must cover all adapters (Blazor + React)
- If blocks are changed, kitchen-sink demo updates are mandatory deliverables

### Stages 06–07 (Build & Review)
- Real implementation in packages/, apps/, tooling/ only (not in icm/)
- All unit and integration tests must pass on all adapters
- User-facing changes must include kitchen-sink updates
- compat-telerik compatibility must be verified (or explicitly declined)
- Docs must be updated (apps/docs or inline code comments)

### Stage 08 (Release)
- Changelog must reflect all user-facing changes
- Semantic versioning: major for breaking, minor for features, patch for fixes
- If foundation or ui-core changed, all dependent packages get updated
- Post-release: kitchen-sink updates, docs updates, example updates
