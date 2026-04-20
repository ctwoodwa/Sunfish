# Cal.diy Research: Scheduling Platform Reference

**Source:** https://github.com/calcom/cal.diy  
**Evaluated:** 2026-04-20  
**License:** MIT (fully open-source, no Enterprise Edition code)  
**Stars:** ~41,500 | **Status:** Actively maintained

---

## Summary

Cal.diy is a fully open-source scheduling platform forked from Cal.com with all proprietary "Enterprise Edition" code removed. It provides booking pages, calendar sync, availability management, team scheduling, webhooks, and a published React embed/atoms SDK. Everything is MIT-licensed.

**Relevance to Sunfish:**
- Strong lessons-learned candidate for component architecture patterns (CVA variants, headless primitives, platform atoms SDK design)
- Viable integration target for scheduling functionality via iframe embed (Blazor-compatible) or .NET REST client (OpenAPI spec available)
- MIT license — no legal blockers for any usage model

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 16, React 18/19, Tailwind CSS 4 |
| Component primitives | Radix UI (Dialog, Popover, Tooltip, DropdownMenu, etc.) |
| Variant management | `class-variance-authority` (CVA) |
| Server state | TanStack Query v5 |
| Internal API | tRPC |
| External API | NestJS REST (OpenAPI/Swagger, date-versioned) |
| ORM | Prisma + PostgreSQL 13+ |
| Auth | next-auth v4 (web), Passport.js multi-strategy (API) |
| Monorepo | Turborepo + Yarn v4 workspaces |
| Linting | Biome (replaced ESLint/Prettier) |
| Testing | Vitest (unit), Playwright (E2E) |

---

## Core Scheduling Features

- **Event types** — booking link configuration (duration, buffers, recurring, seats, payments, custom fields)
- **Booking flow** — slot selection → intake form → confirmation
- **Availability/schedules** — working hours, date overrides, buffer management
- **Calendar sync** — Google Calendar, Outlook, Apple Calendar (read busy times, write new events)
- **Conferencing** — Zoom, Google Meet, Daily.co, Teams via app-store plugins
- **Routing forms** — Typeform-style intake that routes to event types
- **Webhooks** — Full booking lifecycle events (created, rescheduled, cancelled, confirmed, payment, OOO, etc.)
- **Out-of-office** — OOO periods with delegation
- **App store** — ~50 integrations (CRMs, payments, video, analytics, automation)
- **Booking page theming** — CSS custom property-based dark/light/custom themes

---

## Architecture

### Monorepo Structure

```
apps/
  web/          — Next.js main application
  api/v2/       — NestJS REST API (platform API, OpenAPI spec)

packages/
  ui/           — Internal design system (~40 React components, not published)
  prisma/       — Shared schema + migrations
  trpc/         — tRPC routers
  features/     — Feature-scoped business logic (bookings, calendars, webhooks, slots, etc.)
  platform/
    atoms/      — Published React SDK components (the embed/integration surface)
  embeds/
    embed-core/ — Vanilla JS embed runtime (iframe manager)
    embed-react/ — React wrapper for embed-core
    embed-snippet/ — Minimal CDN loader snippet
  app-store/    — Integration plugins
  emails/       — React Email templates
```

### Key Patterns

**Feature-scoped packages** — each domain (bookings, calendars, webhooks, slots) is a self-contained package with service/repository/interface layering. Maps directly to Sunfish's layered package model.

**Dual API surface** — tRPC for the web app's internal API; NestJS REST for external/platform consumers. API v2 uses date-stamped versioning (`slots-2024-04-15`, `slots-2024-09-04`) for clean breaking-change isolation.

**Repository pattern in NestJS** — `UsersRepository`, `OAuthClientRepository`, etc. Explicit separation of data access from business logic.

---

## Embeddability

Three distinct embed modes via `@calcom/embed-core` (vanilla JS, no React dependency at embedding layer):

| Mode | How |
|---|---|
| **Inline** | Renders in a `<div>` via `<cal-inline>` custom element |
| **Modal** | Popup triggered by a link/button |
| **Floating button** | Persistent floating CTA → modal |

**Embed architecture:**
- Script tag loads the `Cal` global object
- Namespace-scoped iframes — multiple embeds on one page never interfere
- Instruction queue — commands queue before iframe is ready, flush on load
- Bidirectional typed postMessage protocol (`{ type, namespace, fullType, data }`)
- Preloading optimization — can pre-warm iframe before user interaction
- Theme support via CSS custom properties

**Blazor integration path:** Inject the embed-core snippet via `IJSRuntime`. The iframe-based embed has zero React dependency at the embedding layer — fully viable in a Blazor app.

---

## REST API (NestJS v2)

OpenAPI/Swagger-documented REST API with versioned routes.

