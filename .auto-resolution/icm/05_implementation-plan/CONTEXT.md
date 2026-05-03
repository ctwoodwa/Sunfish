# Stage 05: Implementation-Plan

**Purpose:** Create a detailed, ordered task list for stage 06 build, including ownership, acceptance
criteria, and test strategy.

## Inputs

- All prior stage outputs (intake, discovery, architecture, package design, scaffolding if applicable)
- Sunfish package structures and existing code patterns

## Process

1. **Translate design to tasks**
   - Break down the architecture and package design into concrete coding tasks
   - Order tasks by dependency (some must happen before others)
   - Assign owners for each task (even if it's a self-assignment)

2. **Define code locations**
   - Where in packages/, apps/, tooling/ will changes happen?
   - Which files will be created, modified, or deleted?
   - Map each task to specific code paths

3. **Plan the test strategy**
   - What unit tests need to be written?
   - What integration tests? (how do components work together?)
   - What adapter parity tests? (Blazor and React equivalence)
   - What regression tests? (existing functionality still works)

4. **Plan documentation updates**
   - Code comments/JSDoc/XML docs for all new public APIs
   - Update kitchen-sink demo (if user-facing)
   - Update apps/docs (if user-facing)
   - Plan changelog entry (for stage 08 release)

5. **Identify dependencies and sequencing**
   - Do some tasks depend on others? (e.g., write foundation types before using them in blocks)
   - Are there external dependencies? (third-party libraries, peer team coordination)
   - Is there a critical path?

6. **Create implementation plan**
   - File: `05_implementation-plan/output/implementation-plan.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- `05_implementation-plan/output/implementation-plan.md` — ordered tasks, ownership, acceptance
  criteria, test strategy, documentation plan, dependencies

## Exit Criteria

- [ ] All major tasks identified and sequenced
- [ ] Owners assigned (even if tentative)
- [ ] Code locations mapped
- [ ] Test strategy defined (unit, integration, adapter parity, regression)
- [ ] Documentation needs identified
- [ ] Acceptance criteria are clear for each task
- [ ] Implementation plan reviewed and approved

## Next Stage

→ **06_build**

The implementation plan is the task list for stage 06. It should be detailed enough for a developer
to pick up a task and know what done looks like.

## Sunfish-Specific Considerations

### Task Ordering for Sunfish Work

1. **Foundation types first** — If foundation changes, everything downstream depends on it
2. **ui-core contracts second** — Adapters implement ui-core contracts
3. **Adapter implementations** — Blazor and React adapters (parallel work possible)
4. **Blocks and composition** — Blocks depend on adapters
5. **Apps and examples** — Apps/kitchen-sink/docs depend on everything above
6. **Tooling updates** — Scaffolding updates reference the above

Example task ordering for a new form field type:
```
1. Define FormFieldConfig type in foundation
2. Define FormField component contract in ui-core
3. Implement FormField in ui-adapters-blazor
4. Implement FormField in ui-adapters-react
5. Update blocks-forms to use new field type
6. Add kitchen-sink demo for new field type
7. Update apps/docs with new field example
8. Update tooling/scaffolding-cli templates (if needed)
9. Write parity tests (Blazor vs. React)
10. Write regression tests (existing forms still work)
```

### Test Strategy for Sunfish

- **Unit tests:** Each function/component/type should have unit tests
  - Test behavior in isolation
  - Test edge cases
  - Test error conditions
- **Integration tests:** Components work together
  - Foundation types work with ui-core components
  - ui-core contracts work with adapters
  - Blocks compose adapters and foundation correctly
- **Adapter parity tests:** Blazor and React behave the same
  - Same props produce the same output
  - Same user interactions produce the same events
  - Same styling applies
- **Regression tests:** Existing functionality unaffected
  - Existing forms still render
  - Existing blocks still function
  - Existing apps still work

### Documentation Tasks

Every new public API should have:
- JSDoc (TypeScript/JavaScript) or XML docs (C#) on the definition
- Changelog entry (for release notes)
- If user-facing: example in kitchen-sink and/or apps/docs
- If using pattern: reference to where the pattern is documented

### Acceptance Criteria Examples

**Task:** "Implement FormField component in React adapter"
**Acceptance criteria:**
- [ ] Component accepts FormFieldConfig from ui-core
- [ ] Component renders input with correct type
- [ ] Component fires onChange events correctly
- [ ] Component applies validation and shows errors
- [ ] Component has JSDoc comments
- [ ] Unit tests pass (80%+ coverage)
- [ ] Parity test with Blazor adapter passes
- [ ] No TypeScript/ESLint errors
- [ ] Renders correctly in kitchen-sink demo

**Task:** "Update blocks-forms to support new field types"
**Acceptance criteria:**
- [ ] FormBuilder can construct forms with new field types
- [ ] Forms render correctly in Blazor and React
- [ ] Validation works for new field types
- [ ] Kitchen-sink demo shows new field types in action
- [ ] Documentation updated in apps/docs
- [ ] Integration tests pass
- [ ] No regressions in existing blocks-forms tests

### Owner Assignment

- **foundation changes:** typically framework-agnostic; one owner per feature
- **ui-core changes:** typically framework-agnostic; one owner per component/contract
- **ui-adapters-blazor:** one owner (or per-area owner)
- **ui-adapters-react:** one owner (or per-area owner)
- **blocks-*:** one owner per block
- **apps/docs:** docs owner
- **apps/kitchen-sink:** demo owner
- **tooling/scaffolding-cli:** tooling owner

For parallel work, assign owners to independent areas (e.g., React and Blazor adapters can be
parallel; foundation and adapters must be sequential).

### Dependency Management

Common dependencies:
- foundation changes → all downstream packages depend on them
- ui-core contract changes → all adapters must implement them
- adapter changes → blocks depending on that adapter
- kitchen-sink updates → must wait for adapter implementation

Document blockers:
- "Task C cannot start until Task A is complete"
- "Task X requires decision from [person/group]"
- "Task Y requires [third-party library] to be updated first"

## Common Implementation Plans

### New Block
```
1. Define types in foundation
2. Define component contracts in ui-core
3. Implement component in Blazor adapter
4. Implement component in React adapter
5. Create block composition in blocks-*
6. Add kitchen-sink demo
7. Write tests (unit, integration, parity)
8. Update apps/docs
9. Update scaffolding templates
10. Acceptance: all tests passing, demo working, docs complete
```

### Breaking API Change
```
1. Update affected types in foundation/ui-core
2. Update Blazor adapter to new API
3. Update React adapter to new API
4. Update all blocks that use changed API
5. Write migration guide for consumers
6. Update kitchen-sink and docs examples
7. Write migration tests (old code migrates correctly)
8. Deprecate old API (if keeping it temporarily)
9. Update scaffolding templates
10. Acceptance: parity tests pass, migration path clear, docs complete
```

### Bug Fix with Test Coverage
```
1. Write failing unit test that reproduces bug
2. Find and understand the bug in code
3. Fix the bug
4. Unit test now passes
5. Write regression test to prevent reoccurrence
6. Check parity (does bug affect all adapters?)
7. Update affected tests
8. Acceptance: bug fixed, regression test written, no new failures
```

## When Implementation-Plan is Complete

Implementation planning is done when:
- All major tasks identified and ordered
- Dependencies mapped and critical path clear
- Ownership assigned
- Code locations mapped
- Test strategy documented (unit, integration, parity, regression)
- Documentation needs identified (comments, kitchen-sink, docs, changelog)
- Acceptance criteria clear for each task
- Ready to hand to stage 06 for execution
