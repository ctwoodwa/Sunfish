# sunfish-api-change Pipeline

**Purpose:** Manage breaking changes, public API updates, and contract modifications across framework-agnostic
and adapter implementations.

## When to Use This Pipeline

Use this pipeline variant when the request involves:
- Breaking changes to existing APIs
- Changes to public contracts in foundation or ui-core
- Changes to adapter interfaces or implementations that affect compatibility
- Type system changes that impact consumers
- Deprecation/removal of existing APIs
- Changes that require a MAJOR version bump

**Do NOT use this pipeline for:**
- New features without breaking (→ sunfish-feature-change)
- Test work (→ sunfish-test-expansion)
- Documentation only (→ sunfish-docs-change)
- Generator changes (→ sunfish-scaffolding)

## Affected Sunfish Areas

Breaking changes typically affect:
- foundation (if types change)
- ui-core (if contracts change)
- ui-adapters-blazor (must update to match new contracts)
- ui-adapters-react (must update to match new contracts)
- blocks-* (must adapt to new APIs)
- compat-telerik (must update mappings or document incompatibility)
- All downstream consumers

## Key Responsibilities

When making breaking changes, you must:

1. **Identify all breaking changes clearly** — don't hide them
2. **Provide migration path** — tell consumers how to upgrade
3. **Update all affected packages** — breaking changes cascade
4. **Maintain adapter parity** — both Blazor and React must support the new API
5. **Document impact** — changelog must be clear about what breaks
6. **Consider deprecation period** — if reasonable, allow time for migration

## Typical Deliverables

| Stage | Key Deliverable |
|---|---|
| 00_intake | Intake note clearly identifying what is breaking and why |
| 01_discovery | Discovery report identifying all reverse dependencies |
| 02_architecture | Architecture decision with migration path documented |
| 03_package-design | Package design showing old vs. new API (with examples) |
| 04_scaffolding | Scaffolding updates to templates (if needed) or SKIP |
| 05_implementation-plan | Implementation plan with adapter coordination |
| 06_build | Code updates in all affected packages, migration tests |
| 07_review | Review approval with migration path sign-off |
| 08_release | Release checklist with MAJOR version bump and migration guide |

## Common Pitfalls

1. **Silent breaking changes**
   - Breaking changes discovered late in the process
   - **Fix:** Identify and document all breaking changes in stage 00-01

2. **One adapter updated, one not**
   - Blazor supports new API, React still uses old API (or vice versa)
   - **Fix:** Update both adapters in parallel; write parity tests

3. **Missing migration guidance**
   - Breaking change shipped, users don't know how to upgrade
   - **Fix:** Provide before/after code examples in changelog and migration guide

4. **Underestimated scope**
   - Change impacts many more packages/blocks than anticipated
   - **Fix:** Deep discovery in stage 01; identify all reverse dependencies

5. **compat-telerik breaks unexpectedly**
   - Breaking change compatible with Sunfish but breaks compat-telerik
   - **Fix:** Assess compat-telerik impact in 02_architecture; don't wait until 07_review

## How This Pipeline Influences Stages

### Stage 00: Intake
- **Explicitly state what is breaking** (don't bury it)
- Explain why the breaking change is necessary
- Estimate scope and complexity

### Stage 01: Discovery
- Find ALL reverse dependencies (which packages, which blocks, which consumers use this API?)
- Understand impact on each downstream package
- Identify affected adapters

### Stage 02: Architecture
- Document old API (before change)
- Document new API (after change)
- Explain why the change was necessary
- Outline migration path step-by-step
- Assess timeline for removal of old API (if phased deprecation)

### Stage 03: Package-Design
- Show old vs. new API side-by-side with examples
- Document migration for each affected package
- Update all affected package APIs

### Stage 04: Scaffolding (Optional)
- Include if templates need to use new API
- Skip if templates don't reference the changed API

### Stage 05: Implementation-Plan
- Identify all packages that must be updated
- Order tasks so new API is implemented first, then dependent packages migrate
- Include "migration tests" (verify old code can be migrated to new)
- Plan coordinated release of all affected packages

### Stage 06: Build
- Implement new API (framework-agnostic contracts first)
- Update all adapters to support new API
- Update all blocks/packages that use changed API
- Write unit tests for new API
- Write migration tests (old code correctly migrates to new)
- Write regression tests (nothing else breaks)
- Update all docs and examples to use new API (remove old API examples)

### Stage 07: Review
- API review: is new API cleaner/better than old?
- Completeness review: are all affected packages updated?
- Migration review: is migration path clear and testable?
- compat-telerik review: is it compatible, or is incompatibility approved?
- Release review: is version bump (MAJOR) appropriate?

### Stage 08: Release
- Version bump: MAJOR (breaking change = semantic versioning MAJOR)
- Changelog: clearly list breaking changes, reasons, migration steps
- Migration guide: step-by-step with before/after code examples
- Timeline: when will old API be removed (if applicable)
- Post-release: monitor for migration issues, provide support
