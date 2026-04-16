# Stage 03: Package-Design

**Purpose:** Define per-package API surfaces, type systems, module boundaries, and implementation
details that will guide coding in stage 06.

## Inputs

- architecture-decision.md from 02_architecture/output/
- Existing package structures and code (to understand conventions)

## Process

1. **For each affected package, define the API surface**
   - What new types are exported?
   - What new functions/components/classes are exported?
   - What existing APIs are changed or deprecated?
   - What is the import path for each export?

2. **Define the type system**
   - What are the core types? (props interfaces, return types, enums)
   - Are there generics involved? (e.g., typed arrays, component props)
   - What is the relationship between types? (inheritance, composition, unions)
   - Use TypeScript/C# pseudocode or real type sketches

3. **Define module boundaries**
   - Which files belong in which packages?
   - What can be internal vs. exported?
   - How do packages depend on each other?

4. **Address framework-specific design**
   - **foundation:** Should be framework-agnostic; focus on data structures and utilities
   - **ui-core:** Framework-agnostic component contracts; focus on props, behavior, lifecycle
   - **ui-adapters-blazor:** Implementation in C#/Razor; map ui-core contracts to Blazor components
   - **ui-adapters-react:** Implementation in TypeScript/JSX; map ui-core contracts to React components
   - **blocks-*:** Composition layer using foundation and adapters; define what each block does
   - **compat-telerik:** Map new ui-core contracts to Telerik equivalents (or document gaps)

5. **Plan for adaptation and compatibility**
   - If the design requires compat-telerik to change, map those changes explicitly
   - If the design creates parity differences between adapters, justify them

6. **Create package design note**
   - File: `03_package-design/output/package-design-note.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- `03_package-design/output/package-design-note.md` — per-package API surfaces, types, boundaries,
  adapter implementation strategies, compat-telerik mapping

## Exit Criteria

- [ ] All affected packages have documented API surfaces
- [ ] Type system is clear and complete
- [ ] Module boundaries are defined
- [ ] Adapter implementation strategies are documented
- [ ] compat-telerik impact (or compatibility) is documented
- [ ] Migration path for breaking changes is clear
- [ ] Package design note reviewed and approved

## Next Stage

→ **04_scaffolding** (if tooling/scaffolding-cli is affected)
→ **05_implementation-plan** (if no scaffolding work needed)

## Sunfish-Specific Considerations

### foundation Package
- Contains core types, utilities, and constants
- Should be framework-agnostic
- All other packages depend on it
- Changes here require coordination across all packages

**Design questions:**
- What types are being added/changed?
- Are they used by ui-core, blocks, or both?
- Is there a migration path for existing code?

### ui-core Package
- Contains framework-agnostic component contracts
- Defines component props, events, lifecycle via interfaces
- All adapters implement these contracts

**Design questions:**
- What component contracts are being added/changed?
- What props/events/methods must all adapters support?
- Are there optional props or adapter-specific extensions?

### ui-adapters-blazor and ui-adapters-react
- Implement ui-core contracts in their respective frameworks
- May have framework-specific optimizations or constraints
- Should maintain feature parity unless explicitly approved

**Design questions:**
- How does Blazor implement this contract? (Razor components, lifecycle, interop)
- How does React implement this contract? (functional components, hooks, side effects)
- Are there framework-specific constraints or opportunities?
- Does one adapter need something the other doesn't? (Why?)

### blocks-* Packages
- Composed from foundation types and ui-core components
- Depend on adapters for rendering
- Each block should have a clear purpose and API

**Design questions:**
- What is this block's purpose? (form building, task tracking, etc.)
- What types does it use from foundation?
- What components does it use from ui-core?
- What is the public API of this block?
- How will kitchen-sink demo showcase it?

### compat-telerik Considerations
- If ui-core changes, compat-telerik may need adapter updates
- Treat compat-telerik as a *consumer* of Sunfish, not as the source of API design
- If a new Sunfish API has no Telerik equivalent, that's OK; document it

**Design questions:**
- Is there a Telerik component/prop/event that maps to this new contract?
- If yes, define the mapping (how compat-telerik will wrap it)
- If no, explicitly note: "This feature is not available in compat-telerik" and get sign-off

### Cross-Package Dependencies
Typical dependency graph:
```
foundation (no internal dependencies)
  ↓ (used by)
ui-core, blocks (depend on foundation + adapters for composition)
  ↓ (used by)
blocks, apps (depend on ui-core for component contracts)
  ↓ (used by)
apps/kitchen-sink, apps/docs (demonstrate blocks and components)
```

When designing new APIs:
- Avoid circular dependencies
- Prefer foundation/ui-core imports over block imports
- Minimize dependencies between blocks

### Type Documentation
- Every exported type should have a JSDoc/XML comment explaining its purpose
- Props interfaces should document each property (required, optional, defaults)
- Enums should list and explain each value
- Functions should document parameters and return values

Example (TypeScript):
```typescript
/**
 * Configuration for a form field.
 * Used by blocks-forms to define field behavior.
 */
export interface FormFieldConfig {
  /** Unique identifier for the field */
  id: string;
  /** Display label for the field */
  label: string;
  /** Field type (text, number, select, etc.) */
  type: FieldType;
  /** Whether the field is required */
  required?: boolean;
  /** Custom validation function */
  validate?: (value: any) => string | null;
}
```

## Common Package-Design Scenarios

### Adding a New Component to ui-core
1. Define the component contract (props interface)
2. Define the required events/callbacks
3. Define lifecycle (if applicable)
4. Plan Blazor implementation (Razor component, lifecycle)
5. Plan React implementation (functional component, hooks)
6. Plan kitchen-sink demo
7. Assess compat-telerik compatibility

---

### Adding a New Type to foundation
1. Define the type (interface, enum, class)
2. Document its purpose and when to use it
3. Identify which packages will use it (ui-core, blocks, adapters)
4. Plan any migration for existing code
5. Document any framework-specific serialization needs (if JSON/etc.)

---

### Adding a New Block
1. Define the block's purpose and scope
2. Identify foundation types it uses
3. Identify ui-core components it composes
4. Plan the block's public API (exports, functions)
5. Plan adapters (if block-specific adaptation needed)
6. Plan kitchen-sink demo and docs examples

---

### Breaking Change to Existing Type/Component
1. Document the old API (before change)
2. Document the new API (after change)
3. Document migration steps for consumers
4. Identify all packages affected
5. Plan deprecation period (if applicable)
6. Identify which blocks/apps will need updates

## When Package-Design is Complete

Package design is done when:
- Every affected package has a documented API surface (types, exports, imports)
- Type system is clear (interfaces, enums, classes are defined)
- Module boundaries are explicit (what goes where, why)
- Adapter implementation strategies are clear (how Blazor and React will implement contracts)
- compat-telerik impact is mapped (compatible, incompatible with justification, or requiring changes)
- Migration path for breaking changes is documented
- Ready to hand to stage 05 implementation planning
