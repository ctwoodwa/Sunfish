import { DimensionChangeKind } from '../../contracts/SystemRequirements';

export const STRINGS = {
  title: {
    preInstall: 'System Requirements',
  },
  verdict: {
    pass: 'Your system meets all requirements',
    warn: 'Some recommendations not met',
    block: 'Your system does not meet all requirements',
  },
  dimension: {
    [DimensionChangeKind.Hardware]: { name: 'Hardware' },
    [DimensionChangeKind.User]: { name: 'User' },
    [DimensionChangeKind.Regulatory]: { name: 'Regulatory' },
    [DimensionChangeKind.Runtime]: { name: 'Runtime' },
    [DimensionChangeKind.FormFactor]: { name: 'Form Factor' },
    [DimensionChangeKind.Edition]: { name: 'Edition' },
    [DimensionChangeKind.Network]: { name: 'Network' },
    [DimensionChangeKind.TrustAnchor]: { name: 'Trust Anchor' },
    [DimensionChangeKind.SyncState]: { name: 'Sync State' },
    [DimensionChangeKind.VersionVector]: { name: 'Version' },
  } as Record<string, { name: string }>,
  policy: {
    Required: 'Required',
    Recommended: 'Recommended',
    Informational: 'Informational',
    Unevaluated: 'Not evaluated',
  },
  status: {
    pass: 'Passed',
    warn: 'Warning',
    block: 'Failed',
    unevaluated: 'Not evaluated',
    // F2: regressed label for regression banner list items (council W#56 P3 amendment).
    regressed: 'Regressed',
  },
  actions: {
    forceInstall: 'Force install anyway',
    installAnyway: 'Install anyway',
    continue: 'Continue',
    // F3: dismiss label for regression banner dismiss button (council W#56 P3 amendment).
    dismiss: 'Dismiss',
  },
  recovery: {
    tryThis: 'Try this',
  },
} as const;
