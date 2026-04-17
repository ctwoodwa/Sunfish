# Stage 07: Review

**Purpose:** Conduct quality gates and approvals before release: code review, API review, compat-telerik
compatibility, test coverage, documentation, and release readiness.

## Inputs

- implementation-summary.md from 06_build/output/
- All code changes (in packages/, apps/, tooling/)
- All tests and test results
- Updated docs and kitchen-sink

## Process

1. **Code quality review**
   - Code follows Sunfish conventions
   - No ESLint/compiler warnings (or documented exceptions)
   - Performance impact assessed
   - Security review (if applicable)

2. **API review**
   - New public APIs are well-designed
   - APIs are framework-agnostic (in foundation/ui-core)
   - Type system is clear and correct
   - No accidental API exports
   - Documentation is complete

3. **Adapter parity review**
   - Blazor and React adapters have equivalent functionality
   - Any intentional differences are justified and documented
   - Parity tests pass

4. **compat-telerik compatibility review**
   - If compat-telerik is affected: is it compatible?
   - If incompatible: is that approved?
   - Mapping from new ui-core to Telerik is documented
   - compat-telerik tests pass (if applicable)

5. **Test coverage review**
   - Unit test coverage meets threshold (typically 80%+)
   - Integration tests cover key scenarios
   - Regression tests verify existing functionality
   - Adapter parity tests verify equivalence

6. **Documentation review**
   - Public APIs have JSDoc/XML comments
   - kitchen-sink demo shows user-facing features
   - apps/docs have examples and guides (if user-facing)
   - Changelog entry is ready
   - Migration guide provided (if breaking)

7. **Release readiness review**
   - No outstanding issues
   - All tests passing
   - CI/CD pipeline passing
   - Ready to merge and release

8. **Create review approval**
   - File: `07_review/output/review-approval.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- `07_review/output/review-approval.md` — review findings, sign-off, any conditions or blockers

## Exit Criteria

- [ ] Code quality approved
- [ ] API design approved
- [ ] Adapter parity verified
- [ ] compat-telerik compatibility verified (or approved as incompatible)
- [ ] Test coverage meets threshold
- [ ] Documentation complete
- [ ] Release readiness confirmed
- [ ] All reviewers have signed off (or blockers documented)

## Next Stage

→ **08_release** (if APPROVED)
→ **Return to earlier stage** (if BLOCKED and requires rework)

If review finds issues that require rework, return to the relevant earlier stage:
- Code quality issues → return to 06_build
- API design issues → return to 03_package-design (or 02_architecture if fundamental)
- Test coverage → return to 06_build
- Documentation → return to 06_build

## Sunfish-Specific Review Criteria

### Code Quality Checklist
- [ ] Follows Sunfish file structure conventions
- [ ] Imports from correct packages (foundation, ui-core, blocks, etc.)
- [ ] No circular dependencies between packages
- [ ] No framework-specific code outside of adapters (if framework-agnostic)
- [ ] No duplicated code that should be in foundation/ui-core
- [ ] Comments explain *why*, not *what*
- [ ] No debugging code left behind
- [ ] No hard-coded values that should be in types/constants

### API Review Checklist
- [ ] Public APIs have clear, singular purposes
- [ ] Type names follow Sunfish conventions (e.g., `*Config`, `*Props`, `*Contract`)
- [ ] Export statements are intentional (nothing accidental)
- [ ] Required vs. optional properties are correct
- [ ] Default values are sensible
- [ ] Enums are complete and well-named
- [ ] No breaking changes (unless approved in earlier stage)

### Adapter Parity Checklist
- [ ] Blazor and React implementations behave identically (given same props/input)
- [ ] Both adapters expose same props, events, lifecycle hooks
- [ ] Both adapters have same test coverage
- [ ] Any framework-specific differences are justified and documented
- [ ] Parity tests included in test suite

### compat-telerik Checklist
- [ ] New ui-core contracts can be mapped to Telerik components (or approved gap)
- [ ] If mapping exists, compat-telerik implementation is correct
- [ ] If no mapping, documentation clearly states what compat-telerik cannot do
- [ ] compat-telerik users understand any limitations
- [ ] Breaking compat-telerik requires explicit approval from compat-telerik owner

### Test Checklist
- [ ] Unit tests cover happy path, error cases, edge cases
- [ ] Integration tests verify correct interaction between packages
- [ ] Parity tests verify Blazor/React equivalence (if applicable)
- [ ] Regression tests verify existing functionality (can run old test suite)
- [ ] Test coverage meets threshold (80%+ for new code, no reduction overall)
- [ ] All tests have clear names explaining what they test
- [ ] No flaky tests

### Documentation Checklist
- [ ] All public APIs have JSDoc (TS) or XML docs (C#)
- [ ] JSDoc includes @param, @returns, @example where relevant
- [ ] kitchen-sink demo shows user-facing features in action
- [ ] kitchen-sink examples are clear and representative
- [ ] apps/docs have API reference (if user-facing)
- [ ] apps/docs have usage guides or tutorials (if helpful)
- [ ] Migration guide provided and clear (if breaking)
- [ ] Changelog entry is user-focused, not implementation-focused

### Release Readiness Checklist
- [ ] Version number determined (major/minor/patch)
- [ ] Changelog drafted
- [ ] No known blocking issues
- [ ] CI/CD pipeline passing
- [ ] Code ready to merge to main
- [ ] Ready to cut a release tag

## Common Review Scenarios

### Feature Complete, All Tests Passing
- Approval → proceed to 08_release

### Minor Code Quality Issues
- Request fixes to 06_build
- Return to 06_build to address and retest
- Return to 07_review for confirmation

### API Design Issue Discovered
- If fundamental (e.g., wrong contract structure): return to 02_architecture or 03_package-design
- If minor (e.g., wrong prop name): return to 06_build for naming fix
- Retest after fixing
- Return to 07_review

### Test Coverage Gap
- Request new tests to 06_build
- Return to 06_build to write tests
- Return to 07_review to verify coverage

### compat-telerik Incompatibility
- Document the incompatibility (why, what's lost)
- Get explicit approval from compat-telerik owner
- If approved: note it and proceed to 08_release
- If not approved: return to earlier stage to redesign around compat-telerik constraint

### Blocking Issue Found
- Document the issue clearly
- Return to relevant earlier stage
- Address the issue (may require architecture or design rework)
- Return to 06_build
- Return to 07_review for re-review

## Who Reviews?

Typical review team:
- **Code reviewer:** Architecture and implementation quality
- **API reviewer:** Public API design, framework-agnostic patterns
- **Adapter reviewer:** Blazor and React equivalence (if applicable)
- **compat-telerik reviewer:** Compatibility assessment (if affected)
- **Test reviewer:** Coverage and test quality
- **Docs reviewer:** Documentation completeness and clarity
- **Release manager:** Overall readiness

For smaller changes, some roles may overlap. For large changes, each role should be distinct.

## When Review is Complete

Review is done when:
- All review criteria met or blockers documented
- All reviewers have signed off (or blockers are clear)
- Implementation is either approved for release or assigned back to build
- Next steps are documented
