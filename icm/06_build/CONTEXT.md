# Stage 06: Build

**Purpose:** Implement code changes across packages/, apps/, and tooling/ according to the
implementation plan from stage 05.

## Inputs

- implementation-plan.md from 05_implementation-plan/output/
- Sunfish codebase (packages/, apps/, tooling/)

## Process

1. **Execute tasks in planned order**
   - Follow the task list from stage 05
   - Work on tasks sequentially or in parallel where dependencies allow
   - Update implementation plan if scope changes

2. **Write code following Sunfish conventions**
   - Use existing file structures and patterns
   - Follow coding standards from /_shared/engineering/coding-standards.md
   - Write clear, documented code
   - Use framework-agnostic patterns first (foundation → ui-core → adapters)

3. **Write and run tests as you go**
   - Unit tests for each new function/component/type
   - Integration tests for interactions
   - Adapter parity tests (if multi-adapter)
   - Regression tests for existing functionality

4. **Update documentation as you go**
   - JSDoc/XML comments on all public APIs
   - Update existing docs if APIs change
   - Add kitchen-sink demo for user-facing changes
   - Add apps/docs examples for user-facing changes

5. **Commit early and often**
   - Each logical change is a commit
   - Commit messages reference the task and what changed
   - Avoid large monolithic commits

6. **Create implementation summary**
   - File: `06_build/output/implementation-summary.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- Code changes in packages/, apps/, tooling/
- `06_build/output/implementation-summary.md` — summary of changes, tests added, docs updated,
  deviations from plan
- Passing test suite (unit, integration, regression, parity)

## Exit Criteria

- [ ] All planned tasks complete (or documented deviations)
- [ ] All unit tests passing
- [ ] All integration tests passing
- [ ] Adapter parity tests passing (if applicable)
- [ ] Regression tests passing
- [ ] Code follows Sunfish conventions
- [ ] JSDoc/XML docs written for all public APIs
- [ ] Kitchen-sink demo added (if user-facing)
- [ ] apps/docs updated (if user-facing)
- [ ] Implementation summary created
- [ ] No blocking issues

## Next Stage

→ **07_review**

Code is ready for quality review, API approval, and release readiness check.

## Sunfish-Specific Considerations

### Code Organization

Sunfish code lives in three places:
- **packages/** — Reusable, versioned modules (foundation, ui-core, adapters, blocks)
- **apps/** — Runnable applications (docs site, kitchen-sink demo)
- **tooling/** — Developer tools (scaffolding CLI)

Implementation code belongs ONLY in these locations. Do not move code into /icm/.

### Package Conventions

- **foundation/** — Framework-agnostic utilities, types, constants
  - No dependencies on ui-core, adapters, or blocks
  - Exported types and functions are part of the public API

- **ui-core/** — Framework-agnostic component contracts
  - Depends on foundation
  - Exports interfaces and types that adapters must implement
  - No framework-specific code (no React imports, no Blazor references)

- **ui-adapters-blazor/** — Blazor implementation of ui-core contracts
  - Depends on foundation and ui-core
  - Implements ui-core interfaces using Razor/C#
  - Framework-specific code is expected and necessary

- **ui-adapters-react/** — React implementation of ui-core contracts
  - Depends on foundation and ui-core
  - Implements ui-core interfaces using React/TypeScript
  - Framework-specific code is expected and necessary

- **compat-telerik/** — Compatibility shim for Telerik components
  - Wraps Sunfish packages to provide Telerik-compatible API
  - Depends on foundation, ui-core, and ui-adapters-blazor
  - Special constraints apply (policy gated, feature mapped)

- **blocks-*/** — Composed blocks for specific domains
  - Depend on foundation, ui-core, and adapters
  - May have adapter-specific specializations if needed
  - Each block should have a clear scope and purpose

### Testing Strategy

**Unit tests** — Test each function/component/type in isolation
- foundation types: behavior of utilities and constants
- ui-core components: contract compliance, props validation, event firing
- adapter implementations: rendering, user interaction handling
- block composition: integration of foundation + ui-core + adapters

**Integration tests** — Test interactions between packages
- foundation types used correctly by ui-core components
- ui-core components work with adapters
- adapters compose into blocks correctly
- blocks work in kitchen-sink apps

**Adapter parity tests** — Ensure feature equivalence
- Same props produce same output in Blazor and React
- Same user interactions fire same events in both
- Same styling applies across adapters
- Document any intentional differences and justify them

**Regression tests** — Ensure existing functionality still works
- Existing tests still pass
- Existing apps (kitchen-sink, docs) still build and run
- Existing blocks still function

### Documentation in Code

Every public API should be documented:
```typescript
/**
 * Configuration for a form field.
 * Used by blocks-forms to define and validate user input fields.
 * 
 * @example
 * const config: FormFieldConfig = {
 *   id: 'email',
 *   label: 'Email Address',
 *   type: 'email',
 *   required: true
 * };
 */
export interface FormFieldConfig {
  /** Unique identifier for the field within the form */
  id: string;
  
  /** Display label shown to the user */
  label: string;
  
  /** Input type: 'text', 'email', 'number', etc. */
  type: FieldType;
  
  /** Whether the field is required (default: false) */
  required?: boolean;
  
  /** Custom validation function; returns error message or null */
  validate?: (value: any) => string | null;
}
```

### Kitchen-Sink Demo Updates

For user-facing changes, add a demo in apps/kitchen-sink:
- Add new component/block to demo pages
- Show typical usage patterns
- Demonstrate key features
- Use framework-agnostic patterns first
- Show both Blazor and React (if different)

### apps/docs Updates

For significant user-facing changes, update apps/docs:
- Add API documentation pages
- Add usage guides and examples
- Add migration guides (if breaking)
- Update summary pages

### Avoiding Common Pitfalls

1. **Don't move implementation into /icm/**
   - /icm is workflow only; keep code in packages/, apps/, tooling/

2. **Maintain framework-agnostic boundaries**
   - foundation and ui-core should have no framework-specific code
   - adapters are the place for framework-specific implementations

3. **Test all adapters**
   - Don't assume "if it works in React, it works in Blazor"
   - Write parity tests

4. **Update docs with code**
   - Don't leave docs for later; update them as you implement

5. **Don't skip compat-telerik**
   - Even if you just document that it's incompatible, document it
   - Don't silently break compat-telerik

## When Build is Complete

Build is done when:
- All tasks from implementation plan are complete (or deviations documented)
- All tests passing (unit, integration, parity, regression)
- Code follows Sunfish conventions
- APIs are documented
- Kitchen-sink and docs updated (if user-facing)
- Implementation summary ready
- Ready to move to stage 07 review
