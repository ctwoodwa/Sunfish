# sunfish-test-expansion Routing

Navigate default stages for test coverage work.

**Acceleration:**
- Stages 00–02: Lightweight (scope is clear: "improve coverage")
- Skip stage 03 (Package-design) — not applicable
- Emphasize stage 05 (Implementation-plan) — define test matrix and scenarios
- **Stage 06 (Build): Write tests** ← Main work
- Stage 07 (Review): Verify coverage metrics and test quality
- Stage 08 (Release): Publish updated test suite

**Typical timeline:** Depends on coverage gap; 1-4 weeks.

**Example:** "Add regression tests for date-picker form field"
1. Intake: Describe the bug and what tests should prevent recurrence
2. Implementation-plan: Define test scenarios (edge cases, error conditions)
3. Build: Write failing tests, fix code, tests pass
4. Review: Verify tests are good quality, coverage acceptable
5. Release: Merge tests to main

**Parity Testing Example:** "Ensure Blazor and React adapters have same test coverage"
1. Intake: Identify which adapters need parity testing
2. Implementation-plan: Create parity test matrix
3. Build: Write parallel tests for Blazor and React
4. Review: Verify both pass equivalently
5. Release: Publish aligned test suite
