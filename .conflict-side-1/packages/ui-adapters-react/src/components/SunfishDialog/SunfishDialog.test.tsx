import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SunfishDialog } from './SunfishDialog';
import { CssProviderProvider } from '../../CssProviderContext';
import { BootstrapCssProvider } from '../../providers/BootstrapCssProvider';

describe('SunfishDialog', () => {
  it('does not render when closed', () => {
    render(
      <SunfishDialog open={false} onClose={() => {}} title="Hi">
        <p>Body</p>
      </SunfishDialog>,
    );
    expect(screen.queryByText('Body')).not.toBeInTheDocument();
  });

  it('renders title and body when open', () => {
    render(
      <SunfishDialog open={true} onClose={() => {}} title="Hello">
        <p>Body text</p>
      </SunfishDialog>,
    );
    expect(screen.getByText('Hello')).toBeInTheDocument();
    expect(screen.getByText('Body text')).toBeInTheDocument();
  });

  it('closes on close-button click', () => {
    const onClose = vi.fn();
    render(
      <SunfishDialog open={true} onClose={onClose} title="Hi">
        Body
      </SunfishDialog>,
    );
    fireEvent.click(screen.getByLabelText('Close'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('closes on overlay click by default', () => {
    const onClose = vi.fn();
    render(
      <SunfishDialog open={true} onClose={onClose} title="Hi">
        Body
      </SunfishDialog>,
    );
    fireEvent.click(screen.getByTestId('sf-dialog-overlay'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('closes on Escape by default', () => {
    const onClose = vi.fn();
    render(
      <SunfishDialog open={true} onClose={onClose} title="Hi">
        Body
      </SunfishDialog>,
    );
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('emits Bootstrap dialog slot classes', () => {
    render(
      <CssProviderProvider provider={new BootstrapCssProvider()}>
        <SunfishDialog open={true} onClose={() => {}} title="Hi">
          Body
        </SunfishDialog>
      </CssProviderProvider>,
    );
    expect(document.querySelector('.modal')).not.toBeNull();
    expect(document.querySelector('.modal-dialog')).not.toBeNull();
    expect(document.querySelector('.modal-content')).not.toBeNull();
    expect(document.querySelector('.modal-header')).not.toBeNull();
    expect(document.querySelector('.modal-body')).not.toBeNull();
  });
});
