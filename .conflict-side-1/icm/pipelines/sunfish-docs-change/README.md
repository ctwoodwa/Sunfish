# sunfish-docs-change Pipeline

**Purpose:** Manage documentation site updates, API docs, usage guides, and kitchen-sink examples.

## When to Use

Use this pipeline when the request involves:
- Updating apps/docs (docs site content)
- Adding examples or tutorials
- Updating kitchen-sink demos
- API documentation updates
- Usage guides for existing features

## Key Characteristics

- **Lightweight pipeline** (compared to feature-change)
- Stage 01 (Discovery) is often skipped (scope is clear)
- Stage 02–03 (Architecture) may be skipped (no API design needed)
- **Stage 06 (Build) focuses on writing docs and examples**
- Stage 07 (Review) emphasizes clarity and accuracy

## Typical Flow

1. Intake: "Update docs for the new DatePicker component"
2. Discovery: (optional) Research existing docs structure
3. Architecture: (skip) Not applicable
4. Package-design: (skip) Not applicable
5. Implementation-plan: Plan which docs to write/update
6. **Build: Write docs, examples, kitchen-sink demos** ← Main work
7. Review: Check links, verify examples work, readability
8. Release: Publish updated docs site
