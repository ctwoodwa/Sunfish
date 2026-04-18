---
uid: security-csp
title: Content Security Policy
description: CSP header configuration for Sunfish Blazor applications.
---

# Content Security Policy

Sunfish is designed to work within a Content Security Policy (CSP). This article explains how each CSP directive applies to Sunfish and provides a recommended starting configuration.

## Directive Reference

### `style-src`

```
style-src 'self';
```

Compiled provider stylesheets (e.g., `sunfish-fluentui.css`) are served from `_content/` paths under the application origin, so `'self'` is sufficient for all component styles.

**Exception — `SunfishThemeProvider` inline styles:**

`SunfishThemeProvider` emits a `style` attribute on its root element to inject the active theme's CSS custom property block. This is an inline style, which is blocked by `style-src 'self'` without additional configuration.

To accommodate this, choose one of:

- Add `'unsafe-inline'` to `style-src`. This is the simplest option but weakens the style policy.
- Use a `nonce`-based policy. Generate a per-request nonce, pass it to `SunfishThemeProvider` via the `Nonce` parameter, and include it in the CSP header:

  ```
  style-src 'self' 'nonce-{your-nonce}';
  ```

> [!NOTE]
> The nonce approach is recommended for applications with strict CSP requirements. See the ASP.NET Core documentation for guidance on generating and injecting nonces in Blazor Server applications.

### `script-src`

```
script-src 'self';
```

Sunfish loads its JavaScript interop modules from `_content/Sunfish.*` paths, which resolve to the application origin. No external script CDN is required. `'self'` is sufficient.

Sunfish does not use `eval()` or dynamic `Function()` constructors, so `'unsafe-eval'` is not needed.

### `img-src`

```
img-src 'self' data:;
```

Sunfish icons are rendered as inline `<svg>` elements in component markup — they are not loaded via `<img>` tags or external URLs. No external image CDN is required.

The `data:` scheme may be needed if your application or any third-party component uses base64-encoded data URIs for images. It is not required by Sunfish itself.

### `font-src`

The font requirement depends on your chosen provider:

| Provider | Default Font | `font-src` requirement |
|----------|-------------|------------------------|
| FluentUI | Segoe UI (system font) | `'self'` — no web font request |
| Bootstrap | System font stack | `'self'` — no web font request |
| Material 3 | Roboto | `'self'` if self-hosted; `https://fonts.gstatic.com` if using Google Fonts CDN |

For Material 3 with Google Fonts:

```
font-src 'self' https://fonts.gstatic.com;
```

To avoid the external font dependency, download Roboto and serve it from your `wwwroot/` folder. Add a `@font-face` declaration in a local CSS file and remove the Google Fonts CDN links.

### `connect-src`

```
connect-src 'self';
```

Sunfish does not make outbound HTTP requests from JavaScript. All server communication goes through the Blazor SignalR connection (for Server rendering) or WebAssembly fetch (for WASM). `'self'` is sufficient.

### `frame-src` and `object-src`

```
frame-src 'none';
object-src 'none';
```

Sunfish does not use iframes or plugin objects. Setting both to `'none'` is safe.

## Recommended CSP Header

The following header covers a Sunfish Blazor Server application using the FluentUI or Bootstrap provider, with `SunfishThemeProvider` inline styles permitted via `'unsafe-inline'`:

```
Content-Security-Policy:
  default-src 'self';
  script-src 'self';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data:;
  font-src 'self';
  connect-src 'self' wss:;
  frame-src 'none';
  object-src 'none';
```

> [!NOTE]
> `connect-src` includes `wss:` to allow the Blazor Server SignalR WebSocket connection. Scope this to your specific host in production (e.g., `wss://myapp.example.com`).

### Stricter Configuration (nonce-based)

For applications that cannot accept `'unsafe-inline'` for styles, use a nonce:

```
Content-Security-Policy:
  default-src 'self';
  script-src 'self';
  style-src 'self' 'nonce-{nonce}';
  img-src 'self' data:;
  font-src 'self';
  connect-src 'self' wss:;
  frame-src 'none';
  object-src 'none';
```

Pass the same nonce value to `SunfishThemeProvider`:

```razor
<SunfishThemeProvider Nonce="@_cspNonce">
    @Body
</SunfishThemeProvider>
```

### Material 3 with Google Fonts CDN

If using Material 3 with Google Fonts, extend `style-src` and `font-src`:

```
style-src 'self' 'unsafe-inline' https://fonts.googleapis.com;
font-src 'self' https://fonts.gstatic.com;
```

## Applying CSP in ASP.NET Core

Add the CSP header in `Program.cs` using a middleware policy or response headers:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; connect-src 'self' wss:; " +
        "frame-src 'none'; object-src 'none';"
    );
    await next();
});
```

For production applications, consider using the [NetEscapades.AspNetCore.SecurityHeaders](https://github.com/andrewlock/NetEscapades.AspNetCore.SecurityHeaders) library for a type-safe, policy-builder approach.

## See Also

- [Security Overview](xref:security-overview)
- [MDN: Content Security Policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP)
