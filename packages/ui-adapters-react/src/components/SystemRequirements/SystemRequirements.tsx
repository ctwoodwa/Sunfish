import { useId } from 'react';
import type { SystemRequirementsResult, SystemRequirementsRenderMode as SystemRequirementsRenderModeType } from '../../contracts/SystemRequirements';
import { OverallVerdict, SystemRequirementsRenderMode } from '../../contracts/SystemRequirements';
import { SunfishButton } from '../SunfishButton/SunfishButton';
import { ButtonVariant } from '../../contracts/ButtonVariant';
import { SystemRequirementsDimensionRow } from './SystemRequirementsDimensionRow';
import { STRINGS } from './SystemRequirements.strings';

export interface SystemRequirementsProps {
  result: SystemRequirementsResult;
  mode: SystemRequirementsRenderModeType;
  onForceInstall?: () => void;
  onInstallAnyway?: () => void;
  onContinue?: () => void;
  bundleId: string;
}

function VerdictBanner({
  overall,
  titleId,
}: {
  overall: SystemRequirementsResult['overall'];
  titleId: string;
}) {
  let text: string;
  let className: string;
  if (overall === OverallVerdict.Pass) {
    text = STRINGS.verdict.pass;
    className = 'sf-sysreq-verdict sf-sysreq-verdict--pass';
  } else if (overall === OverallVerdict.WarnOnly) {
    text = STRINGS.verdict.warn;
    className = 'sf-sysreq-verdict sf-sysreq-verdict--warn';
  } else {
    text = STRINGS.verdict.block;
    className = 'sf-sysreq-verdict sf-sysreq-verdict--block';
  }
  // B2: no role="status" — verdict is static load-time content, not a dynamic status message.
  return (
    <div className={className} aria-describedby={titleId}>
      <span>{text}</span>
    </div>
  );
}

function FooterAction({
  overall,
  onForceInstall,
  onInstallAnyway,
  onContinue,
}: Pick<SystemRequirementsProps, 'onForceInstall' | 'onInstallAnyway' | 'onContinue'> & {
  overall: SystemRequirementsResult['overall'];
}) {
  // W2: accessible name comes from children; no redundant aria-label.
  // B-API-1: disabled when handler is absent so the button is not silently inert.
  if (overall === OverallVerdict.Block) {
    return (
      <SunfishButton
        variant={ButtonVariant.Danger}
        onClick={onForceInstall}
        disabled={!onForceInstall}
      >
        {STRINGS.actions.forceInstall}
      </SunfishButton>
    );
  }
  if (overall === OverallVerdict.WarnOnly) {
    return (
      <SunfishButton
        variant={ButtonVariant.Warning}
        onClick={onInstallAnyway}
        disabled={!onInstallAnyway}
      >
        {STRINGS.actions.installAnyway}
      </SunfishButton>
    );
  }
  return (
    <SunfishButton
      variant={ButtonVariant.Primary}
      onClick={onContinue}
      disabled={!onContinue}
    >
      {STRINGS.actions.continue}
    </SunfishButton>
  );
}

function PreInstallFullPage({
  result,
  bundleId,
  onForceInstall,
  onInstallAnyway,
  onContinue,
}: Omit<SystemRequirementsProps, 'mode'>) {
  // B-ARCH-1: useId() prevents duplicate IDs when multiple instances mount.
  const titleId = useId();
  return (
    <main
      aria-labelledby={titleId}
      className="sf-sysreq-fullpage"
      data-sf-bundle-id={bundleId}
    >
      <h1 id={titleId} className="sf-sysreq-title">
        {STRINGS.title.preInstall}
      </h1>
      <VerdictBanner overall={result.overall} titleId={titleId} />
      <ul role="list" className="sf-sysreq-dimensions" aria-label="System requirement dimensions">
        {result.dimensions.map((d) => (
          <SystemRequirementsDimensionRow key={d.dimension} eval={d} />
        ))}
      </ul>
      <footer className="sf-sysreq-footer">
        <FooterAction
          overall={result.overall}
          onForceInstall={onForceInstall}
          onInstallAnyway={onInstallAnyway}
          onContinue={onContinue}
        />
      </footer>
    </main>
  );
}

export function SystemRequirements({
  result,
  mode,
  onForceInstall,
  onInstallAnyway,
  onContinue,
  bundleId,
}: SystemRequirementsProps) {
  // W-API-1: compare via const reference, not string literal.
  if (mode === SystemRequirementsRenderMode.PreInstallFullPage) {
    return (
      <PreInstallFullPage
        result={result}
        bundleId={bundleId}
        onForceInstall={onForceInstall}
        onInstallAnyway={onInstallAnyway}
        onContinue={onContinue}
      />
    );
  }
  // W-ARCH-1: throw for unimplemented modes so Phase 3 regressions fail loudly.
  throw new Error(
    `SystemRequirements: mode '${mode}' is not yet implemented. ` +
    `PostInstallInlineExplanation and PostInstallRegressionBanner ship in Phase 3.`,
  );
}
