# sunfish-test-expansion Deliverables

## Core Deliverables

**Stage 06 (Build):**
- New test files added to codebase
- Test code is well-organized and documented
- All new tests pass
- Coverage metrics improved to target

## Quality Checks

- [ ] Tests have clear names explaining what they test
- [ ] Tests cover happy path, edge cases, error conditions
- [ ] Parity tests pass on both Blazor and React (if applicable)
- [ ] No flaky tests (tests are reliable and deterministic)
- [ ] Test coverage metric improved (before/after)
- [ ] Regression tests prevent known bugs from recurring
- [ ] Test code is maintainable and follows conventions

## Test Plan Document

**Stage 05 (Implementation-plan):** `test-expansion-plan.md`

**Content:**
- [ ] Current coverage baseline (%)
- [ ] Target coverage (%)
- [ ] Coverage gaps (which areas are untested)
- [ ] Test scenarios to add (organized by area)
- [ ] Parity matrix (if multi-adapter testing)
- [ ] Timeline to full coverage

**Example:**
```markdown
# Test Expansion Plan: blocks-forms

## Current Coverage
- blocks-forms: 65% coverage

## Target Coverage
- blocks-forms: 80% coverage

## Coverage Gaps
- FormField: 45% (gap: validation scenarios, error handling)
- FormBuilder: 75% (gap: dynamic field addition)
- FormValidator: 20% (gap: async validation, complex rules)

## Test Scenarios to Add
1. FormField validation
   - [ ] Required field validation
   - [ ] Custom validation function
   - [ ] Async validation with timeout
   - [ ] Multiple validation errors
   
2. FormBuilder scenarios
   - [ ] Add field dynamically
   - [ ] Remove field
   - [ ] Reorder fields
   - [ ] Complex form with nested structures
   
3. FormValidator
   - [ ] Validate entire form
   - [ ] Validate single field
   - [ ] Async validation
   - [ ] Cancel in-flight validations

## Parity Matrix
| Test | Blazor | React |
|---|---|---|
| FormField validation | [ ] | [ ] |
| FormBuilder add/remove | [ ] | [ ] |
| Async validation | [ ] | [ ] |

## Timeline
- Week 1: FormField tests (15 tests, 30% improvement)
- Week 2: FormBuilder tests (10 tests, 10% improvement)
- Week 3: FormValidator tests (20 tests, 40% improvement)
- Total: 2-3 weeks to 80% coverage
```

## Coverage Metrics

Track before/after for each stage:

**Before:**
- blocks-forms: 65% overall
- FormField: 45%
- FormBuilder: 75%
- FormValidator: 20%

**After:**
- blocks-forms: 82% overall ✓ (target met)
- FormField: 88%
- FormBuilder: 91%
- FormValidator: 78%

## Regression Test Example

```typescript
describe('DatePickerField: regression tests for issue #542', () => {
  it('should fire onChange event when date is selected', () => {
    // Regression: Issue #542 - onChange not firing in async scenarios
    const onChange = jest.fn();
    const { getByRole } = render(
      <DatePickerField 
        value={new Date()} 
        onChange={onChange}
      />
    );
    
    // Simulate async date selection
    fireEvent.click(getByRole('button', { name: /open calendar/i }));
    fireEvent.click(getByRole('button', { name: /15/ }));
    
    expect(onChange).toHaveBeenCalled();
  });
});
```
