import type { SystemRequirementsResult, DimensionEvaluation } from '../../contracts/SystemRequirements';
import { DimensionPassFail, DimensionPolicyKind } from '../../contracts/SystemRequirements';
import { STRINGS } from './SystemRequirements.strings';

export interface SystemRequirementsRegressionBannerProps {
  result: SystemRequirementsResult;
  previousResult?: SystemRequirementsResult;
  onDismiss?: () => void;
}

function hasRequiredRegression(
  current: SystemRequirementsResult,
  previous: SystemRequirementsResult,
): boolean {
  for (const prev of previous.dimensions) {
    // ADR 0063 A1.8: Informational dimension regressions are NOT banner-worthy.
    if (prev.policy !== DimensionPolicyKind.Required) continue;
    if (prev.outcome !== DimensionPassFail.Pass) continue;

    const curr = current.dimensions.find((d) => d.dimension === prev.dimension);
    if (curr && curr.outcome === DimensionPassFail.Fail) return true;
  }
  return false;
}

function buildRegressedDimensions(
  current: SystemRequirementsResult,
  previous: SystemRequirementsResult,
): DimensionEvaluation[] {
  return current.dimensions.filter((curr) => {
    if (curr.policy !== DimensionPolicyKind.Required) return false;
    if (curr.outcome !== DimensionPassFail.Fail) return false;
    const prev = previous.dimensions.find((d) => d.dimension === curr.dimension);
    return prev?.outcome === DimensionPassFail.Pass;
  });
}

export function SystemRequirementsRegressionBanner({
  result,
  previousResult,
  onDismiss,
}: SystemRequirementsRegressionBannerProps) {
  if (!previousResult || !hasRequiredRegression(result, previousResult)) {
    return null;
  }

  const regressed = buildRegressedDimensions(result, previousResult);

  return (
    // WCAG 2.2 SC 4.1.3: role="alert" implies aria-live="assertive" + aria-atomic="true";
    // F1: do NOT also set aria-live/aria-atomic explicitly — causes double-announcement in NVDA+Firefox.
    <div className="sf-sysreq-regression-banner" role="alert">
      <p className="sf-sysreq-regression-message">{STRINGS.verdict.block}</p>
      <ul role="list" className="sf-sysreq-regression-dimensions">
        {regressed.map((d) => (
          // F2: include "Regressed" label so SR users know WHY the dimension is listed.
          <li key={d.dimension} className="sf-sysreq-regression-item">
            {STRINGS.dimension[d.dimension]?.name ?? d.dimension} — {STRINGS.status.regressed}
          </li>
        ))}
      </ul>
      {onDismiss && (
        // F3: "Dismiss" accurately conveys the action; "Continue" implied install would proceed.
        <button
          type="button"
          className="sf-sysreq-regression-dismiss"
          onClick={onDismiss}
        >
          {STRINGS.actions.dismiss}
        </button>
      )}
    </div>
  );
}
