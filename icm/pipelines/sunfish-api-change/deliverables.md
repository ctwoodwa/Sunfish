# sunfish-api-change Deliverables

Standard outputs expected at each stage for a breaking API change.

## Stage 00: Intake

**Artifact:** `00_intake/output/intake-note.md`

**Minimum acceptable content:**
- [ ] Problem statement: why is this breaking change necessary?
- [ ] Old API: what is currently available?
- [ ] New API: what will replace it?
- [ ] Affected packages (list all that will change)
- [ ] Affected consumers (list key blocks, apps, or tooling that use this API)
- [ ] Estimate scope (scope of change, number of packages affected)
- [ ] Timeline/constraints (when must this be done? deprecation period?)

**Example format:**
```markdown
# Intake Note: Breaking Change to FormField API

## Problem
The current FormField component API uses positional props which are hard to remember
and error-prone. We need to refactor to a config object pattern for clarity.

## Old API
FormField accepts: (type, label, value, onChange, required, validate)

## New API
FormField accepts: FormFieldConfig = { type, label, value, onChange, required, validate }

## Affected Packages
- ui-core (component contract changes)
- ui-adapters-blazor (implementation changes)
- ui-adapters-react (implementation changes)
- blocks-forms (uses FormField)
- apps/kitchen-sink (demos FormField)
- apps/docs (documents FormField)

## Estimated Impact
- 6 packages need updates
- ~20 consumer code locations in blocks
- ~10 examples in kitchen-sink
- ~3 pages in docs

## Timeline
Want to ship in v2.0.0; 2-week timeline.
```

**Example naming:** `intake-breaking-change-form-field-api-2025-04-16.md`

---

## Stage 01: Discovery

**Artifact:** `01_discovery/output/discovery-report.md`

**Minimum acceptable content:**
- [ ] Complete reverse-dependency analysis (every package/block that uses the changing API)
- [ ] Impact per package (how many locations in code, how disruptive is migration)
- [ ] Adapter implications (same changes needed in Blazor and React, or different?)
- [ ] compat-telerik impact (compatible with new API, or breaking for compat-telerik too?)
- [ ] Version cascade (if foundation/ui-core changes, which downstream packages must update?)
- [ ] Recommended migration timeline (can this be done in one release, or phased?)

**Reverse-dependency checklist:**
```
[ ] Searched codebase for all uses of old API
[ ] Listed every block/app/file that imports/uses the API
[ ] Assessed effort to migrate each location
[ ] Identified any complex migration scenarios
[ ] Checked if migration can be automated (tool/script)
```

**Example naming:** `01-discovery-report-form-field-api-2025-04-16.md`

---

## Stage 02: Architecture

**Artifact:** `02_architecture/output/architecture-decision.md`

**Minimum acceptable content:**
- [ ] **Old API (before):** Full signature/interface with examples
- [ ] **New API (after):** Full signature/interface with examples
- [ ] **Rationale:** Why this change is necessary and beneficial
- [ ] **Breaking changes:** List every breaking change (old API removed, props renamed, types changed)
- [ ] **Migration path:** Step-by-step how consumers upgrade
- [ ] **Timeline:** Deprecation period (if any) and removal date
- [ ] **Alternatives considered:** Why we didn't choose other approaches
- [ ] **Adapter implications:** Any framework-specific migration concerns (Blazor vs. React)

**Example format:**
```markdown
# Architecture Decision: Form Field API Refactor

## Old API (v1.x)
FormField(type, label, value, onChange, required?, validate?)
- Positional parameters
- Hard to remember correct order
- Difficult to add new optional properties

## New API (v2.0)
FormField(config: FormFieldConfig)
- Config object pattern
- Clear property names
- Easy to extend with new properties

## Breaking Changes
1. Component interface: from positional to config object
2. Prop names: type (no change), label (no change), value (no change), 
   onChange (no change), required (no change), validate (no change)
3. Return type: unchanged

## Migration Path

### Step 1: From positional to named
Before:
```typescript
<FormField 'text' 'Email' email onChange={setEmail} true />
```

After:
```typescript
<FormField config={{
  type: 'text',
  label: 'Email',
  value: email,
  onChange: setEmail,
  required: true
}} />
```

### Step 2: For TypeScript
Ensure config is properly typed:
```typescript
const fieldConfig: FormFieldConfig = {
  type: 'text',
  label: 'Email',
  value: email,
  onChange: setEmail,
  required: true
};
<FormField config={fieldConfig} />
```

## Deprecation Timeline
- v1.5.0: Introduce new API, old API works but logs deprecation warning
- v2.0.0: Remove old API entirely

## Adapter Impact
Both Blazor and React adapt their implementations identically; no framework-specific issues.
```

