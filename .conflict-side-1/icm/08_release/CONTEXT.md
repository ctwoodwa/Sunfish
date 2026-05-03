# Stage 08: Release

**Purpose:** Finalize version numbers, publish changelog, release packages, update post-release
documentation and announcements.

## Inputs

- review-approval.md from 07_review/output/
- All code and documentation changes from prior stages
- Sunfish package version files and release configuration

## Process

1. **Determine version number**
   - Semantic versioning: MAJOR.MINOR.PATCH
   - MAJOR: breaking changes
   - MINOR: new features (backward compatible)
   - PATCH: bug fixes (backward compatible)
   - Check dependencies: if foundation changes, all dependent packages get updated

2. **Write changelog**
   - User-facing summary of changes (not implementation details)
   - Organized by package affected
   - Include migration steps if breaking
   - Include new features with examples
   - Include bug fixes with affected scenarios

3. **Update package versions**
   - Update package.json (npm packages) or .csproj (NuGet packages)
   - Version number matches decision above
   - Update dependent packages if foundation/ui-core changed

4. **Publish packages**
   - Build packages (run npm/dotnet build)
   - Run tests one final time
   - Publish to registry (npm for JavaScript, NuGet for .NET)
   - Verify published packages are accessible and correct

5. **Create release tag**
   - Tag commit with release version (e.g., v2.1.0)
   - Tag should reference changelog and published packages
   - Publish release notes to GitHub

6. **Post-release documentation updates**
   - Update apps/docs with new features
   - Update kitchen-sink with new demos
   - Update tooling/scaffolding-cli docs if affected
   - Verify all links and examples work

7. **Announce the release**
   - Post to community channels (if applicable)
   - Notify dependent teams
   - Monitor for feedback and issues

8. **Create release checklist**
   - File: `08_release/output/release-checklist.md`
   - Use the template in `/icm/_config/deliverable-templates.md`

## Outputs

- Updated package versions
- `08_release/output/release-checklist.md` — pre-release, release, post-release tasks completed
- Published packages in registry
- Release tag in GitHub
- Updated apps/docs and kitchen-sink

## Exit Criteria

- [ ] Version numbers determined for all affected packages
- [ ] Changelog written and reviewed
- [ ] Package versions updated
- [ ] Packages built and tested
- [ ] Packages published to registry
- [ ] Release tag created and pushed
- [ ] Post-release docs updated
- [ ] Release announced
- [ ] Release complete

## Next Stage

→ **Release complete**

Monitor community feedback and plan any necessary follow-up releases or documentation updates.

## Sunfish-Specific Release Considerations

### Versioning Strategy

Sunfish uses semantic versioning across all packages:

- **foundation:** Core types; breaking changes to foundation are MAJOR
- **ui-core:** Component contracts; breaking changes to ui-core are MAJOR
- **ui-adapters-{blazor,react}:** Implementations; must track ui-core version
- **compat-telerik:** Compatibility layer; must track ui-core version
- **blocks-*:** Composition layer; feature additions are MINOR, breaking changes are MAJOR
- **tooling/scaffolding-cli:** CLI tool; breaking changes to CLI are MAJOR
- **apps/docs, apps/kitchen-sink:** Not versioned (updated on each release if needed)

### Cascade Versioning

If foundation or ui-core changes:
- All dependent packages get at least a MINOR version bump (new dependency version)
- If the change is breaking, dependent packages get a MAJOR bump
- Changelog for dependent packages references the upstream change

Example:
```
foundation v2.0.0 (MAJOR: breaking change to Type X)
  ↓
ui-core v2.0.0 (MAJOR: adapts to foundation change)
ui-adapters-blazor v2.0.0 (MAJOR: adapts to ui-core change)
ui-adapters-react v2.0.0 (MAJOR: adapts to ui-core change)
blocks-forms v2.0.0 (MAJOR: adapts to ui-core change, if needed)
...
compat-telerik v2.0.0 (MAJOR: adapts to foundation/ui-core changes)
```

### Changelog Format

Organize by package and change type:

```markdown
# Version X.Y.Z

## foundation
### Breaking Changes
- [change description]

### New Features
- [feature description]

### Bug Fixes
- [bug fix description]

## ui-core
### Breaking Changes
...

## ui-adapters-blazor
...

[Continue for all changed packages]

## Migration Guide (if breaking)
[Step-by-step guide for users to migrate their code]
```

