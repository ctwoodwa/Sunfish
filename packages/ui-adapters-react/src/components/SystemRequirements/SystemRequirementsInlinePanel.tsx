import type { SystemRequirementsResult, DimensionChangeKind, DimensionEvaluation } from '../../contracts/SystemRequirements';
import { SystemRequirementsDimensionRow } from './SystemRequirementsDimensionRow';
import { STRINGS } from './SystemRequirements.strings';

export interface SystemRequirementsInlinePanelProps {
  result: SystemRequirementsResult;
  dimension: DimensionChangeKind;
}

export function SystemRequirementsInlinePanel({
  result,
  dimension,
}: SystemRequirementsInlinePanelProps) {
  const evaluation: DimensionEvaluation | undefined = result.dimensions.find(
    (d) => d.dimension === dimension,
  );
  const dimensionLabel = STRINGS.dimension[dimension]?.name ?? dimension;

  return (
    <details className="sf-sysreq-inline-panel">
      <summary className="sf-sysreq-inline-summary">{dimensionLabel}</summary>
      {evaluation ? (
        <ul role="list" className="sf-sysreq-inline-list">
          <SystemRequirementsDimensionRow eval={evaluation} />
        </ul>
      ) : (
        <p className="sf-sysreq-inline-empty">{STRINGS.status.unevaluated}</p>
      )}
    </details>
  );
}
