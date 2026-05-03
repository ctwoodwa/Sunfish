# sunfish-api-change Routing

How to navigate the default ICM stages for a breaking API change.

## Entry Heuristics

You should use sunfish-api-change if the request includes:
- "Change the API of X" (rename, reorder, remove, change types)
- "Make X breaking" (deprecation → removal)
- "Refactor the ui-core contract"
- "Update the type system in foundation"
- Request explicitly mentions breaking changes or incompatibility

## Stage Navigation

### 00_intake → 01_discovery (required)
**Focus:** Clearly articulate what is breaking and why.

**Questions to answer:**
- What API is changing?
- Why does it need to change?
- What is the old API, and what is the new API?
- Which packages and consumers are affected?
- Are both adapters (Blazor, React) affected?

**Exit:** Intake note clearly identifying breaking changes, affected packages, and rationale.

### 01_discovery → 02_architecture (required)
**Focus:** Deep analysis of impact and dependencies.

**Key work:**
- Find ALL packages and code that depend on the changing API
- Identify all consumers (blocks, apps, tooling)
- Assess impact on both adapters (Blazor and React)
- Evaluate compat-telerik impact
- Understand version cascade (if foundation changes, all downstream packages affected)

**Questions to answer:**
- How many packages/consumers are affected?
- Will all adapters need changes?
- Can adapters migrate independently or must they coordinate?
- Does compat-telerik need updates, or will it be incompatible?
- What is the estimated effort to migrate all consumers?

**Exit:** Discovery report with complete reverse-dependency analysis.

### 02_architecture → 03_package-design (required)
**Focus:** Design the new API; document migration path.

**Key work:**
- Define old API (document it clearly for reference)
- Define new API (type signatures, behavior, contracts)
- Explain rationale for the change
- Document migration steps (how consumers upgrade)
- Plan version bumping (MAJOR for breaking changes)
- Assess timeline for removal of old API

**Questions to answer:**
- Can old and new APIs coexist (for deprecation period)?
- If so, how long before old API is removed?
- What tooling or scripts can help with migration?
- Are there compiler/linter tricks to warn about old API use?

**Exit:** Architecture decision documenting old API, new API, migration path, timeline.

### 03_package-design → 04_scaffolding or 05_implementation-plan (decision)
**Question:** Do templates/generators need to use the new API?

- **Yes** → **04_scaffolding**
- **No** → **skip 04, go to 05_implementation-plan**

### 04_scaffolding (optional) → 05_implementation-plan
**Focus:** Update generators to use new API.

**Key work:**
- Update templates to generate new API (not old API)
- Test: generate sample apps with new API
- Verify generated apps build and run

**Exit:** Scaffolding plan with template updates.

### 05_implementation-plan → 06_build (required)
**Focus:** Coordinate implementation across all affected packages.

**Key work:**
- Order tasks so new API is implemented first
- Then migrate each dependent package
- Plan coordinated build and test
- Include "migration tests" (verify old code can upgrade)
- Coordinate adapter work (Blazor and React must maintain parity)

**Tasks often look like:**
1. Implement new API in foundation/ui-core
2. Update Blazor adapter to new API
3. Update React adapter to new API
4. Migrate blocks-* to new API
5. Migrate apps (kitchen-sink, docs) to new API
6. (Optional) Keep old API for deprecation period
7. (Optional) Create migration tool/script
8. Write migration tests
9. Write regression tests
10. Update all docs and examples to new API only

**Exit:** Implementation plan with coordinated task list.

### 06_build → 07_review (required)
**Focus:** Implement new API; migrate all consumers.

**Key work:**
- Implement new API in framework-agnostic packages
- Update Blazor adapter
- Update React adapter
- Migrate all affected blocks and packages
- Write unit tests for new API
- Write migration tests (old → new)
- Write regression tests (nothing else breaks)
- Remove old API examples from docs
- Update all JSDoc to reflect new API

**Important:** Do NOT keep old API examples in docs (confusing). Either remove them or clearly mark as "deprecated, do not use."

**Exit:** Implementation summary with new API implemented, all consumers migrated, all tests passing.

### 07_review → 08_release or return to 06_build (decision)
**Focus:** Completeness review and migration path verification.

**Review checklist:**
- [ ] New API is better/cleaner than old
- [ ] All affected packages updated (not just some)
- [ ] Both adapters support new API (parity maintained)
- [ ] Migration path is clear and documented
- [ ] Migration tests pass (old code can be upgraded)
- [ ] Regression tests pass (nothing else broken)
- [ ] Docs and examples updated (new API only, no old API examples)
- [ ] compat-telerik impact assessed (compatible or approved incompatible)
- [ ] Version bump is MAJOR (semantic versioning rule)

**Decision:**
- **APPROVED** → proceed to 08_release
- **APPROVED WITH CONDITIONS** → proceed to 08_release with noted conditions
- **BLOCKED** → return to 06_build to address blockers, then return to 07_review

**Exit:** Review approval with migration path sign-off.

### 08_release (required)
**Focus:** MAJOR version release with clear migration guidance.

**Key work:**
- Determine version number: MAJOR (required for breaking changes)
- Write changelog with:
  - List of breaking changes
  - Rationale for each
  - Step-by-step migration guide with before/after code examples
  - Timeline for old API removal (if applicable)
- Update all package versions for affected packages
- Build and publish packages
- Create release tag
- Publish release notes with migration guide prominently featured
- Prepare blog post or announcement explaining migration
- Monitor for migration questions and issues

**Exit:** Release checklist and published packages.

## Acceleration Points

**Can skip stage 04 (scaffolding)?**
- Yes, if templates don't reference the changed API
- Document in 05_implementation-plan why 04 was skipped

**Can combine stages 02 + 03?**
- Not recommended (design and API design benefit from separate reviews)
- But if time-constrained, write combined architecture + package-design
- Ensure it covers both strategic decisions AND per-package API changes

**Can fast-track from stage 00 to 03?**
- Only if you have a very clear picture of what's breaking and why
- Not recommended (discovery of reverse dependencies is crucial)
- Skip at your peril!

## When This Pipeline Ends

sunfish-api-change ends after 08_release when:
- MAJOR version packages published
- Migration guide published
- Community notified of breaking change
- Support plan in place for migration questions

Next step: monitor migration progress and provide support.