**Key resource groups:** `event-types`, `slots`, `bookings`, `schedules`, `calendars`, `webhooks`, `users`, `teams`, `oauth-clients`, `credentials`, `apps`

**Auth strategies (multi-strategy via Passport.js):**
1. API key — `Bearer <api_key>` (SHA-256 hashed in DB)
2. OAuth2 client credentials — `x-cal-client-id` + `x-cal-secret-key` headers
3. NextAuth JWT — for same-origin/embedded web usage

**.NET integration path:** NSwag or Kiota can generate a typed .NET client from the OpenAPI spec.

---

## Platform Atoms SDK

`packages/platform/atoms` — published React SDK for embedding scheduling UI in any React app.

**Exported components:** `Booker`, `AvailabilitySettings`, `CalendarSettings`, `CalendarView`, `EventTypeSettings`, `CreateEventType`, `ListEventTypes`, `CreateSchedule`, `ListSchedules`, `ConferencingAppsSettings`, `DestinationCalendarSettings`, `SelectedCalendarsSettings`, `TroubleShooter`, `BookerEmbed`

**Exported hooks:** `useBookings`, `useBooking`, `useCancelBooking`, `useAvailableSlots`, `useMe`, `useEventTypes`, `useConnectedCalendars`, `useAtomsContext`

Wrapped in `CalProvider` / `CalOAuthProvider` context for auth token injection. Hooks expose data operations independently of UI — consumers can build custom UI while reusing Cal's data logic.

---

## Lessons Learned for Sunfish

### A. CVA + Tailwind for Component Variants
Cal.diy's `Button` uses `cva()` from `class-variance-authority` to declare all visual variants (size, color, shape) as a typed schema. Eliminates prop explosion while keeping a single source of truth. Directly applicable to Sunfish's React adapter variant management.

### B. Radix UI as Headless Primitive Layer
Every interactive component builds on Radix UI primitives (Dialog, Popover, Tooltip, DropdownMenu), adding only Tailwind styling. Free accessibility (ARIA, focus management, keyboard nav) without owning that complexity. Strong model for Sunfish's React adapter approach to complex interactive components.

### C. Namespace-Scoped Embed Communication
The postMessage protocol uses typed, namespaced events (`{ type, namespace, fullType, data }`). Multiple embeds on one page never interfere. Model pattern for any cross-frame component communication Sunfish might need.

### D. Instruction Queue Before Initialization
Commands queue before the embed is ready, then flush on load. Generalizable "deferred instruction queue" pattern for any async component initialization.

### E. Date-Stamped API Versioning
API v2 uses date-based versioning (`slots-2024-04-15`) rather than numeric majors. Each date directory contains the full handler for that contract version. Clean breaking-change isolation without version proliferation.

### F. Feature-Scoped Package Layering
Each feature domain is a self-contained package with service/repository/interface layers. Directly mirrors Sunfish's intended package architecture — a strong reference for how to structure feature packages.

### G. Platform OAuth Provider Pattern
`CalProvider`/`CalOAuthProvider` as React context wrappers that inject auth tokens into all child atoms. Elegant model for SDK distribution where a host app owns auth but components consume it via context. Relevant if Sunfish builds a hosted-auth model for its component SDK.

### H. Headless Core + Optional UI
The atoms `index.ts` exports both components and hooks. Hooks expose the underlying data operations independently — consumers use only the hooks and build custom UI. This "headless core + optional UI" is the pattern Sunfish should aim for in its platform packages.

### I. App-Store Plugin Architecture
Self-contained integration plugins with typed credential schemas, metadata, and lifecycle hooks. Good model for Sunfish extensibility if it ever supports third-party integrations.

### J. Biome over ESLint/Prettier
Fully migrated in a large TypeScript monorepo. Faster, simpler config. Worth evaluating for Sunfish's JavaScript tooling layer.

---

## Integration Feasibility Matrix

| Integration Path | Feasibility | Notes |
|---|---|---|
| Iframe embed in Blazor | ✅ High | Vanilla JS embed-core, no React dep at embedding layer |
| .NET REST client | ✅ High | OpenAPI spec → NSwag/Kiota generated client |
| React atoms in Sunfish blocks | ⚠️ Medium | Carries Tailwind 4 + Radix; style token bridge needed |
| Full white-label self-host | ✅ High | Docker-compose provided; MIT license |
| Hosted/managed option | ❌ N/A | No hosted version; self-host required |

---

## Self-Hosting Requirements

- PostgreSQL 13+
- Redis (for API v2 caching)
- Docker Compose setup available in repo
- Environment variables for calendar OAuth apps (Google, Outlook), conferencing (Zoom), payment (Stripe), etc.

---

## Related Files

- `_shared/product/architecture-principles.md` — Sunfish layered architecture (compare against Cal.diy's feature-scoped packages)
- `_shared/engineering/package-conventions.md` — Package conventions (CVA/variant management lessons applicable here)
