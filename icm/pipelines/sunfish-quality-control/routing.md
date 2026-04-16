# sunfish-quality-control Routing

Navigate default stages for quality audits and review gates.

**Acceleration:**
- Stage 00–02: Lightweight (audit scope is usually clear)
- Skip stage 04 (Scaffolding) — not applicable
- Emphasize stage 05 (Implementation-plan) — defines audit checklist
- **Stage 06 (Build): Run the audit, document findings** ← Main work
- **Stage 07 (Review): Present findings, make recommendations** ← Central stage
- Stage 08 (Release): Publish audit report and remediation plan

**Typical timeline:** 2-3 weeks for audit, depends on scope.

**Example:** "Audit React adapter vs. Blazor adapter for API consistency"
1. Intake: Define what "consistent" means
2. Discovery: List all public APIs in both adapters
3. Implementation-plan: Checklist of consistency checks
4. Build: Run checks, document gaps
5. Review: Present findings (which APIs differ, why, acceptable or not?)
6. Release: Publish audit report, document sign-off
