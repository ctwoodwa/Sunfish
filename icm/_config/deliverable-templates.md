# Deliverable Templates and Artifact Standards

This document defines the standard names, formats, and minimal structure for artifacts at each stage.

## Naming Conventions

All artifact files should follow this pattern:
```
[stage]-[artifact-type]-[brief-name]-[date].md
```

Example:
```
01-discovery-report-form-blocks-2025-04-16.md
03-package-design-react-adapter-2025-04-16.md
```

For minimal/informal artifacts, short names are acceptable:
```
intake-note.md
architecture-decision.md
```

---

## Stage 00: Intake

### Artifact: Intake Note
**File:** `00_intake/output/intake-note.md`

**Minimal template:**
```markdown
# Intake Note

**Date:** [date]
**Requestor:** [name]
**Request:** [one-line summary]

## Problem Statement
[2-3 sentences describing what needs to happen and why]

## Affected Areas
- foundation / ui-core / ui-adapters-blazor / ui-adapters-react / compat-telerik / blocks-* / apps-* / tooling-*
[List which Sunfish packages and apps are involved]

## Selected Pipeline Variant
- [ ] sunfish-feature-change
- [ ] sunfish-api-change
- [ ] sunfish-scaffolding
- [ ] sunfish-docs-change
- [ ] sunfish-quality-control
- [ ] sunfish-test-expansion
- [ ] sunfish-gap-analysis

## Dependencies and Constraints
[Any known blockers, time constraints, or sequencing concerns]

## Next Steps
Proceed to 01_discovery
```

---

## Stage 01: Discovery

### Artifact: Discovery Report
**File:** `01_discovery/output/discovery-report.md`

**Minimal template:**
```markdown
# Discovery Report

**Date:** [date]
**Request:** [reference to intake note]
**Scope:** [1-line scope statement]

## Affected Sunfish Packages
- foundation: [impact description or "not affected"]
- ui-core: [impact description or "not affected"]
- ui-adapters-blazor: [impact description or "not affected"]
- ui-adapters-react: [impact description or "not affected"]
- compat-telerik: [impact description or "not affected"]
- blocks-*: [which blocks, impact]
- apps/docs: [impact or "not affected"]
- apps/kitchen-sink: [impact or "not affected"]
- tooling/scaffolding-cli: [impact or "not affected"]

## Dependencies
[List any internal Sunfish dependencies, external libraries, or version constraints]

## Constraints and Risks
- [constraint 1]
- [constraint 2]
[Note any blockers, timeline, or resource constraints]

## Existing Approaches in Sunfish
[If similar work exists, reference it and explain how this differs]

## Recommended Next Steps
Proceed to 02_architecture with focus on [key design decisions]
```

---

## Stage 02: Architecture

### Artifact: Architecture Decision (ADR format)
**File:** `02_architecture/output/architecture-decision.md`

**Minimal template:**
```markdown
# Architecture Decision: [Decision Title]

**Date:** [date]
**Context:** [What discovery revealed]
**Problem:** [What needs to be solved]

## Decision
[Clear statement of the decision]

## Rationale
[Why this approach over alternatives]

## Implications
- **framework-agnostic (foundation/ui-core):** [impact on core contracts]
- **Blazor adapter:** [Blazor-specific implications]
- **React adapter:** [React-specific implications]
- **compat-telerik:** [compatibility implications]
- **other blocks:** [dependencies or side effects]

## Breaking Changes
[If any: list them clearly, include migration path]

## Next Steps
Proceed to 03_package-design with focus on [key APIs to design]
```

### Artifact: Type/Contract Sketches (optional but recommended)
**File:** `02_architecture/output/contract-sketch-[name].md` or `.ts/.tsx/.cs` code samples

Keep these lightweight. Use TypeScript/C# pseudocode if helpful.

---

## Stage 03: Package-Design

### Artifact: Package Design Note
**File:** `03_package-design/output/package-design-note.md`

