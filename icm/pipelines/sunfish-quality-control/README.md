# sunfish-quality-control Pipeline

**Purpose:** Conduct audits, consistency checks, release readiness assessments, and review gates.

## When to Use

Use this pipeline when the request involves:
- API consistency audits (across adapters)
- Release readiness assessments
- Package/naming consistency checks
- compat-telerik compatibility audits
- Quality gate reviews

## Key Characteristics

- **Verification-focused** (not implementation)
- **Stage 07 (Review) is the main stage** — findings and recommendations
- No code changes expected (unless audit uncovers issues to fix)
- Produces audit report, not new features

## Typical Flow

1. Intake: "Audit API consistency across Blazor and React adapters"
2. Discovery: (lightweight) Plan audit scope
3. Architecture: (skip) Not applicable
4. Package-design: (skip) Not applicable
5. Implementation-plan: Define audit checklist and methodology
6. Build: Run audit, document findings
7. **Review: Present audit findings and recommendations** ← Main stage
8. Release: Publish audit report, document any remediation plan

## Outcome

- Audit report with findings
- List of issues found (severity levels)
- Remediation roadmap (if issues found)
- Sign-off from stakeholders (e.g., "API consistent as of 2025-04-16")
