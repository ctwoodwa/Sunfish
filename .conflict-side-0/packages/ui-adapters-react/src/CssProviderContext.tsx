import { createContext, useContext, type ReactNode } from 'react';
import type { ICssProvider } from './contracts/ICssProvider';
import { BootstrapCssProvider } from './providers/BootstrapCssProvider';

/**
 * React context that hosts the active `ICssProvider`. Every `Sunfish*`
 * component reads its CSS classes through the provider in this context,
 * so swapping provider at the root of the tree reskins all descendants
 * without component-level changes.
 *
 * Default value is the Bootstrap provider so components render with a
 * valid skin even when no `<CssProviderProvider>` is mounted — matches
 * Blazor's default-DI behavior.
 */
const defaultProvider: ICssProvider = new BootstrapCssProvider();

export const CssProviderContext = createContext<ICssProvider>(defaultProvider);

export interface CssProviderProviderProps {
  provider: ICssProvider;
  children: ReactNode;
}

/**
 * Wrap a subtree to select the active CSS provider.
 *
 * @example
 *   <CssProviderProvider provider={new FluentUICssProvider()}>
 *     <SunfishButton>Hello</SunfishButton>
 *   </CssProviderProvider>
 */
export function CssProviderProvider({ provider, children }: CssProviderProviderProps) {
  return <CssProviderContext.Provider value={provider}>{children}</CssProviderContext.Provider>;
}

/**
 * Hook that returns the active `ICssProvider` from context.
 */
export function useCssProvider(): ICssProvider {
  return useContext(CssProviderContext);
}
