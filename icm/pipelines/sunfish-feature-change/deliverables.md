# sunfish-feature-change Deliverables

Standard outputs expected at each stage for a feature request.

## Stage 00: Intake

**Artifact:** `00_intake/output/intake-note.md`

**Minimum acceptable content:**
- [ ] Feature description (1-2 sentences)
- [ ] Problem statement (why this feature matters)
- [ ] Affected Sunfish packages (foundation, ui-core, blocks, adapters, apps, tooling)
- [ ] Estimate scope (small, medium, large)
- [ ] Adapter parity (both Blazor and React, or one only?)
- [ ] Timeline/constraints

**Example naming:** `intake-feature-date-picker-2025-04-16.md`

---

## Stage 01: Discovery

**Artifact:** `01_discovery/output/discovery-report.md`

**Minimum acceptable content:**
- [ ] Feature scope and impact summary
- [ ] Affected packages analysis (foundation, ui-core, blocks, adapters, apps, tooling)
- [ ] Dependencies (internal Sunfish, external libraries)
- [ ] Existing similar features in Sunfish (reuse patterns?)
- [ ] Parity assessment (same behavior in Blazor and React, or different?)
- [ ] compat-telerik compatibility assessment (can it be mapped to Telerik?)
- [ ] Recommended design approach for next stage

**Example naming:** `01-discovery-report-date-picker-2025-04-16.md`

---

## Stage 02: Architecture

**Artifact:** `02_architecture/output/architecture-decision.md`

**Minimum acceptable content:**
- [ ] Feature contract (what the user sees and interacts with)
- [ ] Framework-agnostic design (foundation types, ui-core contracts)
- [ ] Adapter-specific implementation strategies (Blazor and React)
- [ ] Any parity differences and justification
- [ ] compat-telerik compatibility or documented gap
- [ ] Migration path (if breaking changes)
- [ ] Rationale for design choices

**Optional:**
- Type sketches or component contract pseudocode (for clarity)
- Diagram of how feature fits into Sunfish architecture

**Example naming:** `02-architecture-decision-date-picker-2025-04-16.md`

---

## Stage 03: Package-Design

**Artifact:** `03_package-design/output/package-design-note.md`

**Minimum acceptable content:**
- [ ] **foundation:** New types being added (with full definitions)
- [ ] **ui-core:** New component contracts being added (props, events, behavior)
- [ ] **ui-adapters-blazor:** Implementation strategy (how Blazor adapter will implement contracts)
- [ ] **ui-adapters-react:** Implementation strategy (how React adapter will implement contracts)
- [ ] **blocks-*:** If relevant, which blocks use the new feature
- [ ] **compat-telerik:** Mapping of new ui-core to Telerik (or documented gap)
- [ ] Migration path for existing code (if breaking)

**Format for type definitions:**
```typescript
/**
 * Configuration for a date picker field.
 */
export interface DatePickerFieldConfig {
  id: string;              // Unique identifier
  label: string;           // Display label
  required?: boolean;      // Whether selection is required
  defaultValue?: Date;     // Initial date
  minDate?: Date;          // Earliest selectable date
  maxDate?: Date;          // Latest selectable date
  onChange?: (date: Date) => void; // Change callback
}
```

**Example naming:** `03-package-design-note-date-picker-2025-04-16.md`

---

## Stage 04: Scaffolding (Optional)

**Artifact:** `04_scaffolding/output/scaffolding-plan.md` (if applicable)

**Minimum acceptable content:**
- [ ] Which generators/templates need to change
- [ ] What code will be generated (files, structure, examples)
- [ ] New generator/template design
- [ ] Test plan (generate sample app, verify it builds and runs)

**Skip 04 if:**
- No generator/template changes needed
- Existing templates sufficient for the feature

**Example naming:** `04-scaffolding-plan-date-picker-template-2025-04-16.md`

---

## Stage 05: Implementation-Plan

**Artifact:** `05_implementation-plan/output/implementation-plan.md`

**Minimum acceptable content:**
- [ ] Overview of feature being implemented
- [ ] Ordered list of tasks with owners and acceptance criteria
- [ ] Code locations (which files, which packages)
- [ ] Dependencies and critical path
- [ ] Test strategy (unit, integration, parity, regression)
- [ ] Documentation plan (JSDoc, kitchen-sink, docs, changelog)

**Task checklist example:**
```
1. Define DatePickerFieldConfig in foundation
   Owner: [name]
   Acceptance: Type defined, exported, JSDoc complete

2. Define DatePickerField component contract in ui-core
   Owner: [name]
   Acceptance: Interface defined, all adapters can implement

3. Implement DatePickerField in Blazor adapter
   Owner: [name]
   Acceptance: Component renders, selection works, unit tests pass

4. Implement DatePickerField in React adapter
   Owner: [name]
   Acceptance: Component renders, selection works, unit tests pass

5. Add DatePickerField to kitchen-sink demo
   Owner: [name]
   Acceptance: Demo shows picker, examples work in Blazor and React

6. Add API documentation to apps/docs
   Owner: [name]
   Acceptance: DatePickerField documented, examples included

7. Write parity tests
   Owner: [name]
   Acceptance: Blazor and React have equivalent behavior, tests pass

8. Write regression tests
   Owner: [name]
   Acceptance: Existing tests still pass, new tests pass
```

