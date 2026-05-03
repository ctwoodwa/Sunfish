# Stage 01: Discovery

**Purpose:** Research the scope, affected Sunfish packages, dependencies, constraints, and existing
approaches to inform architectural decisions.

## Inputs

- intake-note.md from 00_intake/output/
- Sunfish codebase (/packages, /apps, /tooling, /_shared)
- Relevant package README files and source code

## Process

1. **Map affected packages in detail**
   - For each area flagged in intake, investigate the current implementation
   - Read relevant package README, types, and API surfaces
   - Understand dependencies between packages (e.g., blocks depend on foundation and ui-core)

2. **Identify dependencies and constraints**
   - Internal: which Sunfish packages must coordinate?
   - External: are there third-party library constraints?
   - Temporal: are there timeline or release cycle constraints?
   - Team: who will own implementation?

3. **Research existing approaches in Sunfish**
   - Have similar features been implemented before?
   - Which patterns did they use?
   - How can we reuse or adapt existing solutions?

4. **Assess impacts**
   - Breaking changes needed?
   - Will adapters (Blazor, React) need different implementations?
   - Will compat-telerik compatibility be affected?
   - Do docs/kitchen-sink need updates?

5. **Create discovery report**
   - File: `01_discovery/output/discovery-report.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- `01_discovery/output/discovery-report.md` — scope, dependencies, constraints, existing approaches,
  recommendations for architecture stage

## Exit Criteria

- [ ] All affected packages identified and analyzed
- [ ] Dependencies and constraints documented
- [ ] Existing Sunfish patterns reviewed
- [ ] Discovery report reviewed and approved

## Next Stage

→ **02_architecture**

Use discovery findings to drive the architecture decisions. The report should clearly identify which
design decisions need to be made in stage 02.

## Sunfish-Specific Considerations

### Package Analysis
- **foundation:** Core types, utilities, contracts that all other packages depend on
- **ui-core:** Framework-agnostic component contracts and types
- **ui-adapters-blazor, ui-adapters-react:** Framework-specific implementations; must maintain parity
- **compat-telerik:** Compatibility shim; constrained by Telerik's API surface
- **blocks-*:** Built on foundation and ui-core; depend on adapters for rendering
- **apps/docs:** Documentation site; needs updates for user-facing changes
- **apps/kitchen-sink:** Playground for all components; needs demos for new blocks/features
- **tooling/scaffolding-cli:** Generator for new Sunfish apps

### Key Questions to Answer

1. **Is this framework-agnostic work or framework-specific?**
   - If foundation/ui-core: affects all adapters; must plan parallel adapter work
   - If adapter-specific: does it create parity gaps? Is that acceptable?

2. **Will all adapters (Blazor, React) need changes?**
   - If yes, discovery must map the implementation strategy for each

3. **Will compat-telerik be affected?**
   - If yes, is it compatible? If not, is that acceptable? Flag for stage 07 review.

4. **Is this a user-facing change?**
   - If yes, kitchen-sink demo and docs updates are mandatory outputs in stages 06 & 08

5. **Will scaffolding/generators need updates?**
   - If yes, flag for stage 04

### Typical Sunfish Dependency Patterns

```
foundation (no dependencies within Sunfish)
  ↓
ui-core (depends on foundation)
  ↓
ui-adapters-blazor, ui-adapters-react (depend on foundation + ui-core)
  ↓
blocks-* (depend on foundation + ui-core + adapters)
  ↓
apps/kitchen-sink (depends on blocks)
  ↓
apps/docs (documents all of the above)

compat-telerik (depends on foundation + ui-core, maps to ui-adapters-blazor)

tooling/scaffolding-cli (references packages/apps for templates)
```

## Common Scenarios

### New Block
1. Create new types in foundation? → likely no, reuse existing
2. Create new contracts in ui-core? → yes, for the component interface
3. Implement in ui-adapters-blazor and ui-adapters-react? → yes, must maintain parity
4. Update tooling/scaffolding-cli? → maybe, if new scaffolding template needed
5. Update kitchen-sink? → yes, add demo
6. Update compat-telerik? → check if Telerik has equivalent; if yes, add mapping

### Breaking Change to ui-core API
1. Does it affect all adapters? → yes, discovery must quantify the impact
2. Will it break existing consumer code? → discovery must identify risks
3. How many blocks need updates? → discovery must list them
4. How many apps (kitchen-sink, docs) need updates? → discovery must list them
5. Migration path? → discovery should outline it

### Adapter Parity Gap (React missing feature that Blazor has)
1. Why does Blazor have it and React doesn't? → framework limitation? missing effort? design choice?
2. Can React implement it? → discover the technical path
3. Should we require parity or accept the gap? → discovery notes the trade-off
4. Does compat-telerik care? → discovery flags if relevant

## When Discovery is Complete

Discovery is done when you can answer:
- **What is the scope?** (which packages, which users affected)
- **What are the constraints?** (timeline, team, dependencies, breaking changes)
- **What patterns in Sunfish can we reuse?**
- **What are the key design decisions that stage 02 needs to make?**
