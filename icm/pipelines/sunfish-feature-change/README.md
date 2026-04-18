# sunfish-feature-change Pipeline

**Purpose:** Deliver new features, new blocks, enhancements, demos, and user-facing functionality.

## When to Use This Pipeline

Use this pipeline variant when the request involves:
- Adding a new block (form, task, schedule, asset)
- Adding a new component or component family
- Enhancing existing blocks with new capabilities
- Creating new demos or examples
- Adding new adapter support
- Extending the scaffold CLI with new templates

**Do NOT use this pipeline for:**
- Breaking API changes (→ sunfish-api-change)
- Test-only work (→ sunfish-test-expansion)
- Documentation-only changes (→ sunfish-docs-change)
- Audits or review gates (→ sunfish-quality-control)
- Generator/CLI changes not tied to a feature (→ sunfish-scaffolding)

## Affected Sunfish Areas

Typical work areas for feature requests:
- foundation (new types for the new feature)
- ui-core (new component contracts)
- ui-adapters-blazor (Blazor implementation)
- ui-adapters-react (React implementation)
- blocks-* (composition and domain-specific logic)
- apps/kitchen-sink (demo of the new feature)
- apps/docs (documentation and examples)
- tooling/scaffolding-cli (optional: templates for the new feature)

## Typical Deliverables

At each stage, expect these outputs:

| Stage | Key Deliverable |
|---|---|
| 00_intake | Intake note with feature description and affected areas |
| 01_discovery | Discovery report identifying dependencies and parity concerns |
| 02_architecture | Architecture decision documenting the feature contract |
| 03_package-design | Package design note with APIs and types |
| 04_scaffolding | Scaffolding plan (if new templates needed) or SKIP |
| 05_implementation-plan | Task list with Blazor and React parallel work |
| 06_build | Code in all affected packages, demos, docs |
| 07_review | Review approval with API and parity sign-off |
| 08_release | Release checklist and published packages |

## Common Pitfalls

1. **Unequal adapter coverage**
   - Feature works in React but not Blazor (or vice versa)
   - **Fix:** Write parity tests; implement in both adapters or document why one is unavailable

2. **Kitchen-sink demo missing**
   - Feature built but users can't see it in action
   - **Fix:** Add kitchen-sink demo in 06_build, not as an afterthought

3. **compat-telerik surprise**
   - Feature works in direct Sunfish but breaks compat-telerik unexpectedly
   - **Fix:** Address compat-telerik mapping in 03_package-design, document any gaps in 02_architecture

4. **Weak documentation**
   - Feature shipped but users don't understand how to use it
   - **Fix:** Write JSDoc comments, add kitchen-sink examples, add apps/docs pages

5. **Missing migration strategy**
   - If feature changes existing block APIs, consumers don't know what to do
   - **Fix:** Document migration path in 02_architecture, provide examples in 08_release

## How This Pipeline Influences Stages

### Stage 00: Intake
- Clearly describe the feature
- Identify which Sunfish areas are affected
- Estimate scope (small, medium, large)

### Stage 01: Discovery
- Research how similar features were built in Sunfish
- Identify dependencies and parity concerns
- Document whether all adapters (Blazor, React) will support this

### Stage 02: Architecture
- Design the feature contract (framework-agnostic first)
- Identify adapter-specific implementation strategies
- Address compat-telerik compatibility or gaps
- Get buy-in on the design

### Stage 03: Package-Design
- Define APIs and types for each affected package
- Ensure consistent naming and patterns
- Document compat-telerik mappings

### Stage 04: Scaffolding (Optional)
- Include if you're adding new templates or generators
- Skip if no scaffolding changes needed

### Stage 05: Implementation-Plan
- Order tasks so foundation/ui-core work happens first
- Plan Blazor and React adapter work in parallel
- Include kitchen-sink demo as a task
- Include docs updates as a task

### Stage 06: Build
- Implement foundation/ui-core (framework-agnostic)
- Implement Blazor and React adapters (parallel)
- Write unit and parity tests
- Add kitchen-sink demo
- Update apps/docs
- Write changelog entry

### Stage 07: Review
- API review: is the design clean and consistent?
- Parity review: do Blazor and React behave the same way?
- compat-telerik review: is it compatible or acceptably incompatible?
- Docs review: are docs and demos clear?
- Test review: does parity testing pass?

### Stage 08: Release
- Version bump (MINOR for new feature)
- Changelog entry explaining the new feature
- Post-release: update kitchen-sink, docs, examples
- Announce: explain the feature and how to use it

## Decision Points

**Can both adapters implement this feature equally?**
- Yes → proceed normally, write parity tests
- No → document why, get approval for the difference, note in release

**Does compat-telerik need this feature?**
- Yes → address in 03_package-design, implement/map in 06_build
- No → document the gap, get compat-telerik owner sign-off

**Is this a big feature or small enhancement?**
- Big (new block): expect 05_implementation-plan to be detailed, multiple parallel tasks
- Small (new prop): expect 05_implementation-plan to be simpler, faster execution

**Does this change existing block APIs?**
- Yes → return to 02_architecture for breaking change review, migrate to sunfish-api-change if needed
- No → proceed with feature-change pipeline
