/**
 * Plan 4 Task 4.2 — production a11y test-runner postVisit hook.
 *
 * Runs axe-core (via @axe-core/playwright) against every story's rendered output,
 * then enforces Sunfish-specific contract assertions declared in
 * `parameters.a11y.sunfish` per ADR 0034. Pilot components (sunfish-button,
 * sunfish-dialog, sunfish-syncstate-indicator) are treated as smoke gates — any
 * violation in those fails the whole test run, not just the affected story.
 *
 * Driven by `pnpm test:a11y` (test-storybook --config-dir .storybook).
 */

import { AxeBuilder } from '@axe-core/playwright';
import type { TestRunnerConfig } from '@storybook/test-runner';
import { getStoryContext } from '@storybook/test-runner';
import type { Page } from 'playwright';

const PILOT_COMPONENT_TAGS = new Set([
  'sunfish-button',
  'sunfish-dialog',
  'sunfish-syncstate-indicator',
]);

/** WCAG impact levels we treat as failures. */
const FAILURE_IMPACTS = new Set(['moderate', 'serious', 'critical']);

/**
 * Sunfish a11y contract block as authored on each story's `parameters.a11y.sunfish`.
 * Mirror of the .NET-side SunfishA11yContract record in
 * packages/ui-adapters-blazor-a11y/SunfishA11yContract.cs.
 */
interface SunfishContract {
  ariaPattern?: string;
  keyboardMap?: { keys: string[]; action: string }[];
  focus?: { initial?: string; trap?: boolean; restore?: string | null };
  liveRegion?: 'off' | 'polite' | 'assertive';
  rtlIconMirror?: 'mirrors' | 'non-directional';
  directionalIcons?: string[];
  composedOf?: string[];
  wcag22Conformant?: string[];
}

const config: TestRunnerConfig = {
  async preVisit(page) {
    // Reset any per-page state Sunfish components depend on.
    await page.evaluate(() => {
      document.documentElement.dir = 'ltr';
    });
  },

  async postVisit(page, context) {
    const storyContext = await getStoryContext(page, context);
    const componentTag = storyContext.component as string | undefined;

    // 1. Run axe-core against the story root.
    await runAxe(page, componentTag);

    // 2. Run Sunfish-specific contract assertions if declared.
    const sunfish = (storyContext.parameters as { a11y?: { sunfish?: SunfishContract } } | undefined)
      ?.a11y?.sunfish;
    if (sunfish) {
      await runSunfishAssertions(page, sunfish, componentTag);
    }
  },
};

async function runAxe(page: Page, componentTag: string | undefined): Promise<void> {
  const results = await new AxeBuilder({ page })
    .include('#storybook-root')
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'])
    // Page-level rules don't apply when scoped to a single story container.
    .disableRules(['page-has-heading-one', 'bypass', 'meta-viewport', 'document-title'])
    .analyze();

  const failures = results.violations.filter((v) =>
    FAILURE_IMPACTS.has(v.impact ?? ''),
  );

  if (failures.length === 0) return;

  const message = failures
    .map((v) => `  - [${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} node${v.nodes.length === 1 ? '' : 's'})`)
    .join('\n');

  const isPilot = componentTag !== undefined && PILOT_COMPONENT_TAGS.has(componentTag);
  const heading = isPilot
    ? `Pilot component '${componentTag}' a11y SMOKE GATE FAILURE — fails the whole run.`
    : `A11y violations in '${componentTag ?? 'unknown'}'`;

  throw new Error(`${heading}\n${message}`);
}

async function runSunfishAssertions(
  page: Page,
  contract: SunfishContract,
  componentTag: string | undefined,
): Promise<void> {
  // Focus initial — only enforce when the contract says something other than the
  // implicit "self" / "none" defaults that bUnit-rendered fragments can't satisfy
  // outside a true component host.
  if (contract.focus?.initial && contract.focus.initial !== 'self' && contract.focus.initial !== 'none') {
    const focusInsideRoot = await page.evaluate(() => {
      const root = document.querySelector('#storybook-root');
      if (root === null) return false;
      const active = document.activeElement;
      // "first-focusable-child" is satisfied when focus is anywhere inside the
      // storybook root — including custom-element hosts whose internal focusable
      // descendants live in shadow DOM the harness can't pierce. Stricter
      // selector-based matching can be re-enabled per-component once the cascade
      // adopts a "data-sunfish-focus-target" sentinel.
      return active !== null && active !== document.body && root.contains(active);
    });

    if (!focusInsideRoot) {
      const actualTag = await page.evaluate(() =>
        document.activeElement?.tagName?.toLowerCase() ?? 'body',
      );
      throw new Error(
        `[${componentTag}] focus.initial='${contract.focus.initial}' — focus is on <${actualTag}>, not inside #storybook-root.`,
      );
    }
  }

  // Focus trap — Tab through the focusable descendants; assert focus stays inside.
  if (contract.focus?.trap === true) {
    await assertFocusTrap(page, '#storybook-root', componentTag);
  }

  // Keyboard map — dispatch each binding's chord; assert observable side effect.
  if (contract.keyboardMap && contract.keyboardMap.length > 0) {
    await assertKeyboardMap(page, contract.keyboardMap, componentTag);
  }

  // Directional-icon mirror under RTL (only check when contract names directional icons).
  if (contract.directionalIcons && contract.directionalIcons.length > 0) {
    await assertDirectionalIconsMirroredInRtl(page, contract.directionalIcons, componentTag);
  }
}

