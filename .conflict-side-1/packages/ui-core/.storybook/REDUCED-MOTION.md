# Reduced-motion authoring guidance

Plan 4B §6 + spec §6 require every Sunfish component that animates or transitions
to respect the user's `prefers-reduced-motion: reduce` preference.

## The pattern

Wrap all non-essential animation/transition CSS in a media query:

```css
.my-component {
  transition: opacity 200ms ease, transform 200ms ease;
}

@media (prefers-reduced-motion: reduce) {
  .my-component {
    transition: opacity 0ms, transform 0ms;
  }
}
```

For Lit components using `static styles = css\`...\``, the `@media` block goes inside
the same template literal:

```ts
static styles = css`
  :host { display: inline-block; }
  .ripple {
    animation: ripple-out 400ms ease;
  }
  @media (prefers-reduced-motion: reduce) {
    .ripple { animation: none; }
  }
`;
```

## Storybook preview

The reduced-motion toolbar toggle in the Storybook preview applies a global style
shim (`*, *::before, *::after { ... !important }`) when set to **Motion: reduced**.
This catches components that DON'T wrap their animations correctly — they'll still
go quiet under the toggle but flag the missing `@media` block to the author. Components
that DO wrap correctly already obey the user preference natively; the toggle is a
no-op for them.

## What counts as "non-essential"

ARIA WCAG SC 2.3.3 (Animation from Interactions, AAA) and 2.2.2 (Pause, Stop, Hide, A)
distinguish:

- **Decorative animations** (entrance fades, hover scales, ripple effects) — must be
  reduced or removed under the user preference.
- **Functionally essential motion** (e.g., a video loop demonstrating a flow,
  a progress bar position) — may continue to animate. Provide alternative
  presentation if the motion conveys information.

When in doubt, scale the animation down to instant (`duration: 0ms`) under
`prefers-reduced-motion: reduce`; keep the visible-state-change but drop the
travel-between-states animation.

## Testing

The Storybook test-runner does not enforce reduced-motion compliance directly —
detecting a missing `@media` query from JS is unreliable. Authoring discipline +
the Storybook toolbar visual smoke is the current line of defense. CI promotion
to a hard rule is a Plan 5 follow-up.
