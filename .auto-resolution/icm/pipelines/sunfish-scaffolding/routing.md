# sunfish-scaffolding Routing

Navigate default stages for scaffolding/generator changes.

**Key points:**
- Stages 00–03 are lightweight (design generators)
- **Stage 04 (Scaffolding) is the main work stage** — actual generator code
- Stage 05–06 test generated apps
- Stage 07–08 release updated tooling

**Example flow:**
1. Intake: "Add template for new form block"
2. Discovery: Research how existing block templates work
3. Architecture: Design template structure and options
4. Package-design: Define template inputs/outputs
5. **Scaffolding: Implement the generator/template** ← Main work
6. Implementation-plan: Test plan for generated apps
7. Build: Generate sample apps, test they build and run
8. Review: Verify generated code quality
9. Release: Publish updated CLI/tooling
