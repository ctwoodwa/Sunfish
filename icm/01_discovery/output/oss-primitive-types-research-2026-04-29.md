# OSS Primitive Types Research — Reference Survey for Sunfish Dynamic Forms

**Stage:** 01 Discovery
**Status:** Research artifact informing the dynamic-forms substrate ADR
**Date:** 2026-04-29
**Author:** CTO (research session)
**Triggered by:** CEO directive 2026-04-29 — "conduct research on primitives in popular OSS packages like temporal, GIS, CMS, project management etc."
**Companion:** [`dynamic-forms-authorization-permissions-upf-2026-04-29.md`](./dynamic-forms-authorization-permissions-upf-2026-04-29.md) — section-based permissions UPF (Approach F adopted); cross-field rules UPF (forthcoming); dynamic-forms substrate ADR (synthesis of all three).

---

## Purpose

Validate the proposed Sunfish primitive type catalog against well-established external systems before committing the dynamic-forms substrate ADR. Three goals:

1. Identify primitives Sunfish missed
2. Identify Sunfish primitives that are wrongly shaped (relative to industry)
3. Surface design patterns Sunfish should adopt or explicitly reject

Source set covers 11 systems across 5 categories. Each section: what they have, what's interesting, what Sunfish should learn or borrow. Synthesis at the end produces a revised primitive catalog + delta against the prior v1 list.

---

## 1. FHIR (HL7 Fast Healthcare Interoperability Resources)

The healthcare interoperability standard. Mature, battle-tested, ratified across 100+ countries. Strongest reference for **structured composite types** — many primitive type designs originated here.

### Key types

| FHIR Type | Composition | Notes |
|---|---|---|
| **HumanName** | `family[]`, `given[]`, `prefix[]`, `suffix[]`, `use`, `period`, `text` (display) | All array-cardinality on names; `text` is the display form (authoritative; never auto-formatted from parts). Period bounds the validity (maiden name vs married name). |
| **Address** | `line[]`, `city`, `district`, `state`, `postalCode`, `country`, `period`, `use`, `type` | `line[]` accommodates multi-line addresses; period bounds validity (former vs current); `use` is enum (home/work/temp/old/billing). |
| **ContactPoint** | `system`, `value`, `use`, `rank`, `period` | `system` enum: phone/fax/email/pager/url/sms/other. `rank` orders preference. |
| **Quantity** | `value`, `comparator`, `unit`, `system`, `code` | `comparator` enum (`<`, `<=`, `>=`, `>`) for "less than" semantics. `system` is the URI of the unit code system (UCUM, SNOMED, etc.). |
| **Range** | `low: SimpleQuantity`, `high: SimpleQuantity` | Both bounds optional but at least one required; same unit invariant. |
| **Period** | `start: dateTime`, `end: dateTime` | Both bounds optional. |
| **Identifier** | `use`, `type`, `system`, `value`, `period`, `assigner` | `system` URI namespaces the identifier (e.g., national ID system); `assigner` references the issuing organization. |
| **Money** | `value`, `currency` | `currency` is ISO 4217 alpha-3. Simpler than the rest. |
| **Coding** | `system`, `version`, `code`, `display`, `userSelected` | `system` URI of the code system (e.g., ICD-10); `display` is human-readable text. |
| **CodeableConcept** | `coding[]: Coding`, `text` | Multiple codings for the same concept (interop across code systems). |
| **Annotation** | `author`, `time`, `text` | A note attached to anything. |
| **Reference** | `reference`, `type`, `identifier`, `display` | Typed pointer to another resource. |
| **Attachment** | `contentType`, `language`, `data` (base64) OR `url`, `size`, `hash`, `title`, `creation` | Document/file primitive. |
| **Signature** | `type[]: Coding`, `when`, `who`, `onBehalfOf`, `targetFormat`, `sigFormat`, `data` | Cryptographic signature primitive. |
| **Timing** | `event[]`, `repeat: Timing.Repeat`, `code` | Recurring schedule with bounds, frequency, period, day-of-week, etc. |

### What's interesting

