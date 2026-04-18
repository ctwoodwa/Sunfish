---
uid: security-overview
title: Security
description: Security practices, XSS prevention, and CSP compliance in Sunfish components.
---

# Security

Sunfish is designed to be safe to embed in production Blazor applications. This article describes the security model, known considerations, and how to report vulnerabilities.

## Vulnerability Reporting

Sunfish is open source, licensed under [The Unlicense](https://unlicense.org). To report a security vulnerability:

1. Open a GitHub Issue on the Sunfish repository and apply the **security** label.
2. If the issue is sensitive, send an email to the project maintainers (address listed in the repository README) so it can be assessed before public disclosure.

There is no formal bug-bounty program. Responsible disclosure is appreciated.

## XSS Prevention

### Editor Component

The `SunfishEditor` component uses a `contenteditable` host element and accepts HTML input. To prevent cross-site scripting (XSS), all user-supplied HTML is sanitized before being applied to the DOM:

- Paste operations run through a cleanup pass that strips disallowed tags and attributes.
- Inline event attributes (`onerror`, `onclick`, etc.) are removed during sanitization.
- The sanitization logic is modelled on DOMPurify-style allowlist filtering — only known-safe tags and attributes are permitted.

Raw user-supplied HTML is never rendered via `MarkupString` without sanitization. If you use `SunfishEditor` and need to persist content, treat the HTML value as untrusted when re-rendering outside of the editor and apply server-side sanitization before storage or display.

### Form Components

All Sunfish form components (`SunfishTextField`, `SunfishNumericInput`, `SunfishSelect`, etc.) bind through Blazor's parameter system. Values are encoded by Blazor's rendering pipeline before they reach the DOM — no direct `innerHTML` injection occurs. Validation error messages displayed by these components are sourced from `DataAnnotationsValidator` or custom validators; they are rendered as text nodes, not raw markup.

### Markup String

Sunfish does not emit `MarkupString` with dynamic user data. If your application passes user-controlled strings to component parameters that accept `RenderFragment` or `MarkupString`, apply sanitization before doing so — that responsibility lies with the consuming application, not the library.

## Content Security Policy

Sunfish is CSP-compatible. Key characteristics:

- **No `unsafe-inline` from JavaScript.** Sunfish's JS interop modules do not call `element.style` to inject inline styles. All styling is applied via CSS class names and `--sunfish-*` CSS custom properties.
- **No `unsafe-eval`.** Sunfish does not use `eval()` or `Function()` constructors.
- **`_content/` paths for JS modules.** JavaScript is loaded from standard `_content/Sunfish.*` static asset paths, compatible with a `script-src 'self'` policy.

The one exception is `SunfishThemeProvider`: it emits a `style` attribute on its root element to inject the active theme's CSS custom property values. This requires either `'unsafe-inline'` for styles or a nonce-based `style-src` policy. See [Content Security Policy](xref:security-csp) for the full header configuration guide.

## Input Sanitization

Beyond the Editor, Sunfish applies the following sanitization practices:

- **Numeric inputs** — `SunfishNumericInput` constrains values to the configured `Min`/`Max` range and rejects non-numeric input at the parse step.
- **Date/time inputs** — `SunfishDatePicker`, `SunfishTimePicker`, and `SunfishDateTimePicker` validate and parse input against configured format strings and culture settings before updating bound values.
- **File uploads** — `SunfishUpload` validates file extensions and MIME types against the `Accept` parameter. Server-side validation of uploaded content remains the responsibility of the consuming application.

## Dependencies

Sunfish is designed to minimize external risk:

- **No third-party JavaScript frameworks.** There is no jQuery, React, Vue, or Angular dependency. All interop is implemented in vanilla ES modules loaded from `_content/` paths.
- **No NPM runtime dependencies.** NPM packages are used only for SCSS compilation at build time; they do not ship to the browser.
- **Self-hosted SVG icons.** Icons are inline `<svg>` elements compiled into the component library — no CDN or icon font request is made at runtime.
- **Minimal NuGet dependencies.** The core library depends only on the Blazor framework packages included with the .NET SDK.

For a current dependency list, see the package metadata on NuGet.org or inspect `Sunfish.Components.csproj` in the repository.
