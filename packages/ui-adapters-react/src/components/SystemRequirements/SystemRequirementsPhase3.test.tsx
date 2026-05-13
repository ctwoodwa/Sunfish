import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SystemRequirementsInlinePanel } from './SystemRequirementsInlinePanel';
import { SystemRequirementsRegressionBanner } from './SystemRequirementsRegressionBanner';
import type { SystemRequirementsResult, DimensionEvaluation } from '../../contracts/SystemRequirements';
import { OverallVerdict, DimensionPassFail, DimensionPolicyKind, DimensionChangeKind } from '../../contracts/SystemRequirements';

const EVALUATED_AT = '2026-05-12T00:00:00+00:00';

function makeDim(
  dimension: DimensionEvaluation['dimension'],
  policy: DimensionEvaluation['policy'],
  outcome: DimensionEvaluation['outcome'],
): DimensionEvaluation {
  return { dimension, policy, outcome };
}

function makeResult(dims: DimensionEvaluation[]): SystemRequirementsResult {
  return { overall: OverallVerdict.Pass, dimensions: dims, evaluatedAt: EVALUATED_AT };
}

describe('SystemRequirementsInlinePanel', () => {
  it('toggles open on click — <details> gains open attribute', () => {
    const result = makeResult([
      makeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
    ]);
    render(<SystemRequirementsInlinePanel result={result} dimension={DimensionChangeKind.Hardware} />);
    const details = document.querySelector('details')!;
    const summary = document.querySelector('summary')!;
    expect(details.open).toBe(false);
    fireEvent.click(summary);
    expect(details.open).toBe(true);
  });

  it('renders unevaluated text when dimension is absent from result', () => {
    // result has Hardware but we request Network — not present → unevaluated branch
    const result = makeResult([
      makeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
    ]);
    render(<SystemRequirementsInlinePanel result={result} dimension={DimensionChangeKind.Network} />);
    expect(screen.getByText(/not evaluated/i)).toBeInTheDocument();
  });
});

describe('SystemRequirementsRegressionBanner', () => {
  it('does NOT render when previousResult is undefined', () => {
    const result = makeResult([
      makeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Fail),
    ]);
    render(<SystemRequirementsRegressionBanner result={result} />);
    expect(document.querySelector('.sf-sysreq-regression-banner')).toBeNull();
  });

  it('renders when a Required+Pass dimension flips to Required+Fail', () => {
    const previous = makeResult([
      makeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
    ]);
    const current = makeResult([
      makeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Fail),
    ]);
    render(<SystemRequirementsRegressionBanner result={current} previousResult={previous} />);
    expect(document.querySelector('.sf-sysreq-regression-banner')).not.toBeNull();
  });

  it('does NOT render when only an Informational dimension flips', () => {
    const previous = makeResult([
      makeDim(DimensionChangeKind.Network, DimensionPolicyKind.Informational, DimensionPassFail.Pass),
    ]);
    const current = makeResult([
      makeDim(DimensionChangeKind.Network, DimensionPolicyKind.Informational, DimensionPassFail.Fail),
    ]);
    render(<SystemRequirementsRegressionBanner result={current} previousResult={previous} />);
    expect(document.querySelector('.sf-sysreq-regression-banner')).toBeNull();
  });

  it('banner element has role="alert" (WCAG 2.2 SC 4.1.3 — assertive live region via role)', () => {
    // Council F1: role="alert" implies aria-live="assertive" per ARIA 1.2 §5.2.
    // Redundant explicit aria-live="assertive" was removed to prevent NVDA/JAWS double-announce.
    const previous = makeResult([
      makeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
    ]);
    const current = makeResult([
      makeDim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Fail),
    ]);
    render(<SystemRequirementsRegressionBanner result={current} previousResult={previous} />);
    const banner = document.querySelector('.sf-sysreq-regression-banner');
    expect(banner).not.toBeNull();
    expect(banner).toHaveAttribute('role', 'alert');
  });
});
