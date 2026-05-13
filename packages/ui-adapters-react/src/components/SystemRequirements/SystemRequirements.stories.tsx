import type { Meta, StoryObj } from '@storybook/react';
import { SystemRequirements } from './SystemRequirements';
import { SystemRequirementsInlinePanel } from './SystemRequirementsInlinePanel';
import { SystemRequirementsRegressionBanner } from './SystemRequirementsRegressionBanner';
import type { SystemRequirementsResult } from '../../contracts/SystemRequirements';
import {
  OverallVerdict,
  DimensionPassFail,
  DimensionPolicyKind,
  DimensionChangeKind,
  SystemRequirementsRenderMode,
} from '../../contracts/SystemRequirements';

const EVALUATED_AT = '2026-05-12T00:00:00+00:00';

function dim(
  dimension: DimensionChangeKind,
  policy: DimensionPolicyKind,
  outcome: DimensionPassFail,
  operatorRecoveryAction?: { actionKey: string; argumentMap?: Record<string, string> },
) {
  return { dimension, policy, outcome, ...(operatorRecoveryAction ? { operatorRecoveryAction } : {}) };
}

const ALL_PASS_RESULT: SystemRequirementsResult = {
  overall: OverallVerdict.Pass,
  evaluatedAt: EVALUATED_AT,
  dimensions: Object.values(DimensionChangeKind).map((d) =>
    dim(d, DimensionPolicyKind.Required, DimensionPassFail.Pass),
  ),
};

const WARN_RESULT: SystemRequirementsResult = {
  overall: OverallVerdict.WarnOnly,
  evaluatedAt: EVALUATED_AT,
  dimensions: [
    dim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
    dim(DimensionChangeKind.Network, DimensionPolicyKind.Recommended, DimensionPassFail.Fail),
    dim(DimensionChangeKind.Runtime, DimensionPolicyKind.Required, DimensionPassFail.Pass),
  ],
};

const BLOCK_RESULT: SystemRequirementsResult = {
  overall: OverallVerdict.Block,
  evaluatedAt: EVALUATED_AT,
  dimensions: [
    dim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Fail),
    dim(DimensionChangeKind.Network, DimensionPolicyKind.Recommended, DimensionPassFail.Pass),
  ],
};

const BLOCK_WITH_RECOVERY_RESULT: SystemRequirementsResult = {
  overall: OverallVerdict.Block,
  evaluatedAt: EVALUATED_AT,
  dimensions: [
    dim(
      DimensionChangeKind.Hardware,
      DimensionPolicyKind.Required,
      DimensionPassFail.Fail,
      { actionKey: 'upgrade-ram', argumentMap: { min: '8GB', recommended: '16GB' } },
    ),
  ],
};

const HARDWARE_PASS_RESULT: SystemRequirementsResult = {
  overall: OverallVerdict.Pass,
  evaluatedAt: EVALUATED_AT,
  dimensions: [
    dim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Pass),
  ],
};

const HARDWARE_FAIL_RESULT: SystemRequirementsResult = {
  overall: OverallVerdict.Block,
  evaluatedAt: EVALUATED_AT,
  dimensions: [
    dim(DimensionChangeKind.Hardware, DimensionPolicyKind.Required, DimensionPassFail.Fail),
  ],
};

// ─── SystemRequirements (PreInstallFullPage) stories ─────────────────────────

const meta: Meta<typeof SystemRequirements> = {
  title: 'Components/SystemRequirements',
  component: SystemRequirements,
  parameters: {
    a11y: { config: { rules: [{ id: 'color-contrast', enabled: false }] } },
  },
  args: {
    bundleId: 'com.sunfish.kitchen-sink',
    mode: SystemRequirementsRenderMode.PreInstallFullPage,
  },
};

export default meta;
type Story = StoryObj<typeof SystemRequirements>;

export const Default_Pass: Story = {
  name: 'Default — all dimensions pass',
  args: { result: ALL_PASS_RESULT },
};

export const WarnOnly_Recommended_Fail: Story = {
  name: 'WarnOnly — Recommended dimension fails',
  args: { result: WARN_RESULT },
};

export const Block_Required_Fail: Story = {
  name: 'Block — Required dimension fails',
  args: {
    result: BLOCK_RESULT,
    onForceInstall: () => undefined,
  },
};

export const Block_With_Recovery_Action: Story = {
  name: 'Block — Required fail with recovery action',
  args: {
    result: BLOCK_WITH_RECOVERY_RESULT,
    onForceInstall: () => undefined,
  },
};

// ─── SystemRequirementsInlinePanel story ─────────────────────────────────────

export const Inline_Hardware_Panel: Story = {
  name: 'Inline — Hardware dimension panel',
  render: () => (
    <SystemRequirementsInlinePanel
      result={HARDWARE_PASS_RESULT}
      dimension={DimensionChangeKind.Hardware}
    />
  ),
};

// ─── SystemRequirementsRegressionBanner stories ───────────────────────────────

export const Regression_Banner_Hardware_Flip: Story = {
  name: 'Regression banner — Hardware Required pass → fail',
  render: () => (
    <SystemRequirementsRegressionBanner
      result={HARDWARE_FAIL_RESULT}
      previousResult={HARDWARE_PASS_RESULT}
      onDismiss={() => undefined}
    />
  ),
};

export const Regression_Banner_Suppressed_Informational: Story = {
  name: 'Regression banner — suppressed (Informational flip, no banner)',
  render: () => {
    const prevInfo: SystemRequirementsResult = {
      overall: OverallVerdict.Pass,
      evaluatedAt: EVALUATED_AT,
      dimensions: [
        dim(DimensionChangeKind.Network, DimensionPolicyKind.Informational, DimensionPassFail.Pass),
      ],
    };
    const currInfo: SystemRequirementsResult = {
      overall: OverallVerdict.Pass,
      evaluatedAt: EVALUATED_AT,
      dimensions: [
        dim(DimensionChangeKind.Network, DimensionPolicyKind.Informational, DimensionPassFail.Fail),
      ],
    };
    return (
      <div>
        <p style={{ fontStyle: 'italic', marginBottom: '0.5rem' }}>
          No banner expected — only an Informational dimension regressed.
        </p>
        <SystemRequirementsRegressionBanner result={currInfo} previousResult={prevInfo} />
      </div>
    );
  },
};