### Post-Release Documentation

After publishing packages:

1. **Update apps/docs**
   - Add API documentation for new types/components
   - Add usage guides for new features
   - Update existing guides if breaking changes affect them
   - Verify all links and code examples

2. **Update kitchen-sink**
   - Add demos for new blocks/features
   - Update existing demos if APIs changed
   - Test that demos build and run in both Blazor and React

3. **Update README files**
   - Update package README files if APIs significantly changed
   - Update Sunfish main README if major features added

4. **Announce the release**
   - Post to project GitHub (Releases page)
   - Post to community channels (if applicable)
   - Include changelog and migration steps
   - Include links to docs and examples

## Breaking Change Handling

If your release includes breaking changes:

1. **Clearly identify breaking changes in changelog**
   - What changed?
   - Why did it change?
   - How do consumers migrate?

2. **Provide migration guide**
   - Step-by-step instructions
   - Before/after code examples
   - Timeline for removal of old API (if applicable)

3. **Update all examples**
   - kitchen-sink examples use new API
   - apps/docs examples use new API
   - No examples of old API remain (confusing)

4. **Consider deprecation period** (if reasonable)
   - Old API still works but logs deprecation warning
   - Timeline to remove old API announced
   - Allows consumers time to migrate

5. **Major version bump**
   - Breaking changes = MAJOR version (semantic versioning rule)
   - Users can understand "need to investigate this upgrade" from the version alone

## Handling Failed Releases

If a package fails to build/publish:

1. **Don't force publish** — investigate the error
2. **Return to 07_review** if the issue is code-related
3. **Return to 06_build** if the issue is build configuration
4. **Document the issue** in the release checklist
5. **Reattempt after fixing**

If published package is broken:

1. **Document the issue** in the release checklist
2. **Create a patch release** with the fix
3. **Use PATCH version number** (only for bug fixes)
4. **Announce the fix** with clear explanation

## Sunfish Release Examples

### Example 1: New Block (Minor Release)
```
Current: blocks-forms v1.5.0
Release: blocks-forms v1.6.0 (new DatePickerField block)

Version bumps:
- foundation: no change (no change)
- ui-core: no change (no change)
- ui-adapters-*: no change (no change)
- blocks-forms: v1.6.0 (MINOR: new block)
- compat-telerik: v1.6.0 (MINOR: supports new block)
- apps/docs, kitchen-sink: updated with new block demo

Changelog includes:
- New DatePickerField component in blocks-forms
- Example usage in kitchen-sink
- Migration: none (backward compatible)
```

### Example 2: Breaking ui-core Change (Major Release)
```
Current: ui-core v2.5.0, adapters v2.5.0, blocks v2.5.0
Release: All affected packages v3.0.0 (breaking change to component props)

Version bumps (cascade):
- foundation: v2.x (no change)
- ui-core: v3.0.0 (MAJOR: breaking change to props interface)
- ui-adapters-blazor: v3.0.0 (MAJOR: adapts to ui-core change)
- ui-adapters-react: v3.0.0 (MAJOR: adapts to ui-core change)
- blocks-*: v3.0.0 (MAJOR: adapts to ui-core changes)
- compat-telerik: v3.0.0 (MAJOR: adapts to ui-core changes)

Changelog includes:
- ui-core: breaking change to component props, migration guide
- adapters: updated to support new props
- blocks: updated to use new props
- Migration: "Before/After" code examples for common patterns

Release announcement includes:
- "This is a major release; some code changes required"
- Link to migration guide
- Timeline for support of v2.x
```

### Example 3: Bug Fix (Patch Release)
```
Current: ui-adapters-react v2.5.0
Release: ui-adapters-react v2.5.1 (bug fix)

Version bumps:
- Only ui-adapters-react: v2.5.1 (PATCH: bug fix)
- All other packages: unchanged

Changelog includes:
- ui-adapters-react: "Fixed event handler not firing in async scenarios"
- Affected scenario clearly described
- No migration needed (backward compatible)
```

## When Release is Complete

Release is complete when:
- Version numbers updated for all affected packages
- Changelog written and published
- Packages built, tested, and published to registry
- Release tag created
- Post-release docs updated and verified
- Release announced
- Release checklist complete

## Post-Release Monitoring

After release:
- Monitor community feedback and issues
- Be ready to create patch releases for critical bugs
- Plan next feature release based on feedback
- Update docs and examples based on user questions