- **All names + addresses + contact points are arrays.** A person can have multiple given names; an address can have multiple lines; multiple ways to reach a person. FHIR doesn't pretend humans have one of each.
- **Period bounds on most things.** `HumanName` has a period (maiden vs married). `Address` has a period (former residence vs current). `Identifier` has a period (driver's license expires). This is a remarkably consistent pattern.
- **`text` field as authoritative display form** alongside structured parts. Don't auto-format; let the person tell you how to display.
- **`Coding` + `CodeableConcept`** is the polymorphic-tag pattern done right: structured codes from external systems + free-text fallback. Maps to enum + variant in modern type systems.
- **`use` enum** on names/addresses/contact points categorizes purpose (home/work/temp/old/billing). Consumers filter on this.

### What Sunfish should adopt

1. **Period as a first-class compound primitive** — rather than DateRange + DateTimeRange, just `Period` with optional date precision. Saves a primitive.
2. **Array-cardinality on names + addresses + contact points** — even SMB property management has cases (multiple given names, multiple address lines, multiple contact methods).
3. **Display-form authoritative + structured parts as metadata** — already adopted in revised PersonName.
4. **`use` enum on contacts/addresses** — "this is the leaseholder's *current home* address" vs "former *billing* address" matters for property management.
5. **Coding + CodeableConcept pattern for taxonomies** — Sunfish equipment classes / inspection deficiency categories / vendor specialties could benefit from this; structured codes from a registry + free-text fallback.

---

## 2. Schema.org

The web's structured-data vocabulary. Used for SEO + structured search results. Lighter than FHIR but broader: 800+ types covering events, places, products, people, organizations, creative works, actions.

### Key types relevant to Sunfish

| Schema.org Type | Sample Properties | Notes |
|---|---|---|
| **Person** | `givenName`, `additionalName`, `familyName`, `honorificPrefix`, `honorificSuffix`, `alternateName`, `name` (display), `birthDate`, `gender`, `nationality`, `email`, `telephone`, `address` | Single given name; single family name (deviation from FHIR's array-cardinality). `additionalName` is middle name. |
| **PostalAddress** | `streetAddress`, `addressLocality` (city), `addressRegion` (state), `postalCode`, `addressCountry`, `postOfficeBoxNumber` | Single string `streetAddress` not array; SEO-shaped. |
| **GeoCoordinates** | `latitude`, `longitude`, `elevation`, `addressCountry`, `postalCode` | Geo with associated postal context. |
| **Place** | `address`, `geo`, `containedInPlace`, `containsPlace` | Tree-structured place hierarchy (matches our parent-child instance tree). |
| **Event** | `startDate`, `endDate`, `duration`, `location`, `attendee[]`, `organizer`, `eventStatus`, `eventAttendanceMode` | Date primitives + recurrence via `EventSeries`. |
| **MonetaryAmount** | `value`, `currency`, `minValue`, `maxValue`, `validFrom`, `validThrough` | Money + range + period in one type. |
| **QuantitativeValue** | `value`, `unitCode` (UN/CEFACT), `unitText`, `minValue`, `maxValue`, `additionalProperty[]` | Measurement + range; `additionalProperty[]` extensibility hatch. |
| **PropertyValue** | `propertyID`, `value`, `unitCode`, `valueReference` | Generic name-value pair for arbitrary attributes. |
| **DefinedTerm** | `name`, `description`, `termCode`, `inDefinedTermSet` | Taxonomy entry; matches FHIR Coding pattern. |
| **Action** | `agent`, `instrument`, `object`, `participant`, `actionStatus`, `result`, `target`, `startTime`, `endTime`, `error` | An action that can be taken; standardized for verb-frame structured data. |

### What's interesting

- **`PropertyValue` as a generic name-value extension hatch.** When you don't have a primitive for something, attach a `PropertyValue`. Pragmatic; matches Sunfish's "extension fields" goal in ADR 0005.
- **`MonetaryAmount` collapses Money + MoneyRange + Period.** Simpler than three types.
- **`QuantitativeValue` similarly collapses Measurement + Range** with `additionalProperty[]` for extension.
- **`Place` containment tree** matches Sunfish's instance-tree pattern (Property → Unit → Equipment).
- **Schema.org is intentionally permissive** — many properties can have multiple types (e.g., `address` can be `PostalAddress` OR `Text`). Trade-off: flexible for SEO, ambiguous for typed code.

### What Sunfish should adopt

1. **Collapse Money + MoneyRange into a single `MonetaryAmount`-shape** (value + currency + optional minValue + maxValue). Saves a primitive.
2. **Collapse Measurement + Range similarly** into `QuantitativeValue` shape (value + unit + optional min/max).
3. **`PropertyValue`-style generic extension** — useful for admin-defined types adding fields the platform doesn't predefine.
4. **DON'T** adopt Schema.org's flexibility — schemas should be strict for code; permissive for SEO/data-export only.

---

## 3. iCalendar (RFC 5545)

The standard for calendar data. Mature; battle-tested across all calendar apps. Strongest reference for **temporal recurrence**.

### Key types

| iCal Component | Purpose |
|---|---|
| **VEVENT** | A single event with start/end; attendees; recurrence rule; alarm. |
| **VTODO** | A task with due date / completion. |
| **VJOURNAL** | A journal entry. |
| **VFREEBUSY** | Available/busy windows for scheduling. |
| **VTIMEZONE** | Timezone definition. |

### Key properties

- **DTSTART / DTEND** — start + end with timezone.
- **DURATION** — alternative to DTEND.
- **RRULE** — recurrence rule. Powerful: `FREQ=WEEKLY;BYDAY=MO,WE,FR;COUNT=10` = "10 occurrences on Mon/Wed/Fri starting from DTSTART."
- **EXDATE** — exception dates (skip these in the recurrence).
- **RDATE** — additional dates (extend the recurrence with one-offs).
- **ATTENDEE** — participant with `CN` (common name), `ROLE` (chair/req-participant/opt-participant), `PARTSTAT` (accepted/declined/tentative), `RSVP`, `DELEGATED-FROM`, `DELEGATED-TO`.
- **CATEGORIES** — tags.
- **STATUS** — confirmed/tentative/cancelled.
- **GEO** — lat/lng.
- **TRANSP** — opaque/transparent (does this block other meetings?).

### What's interesting

- **RRULE expressiveness.** Annual inspection cadence ("every March 15"), HVAC service interval ("every 6 months from install date"), monthly statement ("first day of every month") — all expressible as RRULE.
- **EXDATE + RDATE** together handle exceptions to recurrence cleanly.
- **ATTENDEE roles + PARTSTAT** map cleanly to property-management appointment scheduling — vendor visit confirmations, tenant access coordination.
- **TRANSP** for "blocking vs informational" — useful for showings (block the time slot) vs reminders (don't block).

### What Sunfish should adopt

1. **`RecurrenceRule` as a compound primitive** — adopt RRULE syntax verbatim. Annual inspections, monthly statements, quarterly tax filings, lease renewal reminders all use RRULE shape. Don't reinvent.
2. **`Attendee` compound** for appointment scheduling (vendor + leaseholder + tenant access coordination).
3. **`Period` + `RecurrenceRule` + `EXDATE/RDATE`** as a complete temporal-event primitive set.

---

## 4. W3C Personal Names Around the World + ICU CLDR PersonName

The canonical reference for global-friendly name modeling.

### Key insights

- W3C document explicitly calls out 27 cultural variations in name structure
- ICU 72+ ships `PersonName` formatter (CLDR data) that knows: name order per locale, length per context (long/medium/short/monogram), formality (formal/informal/referring), usage (addressing/sorting/monogram)
- ICU PersonName fields: `givenName`, `givenName2` (additional given), `surname`, `surname2`, `surnamePrefix`, `title` (honorific), `credentials` (post-nominal), `generation` (Jr/III), `informal` (nickname)
- Per-locale formatting rules tell the renderer how to compose those fields into a final string

### What Sunfish should adopt

1. **CLDR-aligned `PersonName` shape** — already adopted in revised v1 catalog. Confirmed.
2. **Locale-aware rendering separate from data shape** — store fields, render per locale at form-display time.
3. **`generation` + `credentials`** as structured fields rather than baking into `suffix` — matches ICU/CLDR.

Don't reinvent. Adopt CLDR.

---

## 5. GeoJSON / PostGIS

The standard for spatial data interchange (GeoJSON / RFC 7946) + the production-quality spatial-database type system (PostGIS).

### Key types

| GeoJSON Type | Composition |
|---|---|
| **Point** | `coordinates: [lng, lat, elevation?]` (note: longitude-first per RFC 7946) |
| **MultiPoint** | `coordinates: [[lng,lat], ...]` |
| **LineString** | `coordinates: [[lng,lat], [lng,lat], ...]` (≥2 points) |
| **MultiLineString** | array of LineString coordinates |
| **Polygon** | `coordinates: [[outerRing], [hole1], [hole2]]` (each ring is array of ≥4 points; first = last) |
| **MultiPolygon** | array of Polygon coordinates |
| **GeometryCollection** | array of mixed Geometry types |
| **Feature** | `geometry: Geometry`, `properties: object` |
| **FeatureCollection** | array of Features |

### What's interesting

- **Longitude-first coordinate order** — counter-intuitive (most humans say "lat, lng") but RFC-mandated. Easy to get wrong; SDKs vary.
- **Polygon with holes** — a polygon can have an outer ring + N inner rings (holes). Useful for parcels with easements, properties with right-of-ways through them.
- **Feature pattern** — geometry + arbitrary properties. Matches Sunfish's "shape + entity attributes" pattern; could underpin a Property entity that includes its parcel polygon.
- **PostGIS adds:** SRID (spatial reference system identifier — typically WGS 84 = SRID 4326 for GPS); spatial indexes; distance/intersection/containment queries; geocoding via PostGIS Tiger Geocoder.

### What Sunfish should adopt

1. **`GeoPoint`** (revised name from `LatLng`) — adopt RFC 7946 coordinate order; include optional `elevation`.
2. **`GeoPolygon`** as deferred/v2 primitive — for parcel boundaries, easement mapping. Defer Phase 2.x.
3. **SRID always implicit WGS 84 in v1** — defer multi-SRID support to whenever international or non-GPS coordinates surface.
4. **`Feature` pattern** — Property = entity + optional GeoPolygon. Don't conflate the geometry with the entity.

---

## 6. RESO Real Estate Standards

The data dictionary for residential real estate. Used by MLS providers, listing aggregators, property-management software. The vertical-specific reference.

### Key types

| RESO Concept | Notes |
|---|---|
| **Property** | Address, geo, parcel ID, listing-attributes (bedrooms, bathrooms, year-built, lot-size), media, listing-status, price |
| **Member** (Agent/Broker) | License info, contact, association |
| **Office** (Brokerage) | Address, contact, branding |
| **Listing** | Property + price + agent + status + dates + commission |
| **OpenHouse** | Property + start + end + description |
| **Showing** | Property + agent + appointment time + status |

### What's interesting

- **`PropertyType` enum** — Residential/Commercial/Land/Boat/Mobile-Home/etc. Schema-shipped; not user-defined.
- **`PropertySubType` enum** — within Residential: SingleFamilyResidence/Condominium/Apartment/Manufactured/etc.
- **Specific identifiers shipped**: APN (assessor parcel number), ListingId (per MLS), MlsNumber.
- **Dates everywhere**: ListingContractDate, StatusChangeTimestamp, OnMarketTimestamp, CloseDate, DaysOnMarket.
- **Price + Currency** shipped as separate fields, not as a Money compound (RESO predates strong typing).

### What Sunfish should adopt

1. **`PropertyKind` + `PropertySubKind` enums** — the cluster Property entity already has `Kind`; consider extending to `SubKind` mirroring RESO. Saves us defining our own enum vocabulary.
2. **APN + MLS-style identifiers** as a future v2 primitive (deferred).
3. **`Showing` + `OpenHouse`** as future cluster types in the leasing-pipeline workstream.
4. **DON'T** adopt RESO's separate price + currency — Money compound is the right shape.

---

## 7. CMS systems — Strapi, Contentful, Sanity, Drupal

CMS systems are the closest existing analog to "no-code platform with admin-defined types." All four ship a field-type catalog + composition primitives.

### Common field types across CMS

| Type | Strapi | Contentful | Sanity | Drupal |
|---|---|---|---|---|
| Text (string, slug) | ✓ | ✓ | ✓ | ✓ |
| Rich text (Markdown / HTML) | ✓ | ✓ | ✓ | ✓ |
| Number (int / decimal) | ✓ | ✓ | ✓ | ✓ |
| Boolean | ✓ | ✓ | ✓ | ✓ |
| Date / DateTime | ✓ | ✓ | ✓ | ✓ |
| Email / URL / Phone | ✓ | ✗ (just text+validation) | ✓ | ✓ |
| Media (single file) | ✓ | ✓ | ✓ | ✓ |
| Gallery (multiple files) | ✓ | ✓ | ✓ | ✓ |
| Reference / Relation | ✓ | ✓ | ✓ | ✓ |
| **Components** (reusable groups) | ✓ | ✓ | ✓ | ✗ (uses Paragraphs module) |
| **Dynamic Zones** (variant types) | ✓ | ✗ (use References) | ✓ (anyOf) | ✗ |
| Geo coordinates | ✓ | ✓ | ✓ | ✓ |
| Color | ✓ | ✗ | ✓ | ✓ |
| Tags / Taxonomy | ✓ | ✗ | ✓ | ✓ |
| JSON (escape hatch) | ✓ | ✓ (Object) | ✗ | ✓ |
| Internationalized text | ✓ | ✓ | ✓ | ✓ |
| SEO compound | ✓ (component) | ✓ (App-managed) | ✓ (plugin) | ✓ (module) |

### What's interesting

- **Components** = reusable groups of fields, defined once and embedded in many types. Direct match for Sunfish's value objects.
- **Dynamic Zones (Strapi)** / **anyOf (Sanity)** = polymorphic content blocks. Direct match for Sunfish's variant types (Address: USAddress | EUAddress | ...).
- **Internationalized text** as a first-class field type (different from "the form supports multiple locales") — each text field can be translated per locale; the storage shape includes per-locale strings.
- **CMS systems explicitly distinguish "primitive types" from "compound types from components".** Components are user-defined; primitives are platform-shipped. Same architecture Sunfish needs.
- **JSON escape hatch field** — when admin needs an unusual type, dump it in a JSON object. Sanity rejects this (purity); others adopt (pragmatism).

### What Sunfish should adopt

1. **First-class "Component" concept distinct from "Entity"** — value objects with no identity, defined by admin, embedded in multiple types. Already in Sunfish proposal as "value objects."
2. **First-class "Variant" concept** — already in Sunfish proposal as discriminated unions.
3. **Internationalized text type** — for labels, descriptions on user-facing types. Storage = `Map<locale, text>`. Foundation.I18n substrate already exists; just promote to a primitive.
4. **DON'T** adopt JSON escape hatch — keeps schemas strict; admin-defined types should always be schema-validated.
5. **Tags / Taxonomy** as a v1 primitive — for equipment categorization, vendor specialty, work-order priority.

---

## 8. Project Management — Asana, Jira, Linear, Monday, Notion, Airtable

Project management systems are dynamic-form / table-shape platforms with role-aware editing. All ship custom-field type systems.

### Field types catalog

| Type | Asana | Jira | Linear | Monday | Notion | Airtable |
|---|---|---|---|---|---|---|
| Single-line text | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Multi-line / Rich text | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Number (int / decimal) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Currency | ✗ | ✗ | ✗ | ✓ | ✗ (number+formatting) | ✓ |
| Date | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **DateRange** | ✗ | ✗ | ✗ | ✓ ("Timeline") | ✓ | ✓ |
| Duration | ✗ | ✓ ("Story Points") | ✓ ("Estimate") | ✓ ("Time Tracking") | ✗ | ✓ |
| Single-select (enum) | ✓ | ✓ ("Priority", "Status") | ✓ | ✓ | ✓ | ✓ |
| Multi-select (tags) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Person / People** | ✓ ("Assignee") | ✓ ("Assignee", "Reporter") | ✓ | ✓ | ✓ | ✓ |
| File attachment | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Relation** (link to another record) | ✓ | ✓ ("Linked Issues") | ✓ ("Sub-issue") | ✓ ("Connect Boards") | ✓ ("Relation") | ✓ |
| **Rollup** (aggregation across relations) | ✗ | ✗ | ✗ | ✓ | ✓ | ✓ |
| **Formula** (computed field) | ✓ ("Custom Fields") | ✓ ("Calculated") | ✗ | ✓ | ✓ | ✓ |
| Checkbox | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| URL | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Email | ✓ | ✓ | ✗ | ✓ | ✓ | ✓ |
| Phone | ✗ | ✗ | ✗ | ✓ | ✓ | ✓ |
| **Rating / Stars** | ✗ | ✗ | ✗ | ✓ | ✗ | ✓ ("Rating") |
| **Progress** (0-100%) | ✗ | ✗ | ✗ | ✓ | ✓ | ✓ |
| Color | ✗ | ✗ | ✗ | ✓ | ✓ | ✓ |
| Auto-number / Created / Modified | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

### What's interesting

- **DateRange ("Timeline")** is universal across modern PM systems. Tasks have start + end dates. Adopt.
- **Person/People as a first-class field type** distinct from Reference. Person fields autocomplete from the workspace member list; have presence indicators; trigger notifications on assignment. Sunfish equivalent: a `PersonReference` to a `Party` (per `blocks-leases.PartyKind` model).
- **Rollup** = aggregate values from a child relation. "Sum of all maintenance costs across this Property's work orders." Property-management uses these constantly.
- **Formula / Computed fields** — the cross-field-rules consideration from the prior UPF call.
- **Progress (0-100%)** as its own type (not just Percentage) — semantically distinct: "this work order is 60% complete" carries workflow progress, not statistical percentage.
- **Linear's hierarchy: Project → Issue → Sub-issue** matches Sunfish's instance-tree (Property → Unit → Equipment → Sub-component).
- **Monday's "Connect Boards"** lets the user link records across different "tables" (types). Sunfish's reference primitive should support cross-type references.

### What Sunfish should adopt

1. **`Progress`** as distinct from `Percentage` — workflow progress (0-100% complete) vs statistical percentage.
2. **`PersonReference` primitive** — typed reference to a Party with display affordances (avatar, name, presence). Composes Foundation.Macaroons identity.
3. **Rollup** as a field-type concept (deferred to v2 — it's a derived type, not stored data).
4. **Formula / Computed field** — punted to cross-field-rules UPF; will resolve there.
5. **Hierarchy / Sub-relation** as a first-class reference kind (parent-child) distinct from generic Reference.

---

## 9. Temporal (workflow engine)

Temporal's primitives are workflow-shaped, not form-shaped, but inform Sunfish's adjacent work-order coordination spine (ADR 0053).

### Key concepts

- **Workflow** — durable function execution; deterministic; handles failure/retry transparently
- **Activity** — non-deterministic external work (API call, file read); retried on failure
- **Signal** — external event delivered to a running workflow (e.g., "vendor accepted appointment")
- **Query** — read-only inspection of workflow state (e.g., "what's the work-order status?")
- **Search Attribute** — indexed key-value pairs for workflow lookup; types: Bool, Datetime, Double, Int, Keyword (string-exact), KeywordList, Text (full-text)
- **Update** — synchronous state mutation with response
- **Timer** — durable wait-until/wait-for primitive

### What's interesting for Sunfish

- **Search Attribute types** are a remarkably clean catalog of indexable primitive types. If Sunfish needs "find all work orders where Status = AwaitingSignOff and Priority = High," it's the same shape.
- **Workflow + Activity** separation — the workflow code stays deterministic; activities handle external I/O. Sunfish's audit-emission discipline (ADR 0049) and provider-neutrality (ADR 0013) align with this; activities = adapters.
- **Timer primitive** — durable wait. Useful for "wait 48 hours after entry-notice before marking confirmed." Already aligns with the cluster's right-of-entry compliance work.

### What Sunfish should adopt

1. **DON'T** adopt the workflow engine itself; cluster intake explicitly defers Quartz/Temporal integration. But **borrow the indexed-primitive type catalog** for schema-registry-side queries.
2. **Keyword + KeywordList types** for tags — standardized, indexable.
3. **Timer primitive** for cluster work-order entry-notice + lease-expiry reminder semantics. Defer to scheduler ADR (Phase 2.3+).

---

## 10. Salesforce / Microsoft Dataverse

Enterprise no-code platforms. Most directly comparable to where Sunfish is heading.

### Key field types

| Type | Salesforce | Dataverse |
|---|---|---|
| Text (single-line) | ✓ | ✓ |
| Text Area (multi-line) | ✓ (Plain or Rich) | ✓ (Plain or Rich) |
| Number | ✓ | ✓ |
| Currency | ✓ (multi-currency-aware) | ✓ |
| Percent | ✓ | ✓ |
| Date | ✓ | ✓ |
| DateTime | ✓ | ✓ |
| Phone | ✓ | ✓ |
| Email | ✓ | ✓ |
| URL | ✓ | ✓ |
| Picklist (enum) | ✓ | ✓ |
| Multi-select picklist | ✓ | ✓ |
| Checkbox | ✓ | ✓ |
| Lookup (reference) | ✓ | ✓ |
| Master-Detail (parent-child) | ✓ | (similar via cascading-delete) |
| External Lookup | ✓ | ✓ |
| **Formula** (calculated) | ✓ | ✓ |
| **Rollup Summary** | ✓ | ✓ |
| **Auto Number** | ✓ | ✓ |
| Geolocation | ✓ | ✓ (lat/lng) |
| **Hierarchy** (self-referential) | ✓ | ✓ |
| **Address (compound)** | ✓ (street/city/state/zip/country/lat/lng) | ✓ (separate fields) |
| **Name (compound)** | ✓ (Salutation/First/Middle/Last/Suffix) | ✓ |
| **Person Account** | ✓ | (similar via Contact extension) |
| **JSON / Long Text Object** | ✗ | ✓ (Multi-line text up to 1MB) |

### What's interesting

- **Multi-currency awareness** — Salesforce's Currency type can track per-record currency + auto-convert via rates table. SMB usually doesn't need this; enterprise does.
- **Address as a compound type** — Salesforce ships it as a single field with internal subfields. Dataverse leaves it as separate fields. Sunfish's variant Address with USAddress (and future EU/MX) variants is more sophisticated than either.
- **Formula and Rollup Summary** are first-class. Formula computes from same record; Rollup aggregates across child records. Both rely on field-level dependencies + cascade-recompute.
- **Hierarchy** as a self-referential lookup — "Parent Account" on Account; "Reports To" on Contact. Matches Sunfish's parent-child reference need.

### What Sunfish should adopt

1. **`Picklist` + `Multi-select Picklist`** as distinct types from generic enum — they have specific UX expectations (dropdown vs multi-select chips).
2. **Hierarchy reference** — already adopted from Linear/Notion's sub-issue pattern.
3. **Auto Number** as a field-type — useful for `WorkOrderNumber` ("WO-2026-0042") that's friendlier than UUID for human display.
4. **DON'T** ship multi-currency in v1 (BDFL is USD-only); Currency = ISO 4217 alpha-3 + amount, single-currency invariant.

---

## 11. JSON Schema (Draft 2020-12)

The standards-track type system. Foundational for any of the above that need machine-readable schema definitions.

### Key concepts

- **Type keywords**: `string`, `number`, `integer`, `boolean`, `null`, `array`, `object`
- **String formats** (validation hints): `date-time`, `date`, `time`, `duration`, `email`, `idn-email`, `hostname`, `idn-hostname`, `ipv4`, `ipv6`, `uri`, `uri-reference`, `uri-template`, `iri`, `iri-reference`, `uuid`, `regex`
- **Composition**: `oneOf`, `anyOf`, `allOf`, `not`
- **Conditional**: `if/then/else`
- **References**: `$ref`, `$dynamicRef` for schema reuse
- **Validation keywords**: `minLength`, `maxLength`, `pattern`, `minimum`, `maximum`, `multipleOf`, `minItems`, `maxItems`, `uniqueItems`, `required`, `enum`, `const`
- **Vocabularies**: extensible; new keywords via vocab registration

### What's interesting

- **`oneOf` as the canonical discriminated union** — what Sunfish's variant types should align with. `Address` = `oneOf [USAddress, EUAddress, MXAddress]` with `discriminator` keyword (OpenAPI extension).
- **String formats** are a soft-validation layer — the `email` format suggests an email but doesn't enforce. JSON Schema deliberately keeps them as hints; strict validation is the consumer's choice.
- **`if/then/else`** is the standardized conditional-validation primitive — handles "if state = TX then field-X is required" without leaving JSON Schema. Cross-field rules UPF will revisit.

### What Sunfish should adopt

1. **JSON Schema as the validation core** — already proposed in earlier discussion. Confirmed.
2. **`oneOf` + discriminator** for variant types.
3. **String formats** as soft-typed primitives — `email`, `phone`, `url`, `uuid` get format hints + validation; the underlying type is `string` for storage.
4. **Conditional `if/then/else`** for cross-field rules — defer to that UPF.

---

## Synthesis: revised primitive catalog

Cross-source synthesis, in order of reuse frequency. Recommendations relative to v1 catalog (14 types):

### Confirmed in v1 (no change)

- **`Money`** — adopt FHIR/Schema.org/Salesforce shape; value + ISO-4217 currency
- **`Percentage`** — adopt; distinct from Quantity
- **`PersonName`** — already revised to CLDR/FHIR-aligned shape
- **`ContactInfo`** — revise to FHIR `ContactPoint` shape (system + value + use + rank)
- **`Address`** — variant pattern adopted
- **`GeoPoint`** (formerly LatLng) — adopt RFC 7946 coordinate order
- **`ConditionScore` / `Rating`** — already collapsed to single bounded-numeric Rating with optional labels

### Revised / collapsed

- **`MonetaryAmount`** ← collapse `Money` + `MoneyRange` per Schema.org pattern
- **`QuantitativeValue`** ← collapse `Measurement` + `Range` per Schema.org pattern (value + unit + optional minValue + maxValue)
- **`Period`** ← replaces both `DateRange` and `DateTimeRange` per FHIR pattern (start + end with optional time precision)

### New additions (from research)

- **`Identifier`** — system + value + use + period + assigner per FHIR. Subsumes APN, VIN, EIN, SSN, MLS-number, etc. as instances of one primitive.
- **`Coding` / `CodeableConcept`** — taxonomy entry from a registered code system + free-text fallback. Used for equipment classes, vendor specialties, deficiency categories.
- **`RecurrenceRule`** — RFC 5545 RRULE syntax. Required for inspection cadence, lease renewal reminders, monthly statements.
- **`Tag` / `TagList`** — keyword + multi-keyword taxonomy fields per Temporal Search Attribute model.
- **`Progress`** — 0-100% workflow progress; distinct semantic from Percentage.
- **`PersonReference`** — typed reference to a Party with display + presence affordances.
- **`InternationalizedText`** — Map<locale, text> for user-facing labels across multiple locales.
- **`AutoNumber`** — formatted human-friendly identifier (`WO-2026-0042`).
- **`Attachment`** — FHIR-shaped: contentType + url-or-blob-ref + size + hash + title.

### Deferred to v2 / on-demand

- **`GeoPolygon`** — for parcel boundaries; not Phase 2 priority
- **`MultiCurrency`** awareness — defer until international tenants
- **`Rollup`** — derived type, not a stored primitive
- **`Formula` / Computed Field** — defer to cross-field-rules UPF
- **`Color`** — non-essential for property management v1
- **`RichText`** — when admin-defined types need WYSIWYG; deferred until that need is concrete
- **`Signature`** — already in flight via ADR 0054; not a primitive but a kernel-tier substrate

### Removed from v1 catalog

- **`FullName`** — replaced by global-friendly `PersonName`
- **`Quantity`** — collapsed into `QuantitativeValue` (with discriminator for countable-vs-continuous if needed)
- **`Measurement`** — collapsed into `QuantitativeValue`
- **`MoneyRange`** — collapsed into `MonetaryAmount`
- **`DateRange` / `DateTimeRange`** — collapsed into `Period`
- **`ConditionScore`** — collapsed into `Rating`

### Final v1 primitive catalog (revised: 18 types)

| # | Type | Composition | Source |
|---|---|---|---|
| 1 | **`MonetaryAmount`** | value + currency + minValue? + maxValue? + period? | Schema.org / Salesforce |
| 2 | **`QuantitativeValue`** | value + unit + minValue? + maxValue? + countable-flag | Schema.org / FHIR Quantity |
| 3 | **`Percentage`** | decimal (0-100 implicit) | Common |
| 4 | **`Progress`** | decimal (0-100 percent-complete; workflow-semantic) | PM systems |
| 5 | **`Period`** | start + end (date or datetime) | FHIR |
| 6 | **`Duration`** | value + unit (days/months/years/etc.) | iCal / FHIR |
| 7 | **`RecurrenceRule`** | RFC 5545 RRULE expression | iCal |
| 8 | **`PersonName`** | CLDR-aligned; given[] + family[] + honorificPrefix? + honorificSuffix? + displayName + nameOrder + pronunciation? | W3C/CLDR/FHIR |
| 9 | **`PersonReference`** | typed-reference to a Party | PM systems |
| 10 | **`ContactPoint`** | system + value + use? + rank? + period? | FHIR |
| 11 | **`Address`** | variant: USAddress (others later) | FHIR + variant pattern |
| 12 | **`GeoPoint`** | longitude + latitude + elevation? + accuracy? | GeoJSON |
| 13 | **`Identifier`** | system + value + use? + period? + assigner? | FHIR |
| 14 | **`Coding`** | system + code + display? | FHIR |
| 15 | **`CodeableConcept`** | coding[] + text | FHIR |
| 16 | **`Rating`** | value + scale-min + scale-max + scale-step + labels? | Synthesis |
| 17 | **`Tag` / `TagList`** | keyword + multi-keyword | Temporal / common |
| 18 | **`InternationalizedText`** | Map<locale, text> | CMS systems |
| 19 | **`AutoNumber`** | formatted-string-with-counter | Salesforce |
| 20 | **`Attachment`** | contentType + (url \| blobRef) + size + hash? + title? | FHIR |

(20 primitives; 14 → 20 net change; 6 added, 6 collapsed/renamed.)

### Plus 2 first-class meta-concepts

- **`Variant`** — discriminated union (Address example uses this)
- **`Reference`** — typed pointer to an Entity with cardinality (1:1, 1:N, N:M) + parent-child semantics

### And string formats (validation hints, not primitives)

- `email`, `phone-e164`, `url`, `uuid`, `date-iso8601`, `time-iso8601`, `currency-code-iso4217`, `country-code-iso3166`, `language-code-bcp47`, `tz-iana`, `regex` (for inline-defined validation)

---

## Open questions for CEO

1. **Adopt the revised 20-primitive catalog?** Default = yes. Override = pin specific cuts/keeps.
2. **Adopt FHIR's `use` enum on contacts/addresses** (home/work/temp/old/billing) for property-management context (current vs former vs billing addresses)? Default = yes.
3. **Adopt iCal's `RecurrenceRule` (RFC 5545) verbatim**, or define a Sunfish-specific recurrence DSL? Default = adopt RFC 5545 verbatim (mature; battle-tested; libraries exist for .NET and JS).
4. **`Coding` + `CodeableConcept`** for taxonomies — sufficient, or should Sunfish ship a dedicated taxonomy management substrate (registered code systems with hierarchy) above Coding? Default = sufficient for v1; taxonomy management tooling deferred.
5. **`Variant` as first-class meta-concept** with discriminator + named variants — confirmed for Address; any other v1 use? Default: ship as substrate; first user is Address; future variants (`PaymentMethod`, `WorkOrderSource`) will compose.

---

## What I produce next (default sequence)

Per prior commitment:

1. ✅ Provider research (PR #229; deferred per CEO)
2. ✅ Permissions UPF (PR #230)
3. ✅ OSS primitives research (this PR)
4. **Next turn:** Cross-field rules UPF — same Stage 0/1/1.5/2 structure; ~3000 words
5. **Turn after:** Dynamic-forms substrate ADR — synthesizes UPF + research + rules + JSONB + admin-defined types + 20-primitive catalog + variant + reference + form rendering + storage model + sync semantics; ~1500-2500 lines
6. **Turn after that:** Cluster intake reconciliation update + COB hand-off updates per the new substrate

CEO override welcome at any step.

## Sign-off

CTO (research session) — 2026-04-29