**Example naming:** `05-implementation-plan-date-picker-2025-04-16.md`

---

## Stage 06: Build

**Artifacts:**
- Code changes in packages/, apps/, tooling/
- `06_build/output/implementation-summary.md`

**Code checklist:**
- [ ] foundation types defined and exported
- [ ] ui-core component contracts defined
- [ ] Blazor adapter implementation complete
- [ ] React adapter implementation complete
- [ ] All unit tests pass (80%+ coverage)
- [ ] Parity tests pass (Blazor == React behavior)
- [ ] Integration tests pass
- [ ] Regression tests pass
- [ ] No ESLint/compiler warnings
- [ ] JSDoc/XML comments complete on public APIs
- [ ] kitchen-sink demo added and working
- [ ] apps/docs updated with API docs and examples
- [ ] Changelog entry draft ready

**Implementation summary minimum:**
- [ ] Summary of changes (what was built)
- [ ] Code changes by package (which files were added/modified)
- [ ] Tests added (count by type: unit, integration, parity, regression)
- [ ] Documentation updates (JSDoc, kitchen-sink, docs, changelog)
- [ ] Issues or deviations from plan (if any)
- [ ] Ready for review? (yes/no with any concerns)

**Example naming:** `06-implementation-summary-date-picker-2025-04-16.md`

---

## Stage 07: Review

**Artifact:** `07_review/output/review-approval.md`

**Review checkpoints:**
- [ ] Code quality (follows conventions, no warnings)
- [ ] API design (clean, consistent, framework-agnostic first)
- [ ] Adapter parity (Blazor and React equivalent functionality)
- [ ] compat-telerik compatibility (compatible or approved incompatible)
- [ ] Test coverage (80%+ for new code)
- [ ] Documentation completeness (JSDoc, kitchen-sink, docs)
- [ ] Release readiness (no blockers)

**Sign-off section:**
```
## Approval Status

- [x] Code Reviewer: [name] — [date]
- [x] API Reviewer: [name] — [date]
- [x] Adapter Reviewer (Blazor): [name] — [date]
- [x] Adapter Reviewer (React): [name] — [date]
- [x] Test Reviewer: [name] — [date]
- [x] Docs Reviewer: [name] — [date]

**Status:** APPROVED (or APPROVED WITH CONDITIONS, or BLOCKED)
```

**Example naming:** `07-review-approval-date-picker-2025-04-16.md`

---

## Stage 08: Release

**Artifact:** `08_release/output/release-checklist.md`

**Pre-release checklist:**
- [ ] Version number determined (MAJOR, MINOR, or PATCH)
- [ ] Changelog written with feature description and usage examples
- [ ] Package versions updated
- [ ] Docs and examples polished
- [ ] Final test run successful

**Release checklist:**
- [ ] Packages built without errors
- [ ] Packages published to registry
- [ ] Release tag created
- [ ] Release notes posted to GitHub

**Post-release checklist:**
- [ ] kitchen-sink demo verified and updated
- [ ] apps/docs examples verified and updated
- [ ] Release announcement sent to community
- [ ] Feedback monitoring plan in place

**Example naming:** `08-release-checklist-date-picker-v1.6.0-2025-04-16.md`

---

## Naming Convention for All Artifacts

Use this pattern for consistency:
```
[stage]-[artifact-type]-[brief-name]-[date].md
```

Examples:
- `00-intake-feature-date-picker-2025-04-16.md`
- `01-discovery-report-date-picker-2025-04-16.md`
- `02-architecture-decision-date-picker-2025-04-16.md`
- `03-package-design-note-date-picker-2025-04-16.md`
- `04-scaffolding-plan-date-picker-template-2025-04-16.md`
- `05-implementation-plan-date-picker-2025-04-16.md`
- `06-implementation-summary-date-picker-2025-04-16.md`
- `07-review-approval-date-picker-2025-04-16.md`
- `08-release-checklist-date-picker-v1.6.0-2025-04-16.md`

## Quality Gates Before Advancing

**Before entering 01_discovery:**
- [ ] Intake note is clear (reviewers understand the feature)

**Before entering 02_architecture:**
- [ ] Discovery report identifies key design decisions to make

**Before entering 03_package-design:**
- [ ] Architecture decision has clear contracts for implementation

**Before entering 05_implementation-plan:**
- [ ] Package design covers all affected packages

**Before entering 06_build:**
- [ ] Implementation plan has clear tasks and acceptance criteria

**Before entering 07_review:**
- [ ] All code complete, all tests passing, docs updated

**Before entering 08_release:**
- [ ] All review approvals obtained, no blocking issues

**Before completing release:**
- [ ] Packages published, release tag created, post-release docs updated

---

## If Deliverable is Missing or Incomplete

If a stage's deliverable is missing or incomplete:
1. **Identify which deliverable is missing** (reference this document)
2. **Return to the stage** that should have produced it
3. **Complete the deliverable**
4. **Re-review if necessary**
5. **Return to where you were**

This is normal and expected; it's better to catch gaps early.
