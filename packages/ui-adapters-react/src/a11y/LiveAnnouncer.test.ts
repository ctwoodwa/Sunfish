import { announce } from './LiveAnnouncer';

describe('ReactLiveAnnouncer', () => {
  // JSDOM provides document.body; we can observe the aria-live region.
  it('announce_polite_setsAriaLivePolite', (done) => {
    announce('Hello', 'polite');
    // requestAnimationFrame fires synchronously in JSDOM via jest fake timers.
    requestAnimationFrame(() => {
      const region = document.body.querySelector('[aria-live]');
      expect(region).not.toBeNull();
      expect(region!.getAttribute('aria-live')).toBe('polite');
      expect(region!.textContent).toBe('Hello');
      done();
    });
  });

  it('announce_assertive_setsAriaLiveAssertive', (done) => {
    announce('Warning!', 'assertive');
    requestAnimationFrame(() => {
      const region = document.body.querySelector('[aria-live]');
      expect(region!.getAttribute('aria-live')).toBe('assertive');
      expect(region!.textContent).toBe('Warning!');
      done();
    });
  });

  it('announce_critical_mapsToAssertive', (done) => {
    announce('Critical alert', 'critical');
    requestAnimationFrame(() => {
      const region = document.body.querySelector('[aria-live]');
      // critical maps to assertive per ILiveAnnouncer contract
      expect(region!.getAttribute('aria-live')).toBe('assertive');
      done();
    });
  });
});
