import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SystemRequirements } from './SystemRequirements';
import { STRINGS } from './SystemRequirements.strings';
import type { SystemRequirementsResult, DimensionEvaluation } from '../../contracts/SystemRequirements';
import { OverallVerdict, DimensionPassFail, DimensionPolicyKind, DimensionChangeKind } from '../../contracts/SystemRequirements';
import { CssProviderProvider } from '../../CssProviderContext';
import { BootstrapCssProvider } from '../../providers/BootstrapCssProvider';

function wrap(ui: React.ReactElement) {
  return render(
    <CssProviderProvider provider={new BootstrapCssProvider()}>{ui}</CssProviderProvider>,
  );
}

const EVALUATED_AT = '2026-05-12T00:00:00+00:00';

function makeDimension(
  dimension: DimensionEvaluation['dimension'],
  policy: DimensionEvaluation['policy'],
  outcome: DimensionEvaluation['outcome'],
  opts?: Partial<DimensionEvaluation>,
): DimensionEvaluation {
  return { dimension, policy, outcome, ...opts };
}

function makePassResult(): SystemRequirementsResult {
  return {
    overall: OverallVerdict.Pass,
    dimensions: [
      makeDimension(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
      makeDimension(DimensionChangeKind.Network, DimensionPolicyKind.Recommended, DimensionPassFail.Pass),
    ],
    evaluatedAt: EVALUATED_AT,
  };
}

function makeWarnResult(): SystemRequirementsResult {
  return {
    overall: OverallVerdict.WarnOnly,
    dimensions: [
      makeDimension(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
      makeDimension(DimensionChangeKind.Network, DimensionPolicyKind.Recommended, DimensionPassFail.Fail),
    ],
    evaluatedAt: EVALUATED_AT,
  };
}

function makeBlockResult(withRecovery = false): SystemRequirementsResult {
  return {
    overall: OverallVerdict.Block,
    dimensions: [
      makeDimension(
        DimensionChangeKind.Hardware,
        DimensionPolicyKind.Required,
        DimensionPassFail.Fail,
        withRecovery
          ? { operatorRecoveryAction: { actionKey: 'upgrade-ram', argumentMap: { min: '8GB' } } }
          : undefined,
      ),
    ],
    evaluatedAt: EVALUATED_AT,
  };
}

describe('SystemRequirements (PreInstallFullPage)', () => {
  it('Pass verdict renders pass banner text', () => {
    wrap(
      <SystemRequirements
        result={makePassResult()}
        mode="PreInstallFullPage"
        bundleId="test"
      />,
    );
    expect(screen.getByText(STRINGS.verdict.pass)).toBeInTheDocument();
    // No dimension row has a block or warn status in its SR-only text (council B3: icon is aria-hidden)
    const listItems = screen.getAllByRole('listitem');
    expect(listItems.every((li) => !li.textContent?.includes(STRINGS.status.block))).toBe(true);
  });

  it('WarnOnly verdict renders warn banner text and at least one warn-status row', () => {
    wrap(
      <SystemRequirements
        result={makeWarnResult()}
        mode="PreInstallFullPage"
        bundleId="test"
      />,
    );
    expect(screen.getByText(STRINGS.verdict.warn)).toBeInTheDocument();
    // Status conveyed via visually-hidden text (council B3: icon is aria-hidden="true")
    const listItems = screen.getAllByRole('listitem');
    expect(listItems.some((li) => li.textContent?.includes(STRINGS.status.warn))).toBe(true);
  });

  it('Block verdict renders block banner, Required+Fail row, and forceInstall button', () => {
    wrap(
      <SystemRequirements
        result={makeBlockResult()}
        mode="PreInstallFullPage"
        bundleId="test"
      />,
    );
    expect(screen.getByText(STRINGS.verdict.block)).toBeInTheDocument();
    // Status conveyed via visually-hidden text (council B3)
    const listItems = screen.getAllByRole('listitem');
    expect(listItems.some((li) => li.textContent?.includes(STRINGS.status.block))).toBe(true);
    expect(
      screen.getByRole('button', { name: STRINGS.actions.forceInstall }),
    ).toBeInTheDocument();
  });

  it('Block verdict without operatorRecoveryAction renders row without recovery block', () => {
    wrap(
      <SystemRequirements
        result={makeBlockResult(false)}
        mode="PreInstallFullPage"
        bundleId="test"
      />,
    );
    expect(screen.queryByText(STRINGS.recovery.tryThis)).not.toBeInTheDocument();
  });

  it('string constants resolve — page title from STRINGS object is visible', () => {
    wrap(
      <SystemRequirements
        result={makePassResult()}
        mode="PreInstallFullPage"
        bundleId="test"
      />,
    );
    expect(screen.getByText(STRINGS.title.preInstall)).toBeInTheDocument();
  });

  it('onForceInstall callback fires once when forceInstall button clicked', () => {
    const onForceInstall = vi.fn();
    wrap(
      <SystemRequirements
        result={makeBlockResult()}
        mode="PreInstallFullPage"
        bundleId="test"
        onForceInstall={onForceInstall}
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: STRINGS.actions.forceInstall }));
    expect(onForceInstall).toHaveBeenCalledTimes(1);
  });

  it('Block verdict + no onForceInstall handler renders a disabled forceInstall button', () => {
    wrap(
      <SystemRequirements
        result={makeBlockResult()}
        mode="PreInstallFullPage"
        bundleId="test"
      />,
    );
    const btn = screen.getByRole('button', { name: STRINGS.actions.forceInstall });
    expect(btn).toBeDisabled();
  });

  it('empty dimensions array renders the list without throwing', () => {
    const emptyResult: SystemRequirementsResult = {
      overall: OverallVerdict.Pass,
      dimensions: [],
      evaluatedAt: EVALUATED_AT,
    };
    wrap(
      <SystemRequirements result={emptyResult} mode="PreInstallFullPage" bundleId="test" />,
    );
    expect(screen.getByRole('list')).toBeInTheDocument();
    expect(screen.queryAllByRole('listitem')).toHaveLength(0);
  });

  it('unknown mode string throws an error (all valid modes are now implemented in Phase 3)', () => {
    // Phase 3 implements all three SystemRequirementsRenderMode values.
    // Only a truly unknown mode string reaches the throw path.
    expect(() =>
      wrap(
        <SystemRequirements
          result={makePassResult()}
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          mode={'UnknownMode' as any}
          bundleId="test"
        />,
      ),
    ).toThrow(/unknown mode/);
  });
});