**Minimal template:**
```markdown
# Package Design Note

**Date:** [date]
**Scope:** [Which packages are being designed]

## foundation
### New or Changed Types
- Type A: [description, signature]
- Type B: [description, signature]

### New or Changed Exports
- export A: [description, purpose]

### Deprecations
[If any]

## ui-core
### New or Changed Component/Contract
- Component X: [props, purpose]
- Interface Y: [description]

### Adapter Requirements
[How must adapters implement this?]

## ui-adapters-blazor
### Implementation Strategy
[How Blazor adapter will implement ui-core contracts]

### Constraints or Deviations
[If Blazor cannot match React, document why]

## ui-adapters-react
### Implementation Strategy
[How React adapter will implement ui-core contracts]

### Constraints or Deviations
[If React cannot match Blazor, document why]

## compat-telerik
### Compatibility Assessment
- [ ] No compat-telerik changes needed
- [ ] compat-telerik mapping document needed (see below)
- [ ] compat-telerik is incompatible with this change (approved)

### Compat-Telerik Mapping (if needed)
[Map new ui-core contracts to Telerik equivalents, or explain gaps]

## blocks-*
[For each affected block: API surface, type changes, new exports]

## Migration Path for Existing Code
[If breaking: step-by-step guide for consumers to upgrade]

## Next Steps
Proceed to 04_scaffolding or 05_implementation-plan (skip 04 if no generator changes)
```

---

## Stage 04: Scaffolding (if applicable)

### Artifact: Scaffolding Plan
**File:** `04_scaffolding/output/scaffolding-plan.md`

**Minimal template:**
```markdown
# Scaffolding Plan

**Date:** [date]
**Scope:** [Generator/template changes]

## Templates or Generators to Change
- [Template/Generator name]: [what changes]
- [Template/Generator name]: [what changes]

## New Templates or Generators
- [Name]: [purpose, when to use]

## Test Strategy for Scaffolding
- [ ] Generate sample app with new template
- [ ] Run app (build, start, verify it works)
- [ ] Check generated code quality against standards
- [ ] Test on both Blazor and React targets (if applicable)

## Implementation Details
[Code generation logic, template format, plugin points, etc.]

## Next Steps
Proceed to 05_implementation-plan
```

---

## Stage 05: Implementation-Plan

### Artifact: Implementation Plan
**File:** `05_implementation-plan/output/implementation-plan.md`

**Minimal template:**
```markdown
# Implementation Plan

**Date:** [date]
**Scope:** [Feature/change scope]
**Owner:** [Name/team]
**Timeline:** [Expected completion]

## Overview
[Summary of what will be implemented, referencing prior stage outputs]

## Implementation Tasks
1. [Task name] — Owner: [name] — Acceptance: [what done looks like]
2. [Task name] — Owner: [name] — Acceptance: [what done looks like]
3. ...

## Code Locations
- [Package/app/tooling path]: [what goes here]
- [Package/app/tooling path]: [what goes here]

## Dependencies
- [Internal Sunfish dependency]
- [External library]
- [Prior task in this plan]

## Test Strategy
- Unit tests: [what will be tested]
- Integration tests: [what scenarios]
- Adapter parity tests: [Blazor vs. React coverage]
- Regression tests: [any existing scenarios that must not break]

## Documentation Updates
- [ ] Code comments / JSDoc / XML docs
- [ ] apps/docs updates [if needed]
- [ ] apps/kitchen-sink updates [if user-facing]
- [ ] tooling/scaffolding-cli docs [if applicable]

## Acceptance Criteria
- [ ] All code changes complete
- [ ] All tests passing
- [ ] No new ESLint/compiler warnings
- [ ] Docs reviewed and merged
- [ ] Kitchen-sink demo (if user-facing) working

## Next Steps
Proceed to 06_build
```

---

## Stage 06: Build

### Artifact: Implementation Summary
**File:** `06_build/output/implementation-summary.md`

