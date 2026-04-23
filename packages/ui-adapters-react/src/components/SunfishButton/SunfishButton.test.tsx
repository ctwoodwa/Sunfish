import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SunfishButton } from './SunfishButton';
import { CssProviderProvider } from '../../CssProviderContext';
import { BootstrapCssProvider } from '../../providers/BootstrapCssProvider';
import { FluentUICssProvider } from '../../providers/FluentUICssProvider';
import { MaterialCssProvider } from '../../providers/MaterialCssProvider';
import { ButtonVariant } from '../../contracts/ButtonVariant';

describe('SunfishButton', () => {
  it('renders children text', () => {
    render(<SunfishButton>Click me</SunfishButton>);
    expect(screen.getByRole('button', { name: 'Click me' })).toBeInTheDocument();
  });

  it('fires onClick when clicked', () => {
    const onClick = vi.fn();
    render(<SunfishButton onClick={onClick}>Press</SunfishButton>);
    fireEvent.click(screen.getByRole('button', { name: 'Press' }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('emits Bootstrap classes by default', () => {
    render(<SunfishButton variant={ButtonVariant.Primary}>Go</SunfishButton>);
    const btn = screen.getByRole('button', { name: 'Go' });
    expect(btn.className).toContain('btn');
    expect(btn.className).toContain('btn-primary');
  });

  it('emits Fluent classes under the Fluent provider', () => {
    render(
      <CssProviderProvider provider={new FluentUICssProvider()}>
        <SunfishButton variant={ButtonVariant.Primary}>Go</SunfishButton>
      </CssProviderProvider>,
    );
    const btn = screen.getByRole('button', { name: 'Go' });
    expect(btn.className).toContain('sf-button');
    expect(btn.className).toContain('sf-button--primary');
  });

  it('emits Material classes under the Material provider', () => {
    render(
      <CssProviderProvider provider={new MaterialCssProvider()}>
        <SunfishButton variant={ButtonVariant.Danger}>Delete</SunfishButton>
      </CssProviderProvider>,
    );
    const btn = screen.getByRole('button', { name: 'Delete' });
    expect(btn.className).toContain('sf-button');
    expect(btn.className).toContain('sf-button--danger');
  });

  it('applies the disabled modifier when disabled', () => {
    render(
      <CssProviderProvider provider={new BootstrapCssProvider()}>
        <SunfishButton disabled>Nope</SunfishButton>
      </CssProviderProvider>,
    );
    const btn = screen.getByRole('button', { name: 'Nope' });
    expect(btn).toBeDisabled();
    expect(btn.className).toContain('disabled');
  });
});