**Example naming:** `02-architecture-decision-form-field-api-2025-04-16.md`

---

## Stage 03: Package-Design

**Artifact:** `03_package-design/output/package-design-note.md`

**Minimum acceptable content:**
- [ ] **ui-core:** Old vs. new component contract (side-by-side comparison)
- [ ] **foundation:** Any type changes (old types vs. new types)
- [ ] **ui-adapters-blazor:** How Blazor adapter will implement new contract
- [ ] **ui-adapters-react:** How React adapter will implement new contract
- [ ] **blocks-*:** Migration needed for each block that uses changed API
- [ ] **compat-telerik:** Mapping of new ui-core to Telerik (or documented incompatibility)
- [ ] **Migration examples:** Before/after code for each affected package

**Example format:**
```markdown
# Package Design Note: Form Field API Refactor

## ui-core (Component Contract)

### Old Contract
export interface FormFieldProps {
  type: FieldType;
  label: string;
  value: any;
  onChange: (value: any) => void;
  required?: boolean;
  validate?: (value: any) => string | null;
}

export function FormField(props: FormFieldProps): JSX.Element;

### New Contract
export interface FormFieldConfig {
  type: FieldType;
  label: string;
  value: any;
  onChange: (value: any) => void;
  required?: boolean;
  validate?: (value: any) => string | null;
}

export interface FormFieldComponentProps {
  config: FormFieldConfig;
}

export function FormField(props: FormFieldComponentProps): JSX.Element;

## foundation (Types)
No type changes to foundation; FormFieldConfig defined in ui-core.

## ui-adapters-blazor (Implementation)

### Old Implementation
```csharp
<div>
  <label>@Config.Label</label>
  <input type="@Config.Type.ToString()" 
         value="@Config.Value" 
         @onchange="@Config.OnChange" />
</div>
```

### New Implementation
Same structure, now receives config object:
```csharp
<div>
  <label>@Config.Config.Label</label>
  <input type="@Config.Config.Type.ToString()" 
         value="@Config.Config.Value" 
         @onchange="@Config.Config.OnChange" />
</div>
```

## ui-adapters-react (Implementation)

### Old Implementation
```typescript
export function FormField({ type, label, value, onChange, required, validate }: FormFieldProps) {
  return (
    <div>
      <label>{label}</label>
      <input type={type} value={value} onChange={e => onChange(e.target.value)} />
    </div>
  );
}
```

### New Implementation
```typescript
export function FormField({ config }: FormFieldComponentProps) {
  return (
    <div>
      <label>{config.label}</label>
      <input type={config.type} value={config.value} onChange={e => config.onChange(e.target.value)} />
    </div>
  );
}
```

## blocks-forms (Migration Example)

Before:
```typescript
<FormField 
  type="text" 
  label="Name" 
  value={name} 
  onChange={setName} 
  required 
/>
```

After:
```typescript
<FormField config={{
  type: 'text',
  label: 'Name',
  value: name,
  onChange: setName,
  required: true
}} />
```

## compat-telerik (Compatibility)
compat-telerik wraps FormField as TelerikFormField. No API changes needed in compat-telerik;
it passes through config object to underlying FormField. Compatible.
```

**Example naming:** `03-package-design-note-form-field-api-2025-04-16.md`

---

## Stage 04: Scaffolding (Optional)

**Artifact:** `04_scaffolding/output/scaffolding-plan.md` (if applicable)

**Content:**
- Templates/generators that need updating to use new API
- Generated code examples (before/after new API usage)

**Skip if:** Generated templates don't reference the changed API

**Example naming:** `04-scaffolding-plan-form-field-templates-2025-04-16.md`

---

## Stage 05: Implementation-Plan

**Artifact:** `05_implementation-plan/output/implementation-plan.md`

**Minimum acceptable content:**
- [ ] Overview: what breaking change is being implemented
- [ ] Ordered task list (foundation/ui-core first, then adapters, then consumers)
- [ ] For each task: owner, acceptance criteria, blockers
- [ ] Test strategy: unit tests for new API, migration tests (old → new), regression tests
- [ ] Document updates: JSDoc for new API, migration guide outline

