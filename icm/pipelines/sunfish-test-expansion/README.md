# sunfish-test-expansion Pipeline

**Purpose:** Improve test coverage, write regression tests, and ensure adapter parity testing.

## When to Use

Use this pipeline when the request involves:
- Increasing code coverage (target coverage %)
- Writing regression tests for bugs
- Parity testing (Blazor vs. React)
- Scenario/matrix coverage expansion
- Test infrastructure improvements

## Key Characteristics

- **Test-focused** (not implementation)
- No API changes expected
- **Stage 06 (Build) focuses on test code**
- Stage 07 (Review) emphasizes test quality and coverage metrics

## Typical Flow

1. Intake: "Improve test coverage for blocks-forms from 65% to 80%"
2. Discovery: Identify coverage gaps
3. Implementation-plan: Define test matrix and scenarios
4. **Build: Write tests** ← Main work
5. Review: Verify coverage meets target, test quality is good
6. Release: Publish updated test suite

## Outcome

- New tests added to codebase
- Coverage metrics improved
- Test infrastructure maintained/improved