**Minimal template:**
```markdown
# Implementation Summary

**Date:** [date]
**Status:** [COMPLETE / IN PROGRESS / BLOCKED]

## What Was Implemented
[Summary of changes, referencing the implementation plan]

## Code Changes
- [Package/path]: [brief description of changes]
- [Package/path]: [brief description of changes]

## Tests Added
- Unit: [count and coverage areas]
- Integration: [what scenarios covered]
- Regression: [which existing scenarios verified]
- Parity: [Blazor/React coverage]

## Documentation
- Code: [comments/docs updated in these files]
- Kitchen-sink: [demos added/updated]
- apps/docs: [docs added/updated]
- Changelog: [summary for release notes]

## Issues or Deviations from Plan
[If any tasks were skipped, changed, or discovered gaps]

## Ready for Review?
- [ ] All tests passing
- [ ] Code reviewed by [names]
- [ ] No blocking issues

## Next Steps
Proceed to 07_review
```

---

## Stage 07: Review

### Artifact: Review Approval
**File:** `07_review/output/review-approval.md`

**Minimal template:**
```markdown
# Review Approval

**Date:** [date]
**Reviewed by:** [Names/roles]

## Code Quality
- [ ] API surface reviewed and approved
- [ ] No breaking changes unaccounted for
- [ ] Framework-agnostic contracts before adapters
- [ ] Adapter parity maintained (Blazor, React)
- [ ] compat-telerik compatibility verified or documented

## Test Coverage
- [ ] Coverage threshold met [threshold: X%]
- [ ] Regression tests pass
- [ ] Parity tests pass on all adapters
- [ ] Integration tests pass

## Documentation
- [ ] Code is documented (JSDoc/XML/comments)
- [ ] User-facing changes documented in apps/docs
- [ ] Kitchen-sink demo matches feature (if applicable)
- [ ] Migration guide provided (if breaking)

## Release Readiness
- [ ] No outstanding issues
- [ ] Performance impact assessed
- [ ] Security review completed (if applicable)
- [ ] Ready to merge

## Approval Status
- [ ] APPROVED
- [ ] APPROVED WITH CONDITIONS: [list conditions]
- [ ] BLOCKED: [reason]

## Next Steps
If APPROVED: Proceed to 08_release
If BLOCKED: Return to [stage name] with feedback
```

---

## Stage 08: Release

### Artifact: Release Checklist
**File:** `08_release/output/release-checklist.md`

**Minimal template:**
```markdown
# Release Checklist

**Date:** [date]
**Release Version:** [X.Y.Z]
**Release Type:** [MAJOR / MINOR / PATCH]

## Pre-Release
- [ ] Changelog written with clear user-facing descriptions
- [ ] Version bumped in all affected packages
- [ ] Docs examples tested and working
- [ ] Kitchen-sink demo tested and working
- [ ] Migration guide finalized (if breaking)

## Release
- [ ] Tag created: [tag name]
- [ ] Packages published to [registry]
- [ ] Release notes posted
- [ ] Announcement sent (if major feature)

## Post-Release
- [ ] apps/docs updated with new feature/API docs
- [ ] Kitchen-sink examples updated
- [ ] Social/community channels notified
- [ ] Feedback monitoring plan in place

## Sign-Off
- Release Manager: [name] — [date]
- Product: [name] — [date]
- Engineering: [name] — [date]

## Next Steps
Release complete. Monitor community feedback.
```

---

## Optional: Variant-Specific Deliverables

Some pipeline variants expect additional artifacts:

### sunfish-gap-analysis
Add: `gap-analysis-note.md` (01_discovery/output)
- Gap title and description
- Impact assessment (which packages, which scenarios)
- Options to close the gap
- Recommended path forward
- Effort estimate

### sunfish-test-expansion
Add: `test-expansion-plan.md` (05_implementation-plan/output)
- Current coverage metrics (baseline)
- Target coverage metrics
- Test scenarios to add
- Parity matrix (Blazor vs. React coverage alignment)
- Timeline to full coverage

### sunfish-quality-control
Add: `audit-report.md` (07_review/output)
- Audit scope and methodology
- Findings and recommendations
- Severity levels of gaps
- Remediation roadmap
- Sign-off from relevant stakeholders

---

## Guidelines

1. **Keep it simple:** Minimal viable content is better than exhaustive documentation
2. **Use templates as guides, not law:** Adapt to your request's needs
3. **Link between artifacts:** Each stage should reference prior stage outputs
4. **Date everything:** Makes it easier to find the most recent version
5. **Review before advancing:** Each artifact is a gate; don't proceed until reviewed and approved
