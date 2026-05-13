# Platform A11y Bindings — Sunfish Shared Design System

Per-primitive platform API mapping for the W#46 a11y adapter layer ([ADR 0077 §4](../../docs/adrs/0077-shared-design-system.md)).

## ILiveAnnouncer

| Primitive | Blazor (WebView) | React (WebView) | MAUI Windows | MAUI MacCatalyst | iOS | Android |
|---|---|---|---|---|---|---|
| **Announce (Polite)** | `aria-live="polite"` via `sunfish-a11y.js` singleton | `aria-live="polite"` DOM singleton | `AutomationNotification`<br>`ActionCompleted + CurrentThenMostRecent` | `UIAccessibility.PostNotification(Announcement, NSString)` | deferred | deferred |
| **Announce (Assertive)** | `aria-live="assertive"` | `aria-live="assertive"` | `AutomationNotification`<br>`Other + ImportantMostRecent` | same as Polite | deferred | deferred |
| **Announce (Critical)** | maps to assertive | maps to assertive | `AutomationNotification`<br>`Other + ImportantAll` | same as Polite | deferred | deferred |
| **Swallows errors** | `JSDisconnectedException`, `JSException`, `TaskCanceledException` | try/catch | n/a | n/a | — | — |

Blazor implementation: `BlazorLiveAnnouncer` · `sunfish-a11y.js#announce`  
MAUI implementation: `MauiLiveAnnouncer` + `IPlatformA11yNotifier`

## IFocusTrap

| Primitive | Blazor (WebView) | React (WebView) | MAUI Windows | MAUI MacCatalyst | iOS | Android |
|---|---|---|---|---|---|---|
| **EnterAsync** | `trapFocus(containerId)` via JS module | `useEffect` + `document.addEventListener('keydown')` | `UIElement.Focus(FocusState.Keyboard)` | `UIView.BecomeFirstResponder` | deferred | deferred |
| **ExitAsync** | `releaseFocus(containerId)` | cleanup on `active=false` | programmatic restore | `BecomeFirstResponder` on prior | deferred | deferred |
| **Tab/Shift+Tab cycle** | `sunfish-a11y.js#handleTrapKeyDown` | JS keydown handler | n/a | n/a | — | — |
| **Escape exit** | `sunfish-a11y.js` Escape handler → `releaseFocus` | `onEscape` prop callback | Consumer MUST call ExitAsync on platform gesture | Consumer MUST call ExitAsync | — | — |
| **Prior focus restore** | `trap.prior.focus()` in `releaseFocus` | `priorFocus.focus()` in `useEffect` cleanup | `_priorFocusedEl.Focus(Programmatic)` | `_priorFirstResponder.BecomeFirstResponder()` | — | — |
| **Re-entry guard** | `_activeContainerId is not null` check | `active` prop idempotent | `_isActive` guard | `_isActive` guard | — | — |

WCAG SC 2.1.2 (No Keyboard Trap): every trap provides an Escape route.  
WCAG SC 2.4.3 (Focus Order): prior focus restored on exit.

Blazor implementation: `BlazorFocusTrap` · `sunfish-a11y.js#trapFocus / releaseFocus`  
MAUI implementation: `MauiFocusTrap`

## IFirstAidContract / FirstAidRenderer

| Primitive | Blazor | React | MAUI |
|---|---|---|---|
| **aria-describedby** | `<div aria-describedby="@_helpId">` | `<div aria-describedby={helpId}>` | Deferred — BlazorFirstAidRenderer used in MAUI Blazor Hybrid |
| **Visually-hidden help text** | `<span class="sf-sr-only" id="@_helpId">` | `<span className="sf-sr-only" id={helpId}>` | — |
| **Next-action hint** | `<span class="sf-sr-only sf-first-aid__hint">` (conditional) | same | — |
| **Stable ID** | `Guid.NewGuid("N")` per instance | `useId()` (React 18+) | — |
| **role="region" guard** | NOT used (requires accessible name per ARIA spec) | NOT used | — |

Blazor implementation: `BlazorFirstAidRenderer.razor` + `BlazorFirstAidRenderer.razor.cs`  
React implementation: `FirstAidRenderer.tsx`

## IConformanceRegistry

| Primitive | Blazor | React | MAUI |
|---|---|---|---|
| **Storage** | `DefaultConformanceRegistry` (ui-core · `ConcurrentDictionary`) | `ConformanceRegistry` (TypeScript · `Map<string, Map>`) | Shares Blazor impl via MAUI Blazor Hybrid |
| **Register** | `void Register(ConformanceDeclaration)` — idempotent on (LocationId, SurfaceId) | `register(decl)` — idempotent | — |
| **ForLocation** | `IReadOnlyList<ConformanceDeclaration>` | `ConformanceDeclaration[]` | — |
| **DI registration** | `AddSunfishSharedDesignSystem()` | `ConformanceRegistryProvider` | — |

## Deferred (iOS / Android MAUI)

iOS and Android MAUI adapters are explicitly out of scope for W#46 (ADR 0077 §NOT in scope).  
MauiFocusTrap and MauiLiveAnnouncer compile on non-MAUI TFMs (via `#if` guards) but are no-ops.  
A future W#XX iOS/Android wave will implement `UIAccessibilityElement` (iOS) and `AccessibilityManager` (Android) adapters.
