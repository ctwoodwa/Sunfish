# Stage 00: Intake

**Purpose:** Capture and classify an incoming request; determine scope and select a pipeline variant.

## Inputs

- External request (from stakeholder, team, or self-initiated)
- Any existing documentation or context

## Process

1. **Describe the request**
   - Write a concise problem statement (2-3 sentences)
   - Explain why this work matters to Sunfish or its users
   - Note any timeline or priority constraints

2. **Identify affected Sunfish areas**
   - foundation? ui-core? ui-adapters-blazor? ui-adapters-react?
   - compat-telerik? Any blocks? apps/docs? apps/kitchen-sink? tooling/scaffolding-cli?
   - Mark each as "affected," "possible," or "not affected"

3. **Select a pipeline variant** (see `/icm/_config/routing.md`)
   - sunfish-feature-change — new features, blocks, demos
   - sunfish-api-change — breaking changes, public contracts
   - sunfish-scaffolding — generators, CLI, templates
   - sunfish-docs-change — docs, examples, kitchen-sink
   - sunfish-quality-control — audits, review gates, consistency checks
   - sunfish-test-expansion — test coverage, regression, parity
   - sunfish-gap-analysis — finding missing capabilities

4. **Document dependencies and constraints**
   - Are there blockers? Version requirements? Team availability?
   - Does this depend on another request or release?

5. **Create intake note**
   - File: `00_intake/output/intake-note.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- `00_intake/output/intake-note.md` — problem statement, scope, variant choice, constraints

## Exit Criteria

- [ ] Problem statement is clear and concise
- [ ] Affected Sunfish packages identified
- [ ] Pipeline variant selected and justified
- [ ] Intake note reviewed and approved

## Next Stage

→ **01_discovery**

Use the intake note to guide the discovery stage. The pipeline variant you selected will specialize
how you navigate stages 01–08.

## Sunfish-Specific Considerations

- **Framework-agnostic priority:** If the request affects foundation or ui-core, note that adapters
  will need synchronized changes.
- **Adapter parity:** Flag if this is Blazor-specific, React-specific, or both.
- **compat-telerik impact:** Does this request affect the compatibility layer? If so, flag it for
  later policy review.
- **Kitchen-sink and docs:** If this is user-facing, note that 06_build and 08_release must include
  demos and docs updates.

## When to Skip or Accelerate

- **Reusing prior discovery:** If you have a discovery document from a previous request for a related
  area, link it in the intake note and consider fast-tracking to 02_architecture.
- **Trivial changes:** For bug fixes or documentation-only updates, intake can be very brief; consider
  fast-tracking directly to 06_build or 07_review after approval.
