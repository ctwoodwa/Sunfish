# sunfish-feature-change Routing

How to navigate the default ICM stages for a feature request.

## Entry Heuristics

You should use sunfish-feature-change if the request includes:
- "Add a new [block/component]"
- "Implement support for [capability]"
- "Create a demo or example for [feature]"
- "Extend [block] to support [new behavior]"
- Request affects user-facing behavior (not just internal refactoring)

## Stage Navigation

### 00_intake → 01_discovery (required)
**Focus:** Clearly describe the feature; identify which Sunfish packages must change.

**Questions to answer:**
- What is the feature, in user terms?
- Which Sunfish packages (foundation, ui-core, blocks, adapters) are affected?
- Will both Blazor and React need this, or just one?
- Is compat-telerik involved?
- Does this change existing APIs or only add new ones?

**Exit:** Intake note with feature description and affected areas identified.

### 01_discovery → 02_architecture (required)
**Focus:** Research the impact; understand dependencies and parity concerns.

**Questions to answer:**
- Have similar features been built in Sunfish before? How?
- What are the dependencies? (foundation → ui-core → adapters → blocks)
- Can both adapters implement this equally, or will there be differences?
- Does compat-telerik support similar functionality? Can we map to it?
- Are there breaking changes needed?

**Exit:** Discovery report with dependencies, parity assessment, and recommended design approach.

### 02_architecture → 03_package-design (required)
**Focus:** Design the feature; define contracts and APIs.

**Key work:**
- Define the feature contract in foundation (types) and ui-core (components)
- Plan how Blazor and React adapters will implement it
- Document any parity differences and why
- Assess compat-telerik compatibility
- Identify any breaking changes (may escalate to sunfish-api-change)

**Exit:** Architecture decision with clear contracts and adapter implementation strategies.

### 03_package-design → 04_scaffolding or 05_implementation-plan (decision)
**Question:** Does this feature require scaffolding/generator changes?

- **Yes** (new block or feature that templates should generate) → **04_scaffolding**
- **No** (existing templates sufficient) → **skip 04, go to 05_implementation-plan**

### 04_scaffolding (optional) → 05_implementation-plan
**Focus:** Design any generator/template work needed for the feature.

**Key work:**
- Identify which generators/templates need changes
- Design the generated code (files, structure, examples)
- Plan testing (generate sample app, verify it builds and runs)

**Exit:** Scaffolding plan with template design and test strategy.

### 05_implementation-plan → 06_build (required)
**Focus:** Create task list for implementation.

**Key work:**
- Order tasks so foundation/ui-core work happens first
- Plan Blazor and React adapter work in parallel
- Include kitchen-sink demo as a task
- Include docs updates as a task
- Define acceptance criteria for each task

**Tasks often look like:**
1. Define new types in foundation
2. Define new component contract in ui-core
3. Implement component in Blazor adapter
4. Implement component in React adapter
5. Create kitchen-sink demo (both Blazor and React)
6. Add API docs to apps/docs
7. Write unit tests for new code
8. Write parity tests (Blazor vs. React)
9. Write integration tests
10. Update block composition (if adding to blocks-*)

**Exit:** Implementation plan with ordered tasks and clear acceptance criteria.

### 06_build → 07_review (required)
**Focus:** Code implementation in packages/, apps/, tooling/.

**Key work:**
- Implement foundation types (if needed)
- Implement ui-core contracts (if needed)
- Implement Blazor adapter
- Implement React adapter
- Write unit tests (80%+ coverage target)
- Write parity tests (Blazor vs. React equivalence)
- Write integration tests
- Add kitchen-sink demo
- Update apps/docs
- Write JSDoc/XML comments on all public APIs

**Done when:**
- All code complete
- All tests passing
- Kitchen-sink demo working
- Docs updated
- No ESLint/analyzer warnings

**Exit:** Implementation summary listing all changes, tests added, docs updated.

### 07_review → 08_release or return to 06_build (decision)
**Focus:** Quality gates and approvals.

**Review checklist:**
- [ ] Code quality acceptable (no warnings, follows conventions)
- [ ] API design approved (clear, consistent, framework-agnostic first)
- [ ] Parity verified (Blazor and React behavior equivalent)
- [ ] compat-telerik compatibility assessed (compatible, incompatible but approved, or needs changes)
- [ ] Test coverage meets threshold (80%+ for new code)
- [ ] Documentation complete (JSDoc, kitchen-sink, docs)
- [ ] Release readiness confirmed (no blocking issues)

**Decision:**
- **APPROVED** → proceed to 08_release
- **APPROVED WITH CONDITIONS** → proceed to 08_release with noted conditions
- **BLOCKED** → return to 06_build to address blockers, then return to 07_review

**Exit:** Review approval with sign-offs.

### 08_release (required)
**Focus:** Publish the feature; update post-release docs.

**Key work:**
- Determine version number (MINOR for new feature, MAJOR if breaking changes)
- Write changelog (user-focused description of new feature)
- Update package versions
- Build and publish packages
- Create release tag
- Update kitchen-sink and docs with final polish
- Announce the feature

**Exit:** Release checklist and published packages.

## Acceleration Points

**Can skip stage 04 (scaffolding)?**
- Yes, if no generator/template changes needed
- Document in 05_implementation-plan why 04 was skipped

**Can fast-track stage 01 (discovery)?**
- Yes, if you have a recent discovery document from a related feature
- Reference the prior document in 00_intake
- Update it with any new scope changes

**Can combine stages 02 + 03?**
- Not recommended (design and package API design benefit from separate reviews)
- But if time-constrained, you can write combined architecture + package-design document
- Ensure it covers both architectural decisions AND per-package APIs

## When This Pipeline Ends

sunfish-feature-change ends after 08_release when:
- Packages are published
- Release notes are posted
- kitchen-sink and docs are updated
- Feature is ready for users

Next step: monitor feedback and plan follow-up releases if needed.
