# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-04-16

## User Preferences

<!-- How the user likes things done. Code style, tools, patterns, communication. -->

## Key Learnings

- **Project:** Sunfish
- **Description:** Sunfish is a framework‑agnostic suite of open‑source and commercial building blocks that lets you rapidly scaffold, prototype, and ship real-world applications with interchangeable UI and domain compo

## Do-Not-Repeat

<!-- Mistakes made and corrected. Each entry prevents the same mistake recurring. -->
<!-- Format: [YYYY-MM-DD] Description of what went wrong and what to do instead. -->
[2026-04-16] `but commit` fails with GPG timeout when `commit.gpgsign` is not explicitly set to `false` in `.git/config`, even with `gitbutler.signcommits=false`. Fix: run `git config --local commit.gpgsign false` before `but commit`, then `git config --local --unset commit.gpgsign` after.
[2026-04-16] `PackageVersion` is a reserved CPM item type — using it as a `<PropertyGroup>` property collides with Central Package Management semantics. Use `<Version>` for the global version property in Directory.Build.props instead.
[2026-04-16] `.gitignore` had `**/[Pp]ackages/*` (NuGet restore rule) which also ignored `/packages/` (Sunfish source root). Fix: scope the NuGet rule to nested paths only: `*/**/[Pp]ackages/*`. Never use `**/[Pp]ackages/*` in this repo.
[2026-04-16] `but stage` requires a single file path per call — it cannot accept a directory glob. Stage directories by looping over individual file paths. Transient "database is locked" errors (code 5) clear after a brief moment — just retry the affected file.
[2026-04-16] When migrating Marilo enums, namespace-only `sed` replacements leave Marilo product names in XML doc comments and may leave enum type names unchanged (e.g. MariloResizeEdges). Always run `grep -r "Marilo"` after namespace replacement and fix doc comments + type names manually.
[2026-04-16] `but stage` syntax is `but stage <file> <branch>` — both arguments required. The cerebrum entry saying it takes only a file path is WRONG; the correct form includes the branch name.
[2026-04-16] Sunfish.Foundation requires `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in the csproj to resolve Blazor types (RenderFragment, MouseEventArgs) in models — matching Marilo.Core's approach.
[2026-04-16] When migrating model files that depend on types from future migration tasks (e.g., ScenarioStatus from Task 7, SunfishTheme from Task 5), use a ForwardRefs.cs stub file in packages/foundation/Models/ForwardRefs/ to unblock the build. Comment out the real `using` and add a TODO with the task number. Delete ForwardRefs.cs when the real types are migrated.
[2026-04-16] Task 2 renamed enum types to drop the Marilo prefix only (e.g., MariloResizeEdges → ResizeEdges, NOT SunfishResizeEdges). When writing model code that references these enums, use the unprefixed name (ResizeEdges, not SunfishResizeEdges).

## Decision Log

<!-- Significant technical decisions with rationale. Why X was chosen over Y. -->
