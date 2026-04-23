import type { Preview, Decorator } from '@storybook/react';
import { CssProviderProvider } from '../src/CssProviderContext';
import { BootstrapCssProvider } from '../src/providers/BootstrapCssProvider';
import { FluentUICssProvider } from '../src/providers/FluentUICssProvider';
import { MaterialCssProvider } from '../src/providers/MaterialCssProvider';
import type { ICssProvider } from '../src/contracts/ICssProvider';

const providers: Record<string, () => ICssProvider> = {
  bootstrap: () => new BootstrapCssProvider(),
  fluent: () => new FluentUICssProvider(),
  material: () => new MaterialCssProvider(),
};

// Decorator that selects the active provider from:
// 1. A per-story parameter `sunfishProvider` (used by the 3×3 story grid).
// 2. A global toolbar value (manual override in the Storybook UI).
const withSunfishProvider: Decorator = (Story, context) => {
  const toolbarValue = context.globals['sunfishProvider'] as string | undefined;
  const storyValue = context.parameters['sunfishProvider'] as string | undefined;
  const key = toolbarValue ?? storyValue ?? 'bootstrap';
  const factory = providers[key] ?? providers['bootstrap']!;
  const provider = factory();
  return (
    <CssProviderProvider provider={provider}>
      <Story />
    </CssProviderProvider>
  );
};

const preview: Preview = {
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
  },
  globalTypes: {
    sunfishProvider: {
      name: 'Provider',
      description: 'Active Sunfish CSS provider',
      defaultValue: 'bootstrap',
      toolbar: {
        icon: 'paintbrush',
        items: [
          { value: 'bootstrap', title: 'Bootstrap' },
          { value: 'fluent', title: 'Fluent UI' },
          { value: 'material', title: 'Material 3' },
        ],
        dynamicTitle: true,
      },
    },
  },
  decorators: [withSunfishProvider],
};

export default preview;
