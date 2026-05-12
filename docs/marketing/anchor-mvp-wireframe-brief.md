# Anchor MVP — Wireframe Brief

**Document type:** Marketing / design handoff
**Product:** Sunfish Anchor — local-first desktop + mobile application
**Date:** 2026-05-03
**Prepared by:** Sunfish product team

---

## 1. What Anchor Is

Anchor is a **local-first desktop and mobile application** for small operators who want to run their business on their own device — no mandatory cloud account, no data leaving their hardware unless they choose to sync.

The first reference deployment is **property management**: a small landlord running multiple legal entities (LLCs, a holding company, a management company) who needs rent collection, bank reconciliation, vendor management, lease tracking, and financial statements — all from one app, all owned locally.

**The core promise:** your data lives on your device. You are online if you choose. Sync with other devices — including a spouse's laptop or a field worker's iPhone — is possible but never required.

**Platforms:** Windows desktop (primary), macOS desktop, iOS (companion), Android. All four share the same core; this brief covers the desktop experience. The iOS companion is a lighter surface for field workers.

---

## 2. Primary Users

### 2.1 The Operator (primary)

A small landlord, property manager, or small-business owner who runs multiple legal entities.

**Goals:**
- See the health of every entity at a glance — pending rent, open maintenance, recent bank activity
- Process monthly cycles: rent → invoice → bank reconciliation → statements → vendor payments
- Stay organized across multiple properties, tenants, vendors, and leases

**Mental model:** used to juggling spreadsheets, QuickBooks, and a property management SaaS (Wave, Rentler, etc.). Comfortable with software. Not a developer. Wants things consolidated in one place.

**Concern:** "My data is on someone else's server. They could raise prices, go out of business, or get breached."

### 2.2 The Co-Owner

A spouse or business partner with shared ownership of one or more entities. Has co-ownership access with full visibility and controlled write permissions. Signs off on certain transactions (the app enforces this via a shared approval flow).

**Goals:**
- See what's happening without needing to be the primary administrator
- Approve recovery / emergency access requests
- Run reports independently

**Mental model:** an executive-level stakeholder in the operation, not an administrator.

### 2.3 The Field Worker (mobile, future phase)

An inspector, contractor, or property maintenance worker who receives a limited, time-bound invitation to specific data relevant to a job. They never see the full operation — just the property or work order assigned to them. Uses the iOS companion app.

**Goals:**
- Capture an inspection report, photos, or a signature on-site
- Submit without needing a full Anchor account

---

## 3. Anchor's Information Architecture

Anchor organizes its surfaces using a **ship metaphor** — each area of the app is named after a part of a ship. This vocabulary is stable and will appear in UI labels.

### v1 Locations (navigation destinations)

| Location | What it is | Status |
|---|---|---|
| **Quarterdeck** | Home / dashboard. Entry point on launch. Executive summary of the whole operation. | Wireframe target |
| **Wayfinder** | Settings + configuration. Divided into **Helm** (live status + quick toggles) and **Atlas** (deep settings pages). | Wireframe target |
| **Engine Room** | Observability — sync logs, background job status, telemetry, health of the local node. | Wireframe target (basic) |
| **Tactical** | Alerts and anomaly detection — overdue rent, failed syncs, pending conflicts. | Wireframe target (basic) |
| **Sick Bay** | Recovery and identity — key management, recovery contacts (the people who can unlock the app if you lose access), account security. | Wireframe target |
| **Ship's Office** | Document and content hub — leases, statements, reports, templates. | Wireframe target |
| **Supply Office** | Vendor and procurement management. | Phase 2 — skip for MVP |

### Workspace Switcher

Anchor supports multiple **workspaces** — one per legal entity or team. This is analogous to Slack workspaces: one installation, multiple contexts. The workspace switcher lives in a persistent sidebar (left column on desktop). Switching workspaces changes the entire data context; the navigation structure stays the same.

**For the reference operator:** 6 workspaces — 4 LLCs, 1 holding company, 1 management company. Each workspace has its own encrypted local database; the workspaces only share the device identity.

---

## 4. Screen Inventory

### 4.1 Onboarding Flow (already built — document for reference)

A one-time 3-step wizard shown on first launch. Wire this for QA and spec consistency rather than design invention.

**Step 1 — Install**
> "App is installed and ready. Continue to connect this device to a team."

Single "Continue" button.

**Step 2 — Authenticate**
Two options side-by-side (split card layout):

- **Join an existing team** — paste a base64-encoded invitation bundle or scan a QR code from an existing member's device
- **Start a new team** — enter a team name; this device becomes the founder (the first node; owner-level role)

