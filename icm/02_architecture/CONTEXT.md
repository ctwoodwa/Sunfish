# Stage 02: Architecture

**Purpose:** Make design decisions, document architecture rationale, and define cross-package contracts
that will guide implementation.

## Inputs

- discovery-report.md from 01_discovery/output/
- Package README files, types, and design patterns
- Existing ADRs or design decisions in Sunfish

## Process

1. **Define the design decision(s)**
   - What problem are we solving?
   - What are the options?
   - Which option did we choose and why?

2. **Document framework-agnostic contracts first**
   - Changes to foundation? Define new types and exports clearly
   - Changes to ui-core? Define component contracts, interfaces, expected behavior
   - These contracts are the "true north" that adapters must implement

3. **Identify adapter-specific implications**
   - Blazor: How will the Blazor adapter implement the contract?
   - React: How will the React adapter implement the contract?
   - Are there framework-specific constraints or opportunities?

4. **Flag breaking changes**
   - What is breaking in this design?
   - How will existing consumers migrate?
   - What is the deprecation/removal timeline?

5. **Assess compat-telerik impact**
   - Does compat-telerik need changes?
   - Is compat-telerik compatible with the new design?
   - If not, is that acceptable? (Document the decision)

6. **Create architecture decision**
   - File: `02_architecture/output/architecture-decision.md`
   - Use the ADR template in `/icm/_config/deliverable-templates.md`

## Outputs

- `02_architecture/output/architecture-decision.md` — design decision, rationale, implications,
  breaking changes, adapter considerations
- Optional: `02_architecture/output/contract-sketch-*.md` or `.ts/.tsx/.cs` — lightweight type/contract
  sketches if helpful

## Exit Criteria

- [ ] Design decision is clear and documented
- [ ] Rationale explains why this solution over alternatives
- [ ] Adapter implications (Blazor, React) are understood
- [ ] Breaking changes identified and migration path outlined
- [ ] compat-telerik impact assessed
- [ ] Architecture decision reviewed and approved

## Next Stage

→ **03_package-design**

Use the architecture decision to guide detailed package-level API design.

## Sunfish-Specific Considerations

### Design Principle: Framework-Agnostic First

1. Define the **contract** in foundation or ui-core (framework-agnostic types, interfaces, behaviors)
2. Then define how each adapter **implements** that contract
3. Adapters should not drive the contract; the contract drives adapters

Example: A new form field type should be defined in ui-core as a generic component interface, then
implemented separately in ui-adapters-blazor and ui-adapters-react.

### Adapter Parity

- If Blazor and React will have different implementations, that's expected (different frameworks)
- If Blazor and React will have different **capabilities** or **APIs**, document why and whether that's
  acceptable
- Default assumption: all features available in all adapters unless explicitly approved otherwise

### compat-telerik Considerations

- compat-telerik provides a Telerik-compatible surface over Sunfish
- It is not the source of truth; ui-core and adapters are
- If a new design cannot be mapped to Telerik, that's a constraint to document, not a blocker
- compat-telerik changes are policy-gated; expect to justify them in stage 07 review

Example: If we add a component feature that has no Telerik equivalent, we document:
- "This feature is only available in direct Sunfish usage"
- "compat-telerik apps cannot use this feature"
- "Is that acceptable?" (Design decision made here, enforced in review)

### Type System and Clarity

Sunfish prioritizes clarity in contracts:
- Use explicit types (avoid `any`)
- Document required vs. optional properties
- Define enum values and their meanings
- Use JSDoc/XML docs liberally on framework-agnostic contracts

### Blocks and Dependencies

If your design affects blocks:
- blocks-forms, blocks-tasks, blocks-scheduling, blocks-assets are built on foundation + ui-core
- If you change ui-core, blocks must maintain their contracts
- If you add a block, it must follow Sunfish conventions (foundation types, ui-core contracts, adapter
  parity)

### User-Facing Changes

If the design will be user-facing:
- Document the user experience, not just the implementation
- Consider how kitchen-sink demos will showcase the feature
- Plan for docs examples and API documentation

## Common Architecture Scenarios

### New Component Type in ui-core
**Decision to make:**
- What is the component contract (props, events, lifecycle)?
- How will Blazor implement it?
- How will React implement it?
- What does the user see and interact with?

**compat-telerik question:**
- Is there a Telerik equivalent? Map it. If not, note the gap.

**Block question:**
- Will any blocks use this new component? How?

---

### Breaking Change to Existing Contract
**Decision to make:**
- Why is the change necessary?
- What consumers will be affected?
- What is the migration path?
- When will the old API be removed?

**Adapter question:**
- Will Blazor and React need different migration strategies?

**compat-telerik question:**
- Will compat-telerik need adapter updates?

---

### New foundation Utility or Type
**Decision to make:**
- Why is this needed?
- What problem does it solve?
- Will blocks depend on it?
- Will adapters use it?

**Compat-telerik question:**
- Is this relevant to compat-telerik? Usually no, but check.

---

## When Architecture is Complete

Architecture is done when you can hand off to stage 03 with:
- Clear contracts for framework-agnostic types and components
- Clear implementation strategies for each adapter
- Understanding of any breaking changes and migration paths
- Assessment of compat-telerik and block impacts
- Approval from relevant stakeholders (architects, adapter owners, compat-telerik policy lead)
