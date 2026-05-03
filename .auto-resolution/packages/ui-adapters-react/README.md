# @sunfish/ui-adapters-react

React adapter for Sunfish UI-core contracts. Parity target for
[`@sunfish/ui-adapters-blazor`](../ui-adapters-blazor/).

**Status:** Wave 3.5 scaffold. Three proof-of-concept components
(`SunfishButton`, `SunfishDataGrid`, `SunfishDialog`) wired through three
CSS providers (Bootstrap 5 / Fluent UI v9 / Material 3). Full component
parity is deferred to future waves per
[ADR 0030](../../docs/adrs/0030-react-adapter-scaffolding.md).

## Relevant ADRs

- [ADR 0014 — Adapter Parity Policy](../../docs/adrs/0014-adapter-parity-policy.md)
- [ADR 0023 — Dialog Provider Slot Methods](../../docs/adrs/0023-dialog-provider-slot-methods.md)
- [ADR 0024 — Button Variant Enum Expansion](../../docs/adrs/0024-button-variant-enum-expansion.md)
- [ADR 0025 — CSS Class Prefix Policy](../../docs/adrs/0025-css-class-prefix-policy.md)
- [ADR 0030 — React Adapter Scaffolding](../../docs/adrs/0030-react-adapter-scaffolding.md) (this package)

## Install (consumer setup, once published)

```bash
npm install @sunfish/ui-adapters-react react react-dom
```

## Usage

Wrap your app in a `CssProviderProvider` to select the active skin. Every
Sunfish component reads classes from the provider in context.

```tsx
import {
  CssProviderProvider,
  FluentUICssProvider,
  SunfishButton,
} from '@sunfish/ui-adapters-react';

export default function App() {
  return (
    <CssProviderProvider provider={new FluentUICssProvider()}>
      <SunfishButton onClick={() => console.log('clicked')}>
        Hello Sunfish
      </SunfishButton>
    </CssProviderProvider>
  );
}
```

## Commands

| Command | Description |
|---|---|
| `npm install` | Install dependencies. |
| `npm run build` | Build library (Vite library mode → `dist/`). |
| `npm test` | Run Vitest test suite. |
| `npm run typecheck` | Type-check without emit. |
| `npm run storybook` | Launch Storybook at <http://localhost:6006>. |
| `npm run build-storybook` | Build static Storybook to `storybook-static/`. |

## Scope of the scaffold

This Wave 3.5 package implements **only** the Button / DataGrid / Dialog
slices of the provider contract. The canonical C# surface
(`packages/ui-core/Contracts/ISunfishCssProvider.cs`) has ~150 methods;
this adapter ports ~20. Subsequent waves will expand the contract and
add the remaining Blazor components.

## Framework independence note

This package is **NPM-only** — there is no NuGet artifact and it is not
listed in `Sunfish.slnx`. The .NET build is unaffected by `npm install`
here; `node_modules/` is covered by the root `.gitignore`.