**Step 3 — Sync**
An indeterminate progress bar while the initial data snapshot is applied. No user action; auto-advances to the dashboard on completion.

---

### 4.2 Quarterdeck (Home / Dashboard)

**Entry point.** Loads after onboarding. This is where the operator lands every time they open the app.

**Persistent shell elements (always visible):**
- Left sidebar: workspace switcher (list of entity workspaces with badges showing unread counts / alert counts) + main navigation links to each ship location
- Top bar: active workspace name; sync status indicator (color + icon + text; see §5 for states); current user + role label

**Quarterdeck content — three tiers:**

**Top deck (executive summary)** — KPI cards across the operation:
- Pending rent (count and total $)
- Open maintenance requests (count)
- Unreviewed items (conflicts, approvals pending)
- Bank last reconciled (date freshness badge)
- Active leases (count)

Each card is a tap target linking to the relevant section (rent → rent ledger, maintenance → maintenance queue, etc.).

**Main deck (active pulse)** — recent activity feed:
- Rent payments received today
- Work orders updated
- Sync events (new data from co-owner's device, field worker submission)
- Standing Orders issued (configuration changes)
- Upcoming: lease expiry alerts, rent due in N days

**Alert ticker** — a dismissable alert banner at the top when there are items needing attention (overdue rent, sync conflict, recovery contact change).

---

### 4.3 Workspace Switcher (Sidebar Component)

A persistent left column visible in every location.

**Contents:**
- App logo / wordmark at top
- List of workspaces (entities), each showing:
  - Entity name (e.g., "Cedar Ridge LLC")
  - Entity type badge (LLC, Holding, Management)
  - Alert badge (unread count — red dot with number if > 0)
  - Active state highlight (current entity has a solid left-border or highlighted background)
- "Add workspace" button at the bottom of the list (opens the onboarding flow scoped to adding a second team)
- Separator
- Navigation links (shared across all workspaces):
  - Quarterdeck
  - Ship's Office
  - Tactical
  - Engine Room
  - Sick Bay
  - Wayfinder (Settings)

---

### 4.4 Property Management Module Screens

These screens live inside the Quarterdeck area and the Ship's Office. They represent the core business functionality for the property management use case.

#### Properties

A list of all properties owned by the active entity.

- Property card: address, unit count, occupancy rate, active lease status, last inspection date
- Filter bar: by status (occupied / vacant / pending), by property type
- "Add property" action
- Tap → Property Detail

**Property Detail:**
- Address, photos, description
- Tabs: Overview / Tenants / Leases / Maintenance / Inspections / Equipment / Documents

#### Tenants

List of all tenants across properties for the active entity.

- Tenant row: name, property + unit, lease status, rent status (current / overdue), contact method
- Filter: by status, by property
- Tap → Tenant Detail

**Tenant Detail:**
- Contact info, lease summary, rent ledger, maintenance history
- Actions: send message, record payment, generate statement

#### Leases

A list of active and historical leases.

- Lease row: property, tenant, start/end date, monthly amount, status badge (active / expiring-soon / expired / pending)
- Color coding: green = active, yellow = expiring in 60 days, red = expired
- Tap → Lease Detail (terms, payment history, attached documents, renewal actions)

#### Rent Collection

A dedicated screen for the monthly rent cycle.

- Summary tile: total expected vs. received this month, number of units current vs. overdue
- Ledger: one row per unit, showing expected amount, received amount, balance, payment date
- Quick actions: "Record payment", "Send reminder", "Mark as waived"
- Batch action: select multiple → send reminders to all

#### Maintenance & Work Orders

A queue of open and completed maintenance items.

- Status pipeline view (Kanban-style columns or filterable list): Reported → Assigned → In Progress → Resolved
- Work order card: property, unit, description, assigned vendor, date reported, priority badge
- Tap → Work Order Detail: full description, photos, communication thread with vendor, resolution notes
- "New work order" action

#### Vendors

A list of approved vendors (contractors, plumbers, electricians, etc.).

- Vendor card: name, trade(s), license status badge (verified / unverified), insurance status, rating
- Tap → Vendor Detail: contact info, work history, license + insurance documents, upcoming work orders
- "Add vendor" and "Invite to job" actions

#### Inspections

A record of property inspections.

- List: property, inspection type (move-in / move-out / periodic), date, inspector, result badge (pass / needs attention / fail)
- Tap → Inspection Report: section-by-section findings, photos, signature block, PDF export

#### Bank Reconciliation

For reconciling imported bank transactions against recorded rent payments and vendor payments.

- Unmatched transactions list: each row shows transaction date, amount, description from bank; actions: "match to existing record" or "create new record"
- Matched items list with match confidence indicator
- Month-to-date balance summary
- "Mark reconciled" action to close the month

#### Statements

A generator for monthly or annual financial summaries.

- Template selector (income statement, cash flow, tax prep export)
- Date range picker
- Entity selector (single entity or consolidated)
- Preview + "Download PDF" + "Send to advisor" actions

---

### 4.5 Ship's Office (Document Hub)

All generated documents and uploaded files in one place.

- Document list: title, type badge, associated entity, date, status (draft / final / sent)
- Filter: by type (lease, statement, inspection report, template)
- Search (full-text across document titles and tags)
- Tap → Document Detail: preview, metadata, version history, share / export actions
- "New document from template" action

---

### 4.6 Tactical (Alerts)

A dedicated alert and anomaly surface. Think of it as an inbox for things that need attention.

**Three sections:**

- **Critical** (red): overdue rent past grace period, sync conflict requiring resolution, recovery-contact approval pending
- **Warnings** (amber): rent due in 3 days, lease expiring in 60 days, maintenance overdue, bank sync last successful 7+ days ago
- **Informational** (blue): new tenant message received, vendor submitted a completion report, field worker submitted inspection

Each alert is dismissable or actionable with a single tap (dismissing moves to the "cleared" archive; action opens the relevant screen).

---

### 4.7 Engine Room (Observability)

For the operator who wants to know what the app is doing in the background. Light treatment — most operators will never open this.

- **Sync log**: timestamped list of sync events (devices connected, data exchanged, conflicts detected)
- **Job queue**: background tasks (bank import, statement generation, scheduled reminders) with status
- **Node health**: disk usage, encryption key status, last backup timestamp
- **Connectivity**: list of known peer devices (co-owner laptop, iOS companion) and their last-seen timestamps

---

### 4.8 Sick Bay (Recovery & Identity)

For managing account security and recovery. Sensitive, rarely-visited.

**Sections:**

- **Identity**: current user's name, role, associated entity memberships, Ed25519 public key fingerprint (displayed as a short human-readable string, not raw hex)
- **Recovery contacts**: a list of people designated as recovery trustees (e.g., spouse, business partner). Each contact's approval is required if the operator loses access to the device. Actions: add contact, remove contact, send test verification
- **Paper key**: a one-time-generated backup passphrase (offline recovery). "Generate paper key" action; once generated, displayed for writing down; never stored by the app.
- **Active devices**: list of devices enrolled in this workspace with last-seen date; actions: revoke access

---

### 4.9 Wayfinder (Settings)

**Helm** — a live glance pane at the top of the settings area:
- Current sync status (expanded: last synced at, peers visible, pending items)
- Active team + node ID
- Quick toggles: dark/light theme, notification preferences, offline mode (pause sync)

**Atlas** — scrollable settings pages organized by category:

| Category | Settings |
|---|---|
| Appearance | Theme (light / dark / auto), density (compact / comfortable), language |
| Notifications | Per-event type: which events send a notification, per-platform (desktop / mobile) |
| Sync | Sync on/off per workspace, sync frequency (real-time / manual), metered-connection behavior |
| Security | MFA options, session timeout, require approval for destructive actions |
| Integrations | Bank connection (Plaid), payment processor (Stripe), outbound messaging (email/SMS provider) |
| Data | Export all data, delete workspace, import from legacy system (Wave Accounting migration) |
| About | App version, open-source licenses, feedback link |

---

### 4.10 Add Workspace Flow

Triggered from the "Add workspace" button in the sidebar. Re-uses the onboarding authentication step but scoped to joining a second team rather than first-launch.

- Option A: Scan QR or paste bundle (join an existing entity's workspace where you're already a member)
- Option B: Start a new workspace (create a new entity from scratch)

No sync/install step — device is already initialized.

---

### 4.11 Co-Owner Approval Flow

When the primary operator requests a sensitive action (e.g., revoking a recovery contact, bulk-deleting records, generating a paper key), the app sends an approval request to the co-owner.

**Operator side:**
- Action button → "Requires co-owner approval" modal
- Status: "Approval pending — waiting for [Co-owner Name]"
- Cancel action

**Co-owner side (notification arrives):**
- Push notification or in-app alert
- Approval request detail: what action, who requested, when, affected data
- "Approve" / "Deny" buttons
- Reason field (optional, surfaced to requester)

---

## 5. Sync Status Indicator

The sync status indicator appears persistently in the top bar. It always shows all four signals: color, icon, short text label, and an accessible role for screen readers.

| State | Color | Icon | Short label | Meaning |
|---|---|---|---|---|
| **Healthy** | Green `#27ae60` | Checkmark circle | "Synced" | All peers are current |
| **Stale** | Blue `#3498db` | Clock | "2h ago" | Data is aging; sync hasn't run recently |
| **Offline** | Grey `#7f8c8d` | Cloud-off | "Offline" | No network; working locally |
| **Conflict** | Orange `#e67e22` | Split arrows | "Conflict" | Two versions of a record diverged — user review needed |
| **Held** | Red `#c0392b` | Circle-block | "Held" | Cannot sync — user must open diagnostics |

Tapping / clicking the indicator opens a detail popover (in the Helm / Wayfinder) showing expanded status.

---

## 6. Key User Flows to Wireframe

In priority order:

### Flow 1 — First launch and setup (new operator)

```
App launch → Onboarding Step 1 (Install) → Step 2 (Start new team, enter name)
→ Step 3 (Sync / initialization) → Quarterdeck
```

### Flow 2 — Monthly rent cycle

```
Quarterdeck (pending rent card) → Rent Collection screen →
  For each unit: Record Payment (amount, method, date) →
  All marked → "Generate statements" shortcut →
  Statements screen → Preview → Download PDF / Send to advisor
```

### Flow 3 — Add a second workspace (second LLC)

```
Sidebar → "Add workspace" → Choose "Start new workspace" →
Name + type selection → Initialization → Back to Quarterdeck
(workspace switcher now shows 2 entities)
```

### Flow 4 — Onboard co-owner on their device

```
Operator: Wayfinder → Security → Invite device →
QR code displayed
Co-owner: Fresh install → Onboarding Step 2 → "Scan QR" →
Camera opens, scans → Sync screen → Co-owner's Quarterdeck
(shows same entity with co-owner role)
```

### Flow 5 — Handle a maintenance request

```
Tactical alert ("Maintenance overdue — Unit 4B") →
Tap alert → Work Order Detail →
Assign vendor (from vendor list) →
Vendor notified → (Field worker submits completion via iOS) →
Work order status updates → Close
```

### Flow 6 — Bank reconciliation

```
Quarterdeck ("Bank unreconciled — 12 days") →
Bank Reconciliation screen →
Match transactions to rent payments one by one →
Unmatched items: "Create new record" →
"Mark month reconciled" → Reconciliation complete badge
```

### Flow 7 — Recovery contact setup (Sick Bay)

```
Wayfinder → (or Quarterdeck alert "Recovery not configured") →
Sick Bay → Recovery Contacts → Add contact →
Enter name + contact method →
Co-owner receives verification request → approves →
Contact shows "Verified" badge
```

---

## 7. Navigation Model

### Desktop shell (sidebar layout)

```
┌─────────────────────────────────────────────────────────────┐
│ [Logo]  Sunfish Anchor                     [Sync Status]    │
├─────────────┬───────────────────────────────────────────────┤
│             │                                               │
│  WORKSPACES │  [Main content area — changes per location]  │
│             │                                               │
│  ● LLC 1  3 │                                               │
│  ○ LLC 2    │                                               │
│  ○ LLC 3    │                                               │
│  ○ Holding  │                                               │
│  ○ Mgmt Co  │                                               │
│  + Add      │                                               │
│             │                                               │
│  ─────────  │                                               │
│  NAV        │                                               │
│  Dashboard  │                                               │
│  Documents  │                                               │
│  Alerts     │                                               │
│  Logs       │                                               │
│  Recovery   │                                               │
│  Settings   │                                               │
│             │                                               │
│  ─────────  │                                               │
│  [User]     │                                               │
│  [Role]     │                                               │
└─────────────┴───────────────────────────────────────────────┘
```

- Sidebar is collapsible to icon-only on narrower windows
- Active workspace has a filled indicator; others are outline
- Alert badges on workspace items show unread counts
- Bottom of sidebar: current user name + role

### Top bar (persistent)

```
┌─────────────────────────────────────────────────────────────┐
│  [Active Entity Name]   [Synced ●]   [Username ▾]          │
└─────────────────────────────────────────────────────────────┘
```

- Sync status is always visible; click/tap to expand
- Username dropdown: My Account, Switch User, Sign Out

---

## 8. Design Direction

### Visual identity

- **Primary color:** Forest green `#1e6b3c` — used for primary actions, active states, positive sync status
- **Accent green (light):** `#7fc794` — used for progress bars, secondary highlights
- **Surface background:** Off-white `#fafafa` with card backgrounds `#ffffff`
- **Borders:** `#e5e5e5` (light, hairline)
- **Text:** Near-black `#111` for headings, `#555` for body, `#666` for muted/secondary
- **Status colors:** Green `#27ae60` (healthy), Blue `#3498db` (stale), Grey `#7f8c8d` (offline), Orange `#e67e22` (conflict), Red `#c0392b` (critical)

### Typography

- **System font stack** — the app uses `system-ui, -apple-system, Segoe UI, Roboto, sans-serif`
- H1: 1.75rem, letter-spacing -0.01em
- H2: 1.0625rem
- Body: 1rem / 1.5 line-height
- Small / muted: 0.875rem, color `#666`
- Code / IDs: Consolas, Menlo, monospace

### Component style

- Cards: 1px `#e5e5e5` border, 8px border-radius, `#fafafa` background, 1.25rem / 1.5rem padding
- Buttons: primary = green `#1e6b3c` fill + white text; secondary = white fill + green border + green text
- Inputs: 1px `#ccc` border, 4px border-radius, 0.5rem / 0.625rem padding
- Error states: `#fdecea` background, `#8a1c1c` text
- Progress/stepper: active step = green fill + white text; complete = light green `#e6f4ea` + green text; pending = `#f1f1f1` + grey

### Interaction patterns

- **Optimistic writes** — actions confirm immediately; the app writes locally and syncs in the background. No loading spinners for local operations.
- **No full-page loaders** — the app is local-first; reads are instant. Loading states appear only when fetching from external services (bank import, sending a message).
- **Status badges** — all status communication uses text + icon + color (never color alone). See §5 for the sync status palette.
- **Focus states** — all interactive elements have visible focus rings. Keyboard navigation is expected.

### Layout density

The app targets desktop as primary. Mobile (iOS companion) is a narrower, touch-optimized surface. Desktop layouts use a sidebar + content area pattern. Cards use 8px border-radius. Tables use comfortable row height (48px minimum).

---

## 9. Scope for Wireframing

### In scope — wireframe these

| Priority | Screen / Flow |
|---|---|
| P0 | Quarterdeck (home dashboard) with workspace switcher sidebar |
| P0 | Onboarding flow (3 steps) |
| P0 | Rent Collection screen |
| P1 | Property List + Property Detail |
| P1 | Tenant List + Tenant Detail |
| P1 | Lease List + Lease Detail |
| P1 | Work Orders / Maintenance queue |
| P1 | Tactical (Alerts) |
| P1 | Wayfinder Settings (Helm glance + Atlas categories) |
| P2 | Sick Bay (Recovery contacts + Paper key) |
| P2 | Ship's Office (Document hub) |
| P2 | Bank Reconciliation |
| P2 | Statements generator |
| P2 | Vendor List + Vendor Detail |
| P2 | Engine Room (Observability — basic) |
| P3 | Add workspace flow |
| P3 | Co-owner approval modal |
| P3 | Inspections |

### Out of scope for MVP wireframes

- iOS companion app (field worker surface) — separate brief
- Billing / subscription management
- Supply Office (vendor procurement module) — Phase 2
- Federation / data-sharing with external organizations
- Admin backoffice (this is the Bridge accelerator, not Anchor)

---

## 10. Constraints and Notes for the Design Team

1. **Local-first UX contract** — the app should never imply that data is being "saved to the cloud." Language should say "saved" not "uploaded." Sync indicators are status, not confirmation.

2. **Multi-entity context** — the workspace switcher is always visible. Users switch entities frequently. The active entity must be unmistakably clear at all times (name in top bar, highlighted in sidebar).

3. **No spinners for local reads** — screens load instantly from local data. Only external fetches (bank import, send message) warrant a loading state.

4. **Accessibility baseline** — WCAG 2.2 AA. Status information is never conveyed by color alone (always color + icon + text). All interactive elements reachable by keyboard. Focus rings always visible.

5. **Dense information, not sparse** — the operator is managing 6 entities, 20+ properties, 50+ tenants. The design needs to be information-dense without being overwhelming. Think QuickBooks or Linear, not Airbnb.

6. **The ship metaphor is literal** — navigation labels use the ship vocabulary (Quarterdeck, Wayfinder, Engine Room, Tactical, Sick Bay, Ship's Office). Wireframes should reflect these labels, not substitute generic equivalents like "Dashboard" or "Settings."

7. **Sync status is always visible** — the top bar sync indicator is a hard UX requirement. It cannot be hidden or collapsed in any state.

8. **Entity type matters** — LLCs, holding companies, and management companies have different sets of active screens. An LLC has properties, tenants, and leases. A management company manages other entities. The wireframes should acknowledge that different workspace types have different default content.
