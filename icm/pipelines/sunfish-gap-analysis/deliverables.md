# sunfish-gap-analysis Deliverables

## Core Artifact

**Stage 01 (Discovery):** `01_discovery/output/gap-analysis-note.md`

**Minimum acceptable content:**
- [ ] Gap description (what's missing or inconsistent)
- [ ] Impact assessment (which users/scenarios affected, severity)
- [ ] Root cause analysis (why does gap exist?)
- [ ] Options to close gap (with pros/cons for each)
- [ ] Recommended path (which option to pursue)
- [ ] Effort estimate

**Example:**
```markdown
# Gap Analysis: Form Validation in React Adapter

## Gap Description
Form field validation using async validators works in Blazor adapter but not in React adapter.
Users can't implement custom async validation in React (e.g., checking email uniqueness).

## Impact Assessment
- Affected users: React users who need async validation
- Severity: MAJOR (breaks workflow for async validation use cases)
- Workarounds: Users must implement validation outside form field (hacky)

## Root Cause
React adapter implementation incomplete; async validation hook not written yet.

## Options

### Option 1: Implement async validation in React
- Pros: Closes gap; enables parity with Blazor
- Cons: 1-2 weeks of work
- Risk: Low (established pattern in Blazor)

### Option 2: Document limitation and provide workaround
- Pros: Quick (1 day); documents the limitation
- Cons: Doesn't close gap; users need workaround
- Risk: Low

### Option 3: Defer (don't prioritize)
- Pros: Frees up team for other work
- Cons: Gap persists; users frustrated
- Risk: Reputational

## Recommendation
Option 1: Implement async validation in React adapter.
Effort: 1-2 weeks. Priority: HIGH (user pain point).

## Timeline
- v2.2.0 (next minor release): Implement async validation in React
- Estimated: 4 weeks out
```

## Related Artifact

**Stage 02 (Architecture):** If gap is being closed, document remediation design:

`02_architecture/output/gap-closure-design.md`
- What will be built to close the gap
- How it will match the other adapter
- Any trade-offs or compromises

## Gap Closure Plan

If gap will be closed:
- Plan should feed into a new feature-change or api-change request
- Timeline and resources allocated
- Metrics for success (e.g., "async validation works in both adapters")

## Approved Gap

If gap is approved but not closing:
```markdown
# Approved Gap: compat-telerik and new Calendar block

## Gap
compat-telerik does not have a Telerik equivalent for the new calendar block.
Users using compat-telerik cannot use the calendar block.

## Impact
compat-telerik users need to use alternative calendar solutions or stick with direct Sunfish.

## Decision
This gap is APPROVED as acceptable.

## Rationale
- Telerik calendar component has different API
- Mapping would be overly complex
- compat-telerik is best-effort compatibility, not guaranteed parity

## Sign-off
- [x] Product Owner: Approved
- [x] compat-telerik Owner: Approved

## Documentation
This gap is documented in release notes for v2.1.0.
```
