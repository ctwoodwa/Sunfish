# sunfish-gap-analysis Routing

Navigate default stages for gap identification and remediation planning.

**Key emphasis:**
- **Stage 01 (Discovery): Heavyweight** — scope the gap, understand why it exists
- **Stage 02 (Architecture): Heavyweight** — design the remediation
- Stage 03–05: Plan remediation
- Stage 06+: May not execute immediately (gap closure might be deferred)

**Typical timeline:** 2-4 weeks (may lead to new request if gap should be closed).

**Example:** "React adapter is missing date validation that Blazor has"
1. Intake: Describe the gap (React validation missing)
2. Discovery: Understand why (effort? framework limitation? oversight?)
3. Architecture: Design how to add validation to React
4. Implementation-plan: Task list for adding validation
5. Build: Implement validation in React (or defer)
6. Review: Verify parity achieved
7. Release: Publish updated React adapter

**Approved Gap Example:** "compat-telerik doesn't support new calendar block"
1. Intake: Note the gap
2. Discovery: Understand why (no Telerik equivalent?)
3. Architecture: Decide "approved gap" (compat-telerik intentionally doesn't support this)
4. Review: Document and sign-off on approved incompatibility
5. Release: Publish documentation of gap
