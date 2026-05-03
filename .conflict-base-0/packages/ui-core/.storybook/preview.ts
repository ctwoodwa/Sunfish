import type { Decorator, Preview } from '@storybook/web-components';

const preview: Preview = {
  parameters: {
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-valid-attr-value', enabled: true },
        ],
      },
      options: {
        runOnly: {
          type: 'tag',
          values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'],
        },
      },
    },
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    globalTypes: {
      direction: {
        name: 'Direction',
        description: 'Layout direction',
        defaultValue: 'ltr',
        toolbar: {
          icon: 'globe',
          items: [
            { value: 'ltr', title: 'LTR (en-US)' },
            { value: 'rtl', title: 'RTL (ar-SA)' },
          ],
        },
      },
      reducedMotion: {
        name: 'Reduced motion',
        description: 'Simulate prefers-reduced-motion: reduce',
        defaultValue: 'no-preference',
        toolbar: {
          icon: 'lightning',
          items: [
            { value: 'no-preference', title: 'Motion: full' },
            { value: 'reduce', title: 'Motion: reduced' },
          ],
        },
      },
    },
  },
  decorators: [
    ((story, context) => {
      document.documentElement.setAttribute('dir', context.globals.direction ?? 'ltr');
      applyReducedMotion(context.globals.reducedMotion === 'reduce');
      return story();
    }) as Decorator,
  ],
};

/**
 * Inject (or remove) a global style that effectively disables transitions and animations
 * when the user has set prefers-reduced-motion: reduce. Mirrors the canonical
 * "instant change instead of animated transition" guidance in spec §6 + Plan 4B §6.
 *
 * Components that DON'T already wrap their animations in
 * `@media (prefers-reduced-motion: reduce) { ... }` will still respect this toggle —
 * the injected style overrides via `!important`. Components that DO wrap their
 * animations correctly will already be quiet under this toggle.
 */
function applyReducedMotion(reduce: boolean): void {
  const id = 'sunfish-reduced-motion-shim';
  document.getElementById(id)?.remove();
  if (!reduce) return;

  const style = document.createElement('style');
  style.id = id;
  style.textContent = `
    *, *::before, *::after {
      animation-duration: 0.01ms !important;
      animation-iteration-count: 1 !important;
      transition-duration: 0.01ms !important;
      scroll-behavior: auto !important;
    }
  `;
  document.head.appendChild(style);
}

export default preview;
