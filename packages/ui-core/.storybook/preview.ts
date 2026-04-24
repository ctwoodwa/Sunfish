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
    },
  },
  decorators: [
    ((story, context) => {
      document.documentElement.setAttribute('dir', context.globals.direction ?? 'ltr');
      return story();
    }) as Decorator,
  ],
};

export default preview;