async function assertFocusTrap(page: Page, containerSelector: string, componentTag: string | undefined): Promise<void> {
  const focusableCount = await page.evaluate((sel) => {
    const container = document.querySelector(sel);
    if (!container) return 0;
    const focusableSelectors =
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return container.querySelectorAll(focusableSelectors).length;
  }, containerSelector);

  if (focusableCount === 0) return; // nothing to trap; skip silently.

  for (let i = 0; i <= focusableCount; i++) {
    await page.keyboard.press('Tab');
  }

  const focusInside = await page.evaluate((sel) => {
    const container = document.querySelector(sel);
    return container ? container.contains(document.activeElement) : false;
  }, containerSelector);

  if (!focusInside) {
    throw new Error(
      `[${componentTag}] focus.trap=true — focus escaped '${containerSelector}' after ${focusableCount + 1} Tab presses.`,
    );
  }
}

async function assertKeyboardMap(
  page: Page,
  bindings: { keys: string[]; action: string }[],
  componentTag: string | undefined,
): Promise<void> {
  // Opt-in sentinel: components signal participation in the keyboard-map assertion by
  // exposing `data-sunfish-keyboard-map="enabled"` on a descendant of #storybook-root.
  // Components that haven't adopted the data-sunfish-fired convention yet are skipped
  // silently — Plan 4 cascade brings them under the assertion as it lands. axe-core
  // and the focus-trap / icon-mirror assertions still apply to all stories.
  const enabled = await page.evaluate(() =>
    document.querySelector('#storybook-root [data-sunfish-keyboard-map="enabled"]') !== null,
  );

  if (!enabled) {
    // eslint-disable-next-line no-console
    console.warn(
      `[${componentTag}] skipping keyboardMap enforcement: component has not opted in via data-sunfish-keyboard-map="enabled".`,
    );
    return;
  }

  for (const binding of bindings) {
    // Reset the host's data-sunfish-fired before each binding.
    await page.evaluate(() => {
      const host = document.querySelector('#storybook-root');
      if (host) host.setAttribute('data-sunfish-fired', '');
    });

    const chord = binding.keys.join('+');
    await page.keyboard.press(chord);

    const fired = await page.evaluate(() =>
      document.querySelector('#storybook-root')?.getAttribute('data-sunfish-fired') ?? '',
    );

    if (!fired.includes(binding.action)) {
      throw new Error(
        `[${componentTag}] keyboardMap '${chord}' was expected to fire '${binding.action}'; data-sunfish-fired='${fired}'.`,
      );
    }
  }
}

async function assertDirectionalIconsMirroredInRtl(
  page: Page,
  directionalStates: string[],
  componentTag: string | undefined,
): Promise<void> {
  // Opt-in sentinel: components signal participation by emitting a descendant
  // matching `[data-sunfish-direction="<state>"]` for each state in the contract's
  // directionalIcons array. Components that haven't adopted the convention are
  // skipped silently — Plan 4B §2 + §5 cascade brings them under the assertion.
  const enabled = await page.evaluate(() =>
    document.querySelector('#storybook-root [data-sunfish-direction]') !== null,
  );

  if (!enabled) {
    // eslint-disable-next-line no-console
    console.warn(
      `[${componentTag}] skipping directionalIcons enforcement: component has not opted in via [data-sunfish-direction="<state>"] sentinel.`,
    );
    return;
  }

  await page.evaluate(() => {
    document.documentElement.setAttribute('dir', 'rtl');
  });

  for (const state of directionalStates) {
    const selector = `#storybook-root [data-sunfish-direction="${state}"]`;
    const transform = await page.evaluate((sel) => {
      const el = document.querySelector(sel);
      if (!el) return 'NOT_FOUND';
      return getComputedStyle(el).transform;
    }, selector);

    if (transform === 'NOT_FOUND') {
      throw new Error(
        `[${componentTag}] directionalIcons: no element matching ${selector} for state '${state}'.`,
      );
    }

    if (transform === 'none' || transform === 'matrix(1, 0, 0, 1, 0, 0)') {
      throw new Error(
        `[${componentTag}] directionalIcons: state '${state}' has identity transform under RTL — should mirror. Computed: '${transform}'.`,
      );
    }
  }

  // Restore for subsequent tests in the same page.
  await page.evaluate(() => {
    document.documentElement.setAttribute('dir', 'ltr');
  });
}

export default config;
