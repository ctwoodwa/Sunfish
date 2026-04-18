# Stage 04: Scaffolding

**Purpose:** Design and implement generator/template changes if tooling/scaffolding-cli is affected.

**Note:** This stage is optional and only needed if the change involves scaffolding, templates, or CLI tools.

## Inputs

- package-design-note.md from 03_package-design/output/ (if this stage is used)
- Existing generator templates and tooling/scaffolding-cli code

## When This Stage Applies

Include this stage if the request involves:
- Adding new generator/template (e.g., new block scaffold)
- Changing how Sunfish apps are initialized
- Updating the scaffolding CLI (new commands, new options)
- Changing how code generation works
- Template-level impact (files generated, structure, defaults)

Skip this stage if:
- No scaffolding changes are needed
- Changes are only to runtime code (packages, apps, blocks)

## Process

1. **Identify affected generators/templates**
   - Which tooling/scaffolding-cli templates need changes?
   - Are new templates needed?
   - What is the scope of change?

2. **Design the template**
   - What files does it generate?
   - What is the directory structure?
   - What variables/options does it support?
   - How does it reflect the package-design decisions from stage 03?

3. **Plan code generation**
   - How will the generator produce the right code?
   - Will it generate TypeScript, C#, or both?
   - How will it handle options and customization?
   - How will it integrate with the existing CLI?

4. **Plan testing strategy**
   - Generate a sample app with the new template
   - Build and run it
   - Check generated code quality against Sunfish standards
   - Test on both Blazor and React targets (if applicable)

5. **Create scaffolding plan**
   - File: `04_scaffolding/output/scaffolding-plan.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- `04_scaffolding/output/scaffolding-plan.md` — generators/templates to change, implementation
  details, test strategy
- Optional: `04_scaffolding/scripts/` — any helper scripts for template generation or testing

## Exit Criteria

- [ ] Affected generators/templates identified
- [ ] Template design clear (files, structure, variables)
- [ ] Code generation strategy documented
- [ ] Test strategy defined (sample app generation, quality checks)
- [ ] Scaffolding plan reviewed and approved

## Next Stage

→ **05_implementation-plan**

The scaffolding plan informs the implementation plan's task breakdown. Template work will be part of
stage 06 build.

## Sunfish-Specific Considerations

### Sunfish Scaffolding Conventions
- Scaffolding generates Sunfish app skeletons with foundation, ui-core, adapters, and blocks
- Generated code should follow Sunfish conventions (file structure, naming, imports)
- Generated code should be clean and production-ready

### Template Content Guidelines
- Generated files should import from Sunfish packages (not copy code)
- Use examples that showcase Sunfish features (framework-agnostic patterns first)
- Include both Blazor and React examples (or clearly mark as adapter-specific)
- Reference kitchen-sink for more examples

### Testing Generated Code
- Generated app must build without warnings
- Generated app must start and render without errors
- Generated code must follow ESLint/analyzer rules
- Generated examples must work as expected
- Both Blazor and React targets should produce equivalent functional apps

### Adapter Handling
If generating for both Blazor and React:
- Use shared templates where possible (avoid duplication)
- Use adapter-specific templates for framework-specific code
- Ensure generated Blazor and React apps have equivalent functionality
- Document why any differences exist

## Common Scaffolding Scenarios

### New Block Scaffold Template
1. Generator creates a new block directory
2. Generates boilerplate types, exports, examples
3. Generates adapter-specific components (Blazor and React)
4. Generates kitchen-sink demo component
5. Test: scaffold a new block, build, verify it appears in kitchen-sink

---

### CLI Command Enhancement
1. Add new command to tooling/scaffolding-cli
2. Command takes options (e.g., `sunfish init --template my-block`)
3. Command runs the appropriate generator
4. Test: run new command, verify output

---

### Template Directory Restructure
1. Move/reorganize template files in tooling/scaffolding-cli
2. Update generator to use new paths
3. Ensure backward compatibility or document breaking changes
4. Test: generate apps with old and new templates, verify equivalence

## When Scaffolding is Complete

Scaffolding is done when:
- Affected generators/templates are documented
- Template/generator implementation is designed
- Test strategy is clear and will be executed in stage 06
- Ready to move to implementation planning
