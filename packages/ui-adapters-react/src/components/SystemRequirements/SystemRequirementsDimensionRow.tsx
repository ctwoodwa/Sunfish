import type { DimensionEvaluation } from '../../contracts/SystemRequirements';
import { DimensionPassFail, DimensionPolicyKind } from '../../contracts/SystemRequirements';
import { STRINGS } from './SystemRequirements.strings';

export interface SystemRequirementsDimensionRowProps {
  eval: DimensionEvaluation;
}

function resolveStatusLabel(
  outcome: DimensionEvaluation['outcome'],
  policy: DimensionEvaluation['policy'],
): string {
  if (outcome === DimensionPassFail.Pass) return STRINGS.status.pass;
  if (outcome === DimensionPassFail.Unevaluated) return STRINGS.status.unevaluated;
  // Fail — severity depends on policy
  if (policy === DimensionPolicyKind.Required) return STRINGS.status.block;
  return STRINGS.status.warn;
}

// B3: icon is purely decorative; status is conveyed via visually-hidden text.
const srOnly: React.CSSProperties = {
  position: 'absolute', width: '1px', height: '1px', padding: 0,
  margin: '-1px', overflow: 'hidden', clip: 'rect(0,0,0,0)',
  whiteSpace: 'nowrap', borderWidth: 0,
};

function StatusIcon({ outcome, policy }: { outcome: DimensionEvaluation['outcome']; policy: DimensionEvaluation['policy'] }) {
  let symbol: string;
  if (outcome === DimensionPassFail.Pass) {
    symbol = '✓';
  } else if (outcome === DimensionPassFail.Unevaluated) {
    symbol = '–';
  } else if (policy === DimensionPolicyKind.Required) {
    symbol = '✗';
  } else {
    symbol = '⚠';
  }
  return (
    <span aria-hidden="true" className="sf-sysreq-status-icon">
      {symbol}
    </span>
  );
}

export function SystemRequirementsDimensionRow({ eval: evaluation }: SystemRequirementsDimensionRowProps) {
  const dimensionName =
    STRINGS.dimension[evaluation.dimension]?.name ?? evaluation.dimension;
  const policyLabel =
    STRINGS.policy[evaluation.policy as keyof typeof STRINGS.policy] ?? evaluation.policy;
  const statusLabel = resolveStatusLabel(evaluation.outcome, evaluation.policy);

  return (
    // W1: <li> already has implicit listitem role; role="listitem" would be redundant.
    <li className="sf-sysreq-dimension-row">
      {/* B3: status conveyed as visually-hidden text so SR doesn't double-announce icon + name */}
      <span style={srOnly}>{statusLabel}: </span>
      <StatusIcon outcome={evaluation.outcome} policy={evaluation.policy} />
      <span className="sf-sysreq-dimension-name">{dimensionName}</span>
      <span className="sf-sysreq-dimension-policy">{policyLabel}</span>
      {evaluation.detail && (
        <span className="sf-sysreq-dimension-detail">{evaluation.detail}</span>
      )}
      {evaluation.operatorRecoveryAction && (
        // W3: recovery action wrapped as named group for programmatic association.
        <div
          className="sf-sysreq-recovery"
          role="group"
          aria-label={STRINGS.recovery.tryThis}
        >
          <span aria-hidden="true" className="sf-sysreq-recovery-header">
            {STRINGS.recovery.tryThis}
          </span>
          <span className="sf-sysreq-recovery-key">
            {evaluation.operatorRecoveryAction.actionKey}
          </span>
        </div>
      )}
    </li>
  );
}