**Task example:**
```
1. Implement FormFieldConfig in ui-core
   Owner: [name]
   Acceptance: New config interface defined, exported, JSDoc complete

2. Update Blazor adapter to use new config interface
   Owner: [Blazor lead]
   Acceptance: Component accepts config, renders correctly, unit tests pass

3. Update React adapter to use new config interface
   Owner: [React lead]
   Acceptance: Component accepts config, renders correctly, unit tests pass

4. Migrate blocks-forms to use new API
   Owner: [forms owner]
   Acceptance: All FormField calls use new API, tests pass

5. Update kitchen-sink examples to new API
   Owner: [demo owner]
   Acceptance: All FormField examples use new API, demos work

6. Write migration tests
   Owner: [test lead]
   Acceptance: Tests verify old code pattern migrates to new, tests pass

7. Update docs migration guide
   Owner: [docs owner]
   Acceptance: Migration guide has before/after examples, is clear

8. Update apps/docs API reference
   Owner: [docs owner]
   Acceptance: FormField API documented with new interface
```

**Example naming:** `05-implementation-plan-form-field-api-refactor-2025-04-16.md`

---

## Stage 06: Build

**Artifacts:**
- Code changes in packages/, apps/, tooling/ (new API)
- Migrated code in all consumers
- `06_build/output/implementation-summary.md`

**Code checklist:**
- [ ] New API implemented in ui-core
- [ ] New API interface defined with JSDoc
- [ ] Blazor adapter updated
- [ ] React adapter updated
- [ ] All blocks/consumers migrated to new API
- [ ] kitchen-sink examples use new API
- [ ] apps/docs examples use new API
- [ ] Unit tests for new API (80%+ coverage)
- [ ] Migration tests (verify old pattern migrates to new)
- [ ] Regression tests (existing functionality unchanged)
- [ ] No ESLint/compiler warnings
- [ ] Changelog entry prepared
- [ ] Migration guide drafted

**Implementation summary minimum:**
- [ ] Summary of changes (API refactored from X to Y)
- [ ] Code changes by package (list files modified)
- [ ] Tests added (unit, migration, regression counts)
- [ ] Documentation updates (JSDoc, docs, examples, migration guide)
- [ ] Deviations from plan (if any)
- [ ] Ready for review?

**Example naming:** `06-implementation-summary-form-field-api-2025-04-16.md`

---

## Stage 07: Review

**Artifact:** `07_review/output/review-approval.md`

**Review checkpoints specific to breaking changes:**
- [ ] New API is clearly better than old API
- [ ] All reverse dependencies found and migrated
- [ ] Both adapters updated (parity maintained)
- [ ] Migration path is clear and testable
- [ ] Migration tests pass (old code successfully upgrades)
- [ ] Regression tests pass (nothing else broken)
- [ ] Docs updated (new API documented, old API not mentioned)
- [ ] compat-telerik compatible or approved incompatible
- [ ] Version bump: MAJOR (semantic versioning)
- [ ] Migration guide complete and clear

**Sign-off section:**
```
## Approval Status

- [x] Architecture Reviewer: [name] — [date]
- [x] Adapter Reviewer (Blazor): [name] — [date]
- [x] Adapter Reviewer (React): [name] — [date]
- [x] Migration Reviewer: [name] — [date]
- [x] Test Reviewer: [name] — [date]
- [x] Release Manager: [name] — [date]

**Status:** APPROVED for MAJOR version release
```

**Example naming:** `07-review-approval-form-field-api-2025-04-16.md`

---

## Stage 08: Release

**Artifact:** `08_release/output/release-checklist.md`

**Pre-release:**
- [ ] Version number determined: MAJOR (v2.0.0)
- [ ] Changelog drafted with:
  - List of breaking changes
  - Migration guide with before/after examples
  - Removal timeline (if applicable)
- [ ] Migration guide finalized and tested
- [ ] Docs and examples use new API only
- [ ] All tests passing

**Release:**
- [ ] Packages published with v2.0.0
- [ ] Release tag created
- [ ] Release notes posted with migration guide prominently featured
- [ ] Blog post/announcement drafted

**Post-release:**
- [ ] Announcement sent to community
- [ ] Migration guide available on docs site
- [ ] Examples updated
- [ ] Support plan in place for migration questions

**Example naming:** `08-release-checklist-form-field-api-v2.0.0-2025-04-16.md`

---

## Key Differences from sunfish-feature-change

| Aspect | Feature Change | API Change |
|---|---|---|
| Version bump | MINOR | MAJOR |
| Scope | Add capability | Change existing API |
| Regression risk | Low (new code) | High (existing consumers) |
| Migration effort | None (backward compatible) | High (consumers must update) |
| Deprecation period | Not applicable | Often needed (for grace period) |
| Test strategy | New tests mostly | Heavy migration testing |
| Documentation | New examples | Before/after migration guide |
| Timeline | Flexible | Often fixed (major release cadence) |
