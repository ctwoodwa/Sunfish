# Internationalization

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Every user-visible string, every date/number/currency rendering, every template, every notification body, and every RTL layout concern across `packages/foundation-*`, `packages/ui-core`, `packages/ui-adapters-*`, `blocks-*`, and `apps/*`. Does not govern internal log messages, error codes used only by the audit envelope, or source-code identifiers.
**Companion docs:** [accessibility.md](accessibility.md), [component-principles.md](component-principles.md), [tokens-guidelines.md](tokens-guidelines.md), [documentation-framework.md](../product/documentation-framework.md), [coding-standards.md](../engineering/coding-standards.md), [testing-strategy.md](../engineering/testing-strategy.md), [ci-quality-gates.md](../engineering/ci-quality-gates.md), [vision.md](../product/vision.md) §5.

Sunfish's Pillar 5 commits culture and language as platform primitives, not feature-flagged enterprise upsells ([vision.md](../product/vision.md)). This document operationalizes that commitment into concrete process, code conventions, and quality gates. It reads alongside [accessibility.md](accessibility.md); the two pillars share test harnesses, CI gates, and regulatory footing.

## Baseline commitment

- **Unicode UTF-8 end-to-end.** Source files, database columns, wire formats, filenames, HTTP bodies, log records — all UTF-8. No legacy code pages. `<meta charset="utf-8">` on every HTML surface.
- **BCP-47 locale tags everywhere** ([RFC 5646](https://www.rfc-editor.org/rfc/rfc5646)). Never ad-hoc strings like `"english"` or `"us"`. Tags are normalized to canonical form (`en-US`, `es-419`, `zh-Hans`, `ar-SA`, `he-IL`, `fa-IR`).
- **Resource-file strings only.** Every user-visible string lives in a resource file (`.resx` on .NET, XLIFF-backed catalog on the JS side). Hardcoded English in component markup, templates, or notification bodies is a build failure — see CI gate below.
- **RTL-aware layout by default.** Every layout component works in both directions without per-component special-casing. CSS logical properties, not physical ones.
- **Explicit timezones on every datetime.** Naive `DateTime.Now` is banned in domain code. Store UTC with offset or IANA zone id; render in the tenant display timezone with optional user override.
- **[Unicode CLDR](https://cldr.unicode.org/) is the canonical locale-data source.** .NET's globalization stack and the browser's `Intl` APIs both resolve against CLDR; we do not ship our own locale tables.

## Locale resolution chain

For every request or session, the active locale is resolved in this authoritative order:

1. **Explicit request override** — `?culture=` query parameter or equivalent (test/demo use; not a production UX).
2. **Per-user preference** — stored on the user profile, updatable from the settings UI.
3. **Per-tenant default** — configured when a tenant is provisioned; carried in `TenantMetadata`.
4. **`Accept-Language` header** — parsed for the best match among supported locales.
5. **Platform default** — `en-US` until a deployment overrides it.

On .NET this is applied by setting `CultureInfo.CurrentCulture` (formatting) and `CultureInfo.CurrentUICulture` (strings) on each request; Blazor's `RequestLocalizationMiddleware` wires steps 1 and 4, and Sunfish middleware layers 2 and 3 on top. On the JS / Lit side, the same chain populates `html[lang]`, `html[dir]`, and the `Intl` constructors.

**Fallback semantics** follow BCP-47 parent-tag truncation: `fr-CA` falls through to `fr`, then to the source locale (`en-US`), never to an arbitrary sibling (`fr-CA` does **not** silently use `fr-FR` strings — `fr-CA` is either complete or it inherits the parent). Missing-key behavior at render time returns the source-locale value, logs a telemetry event tagged `i18n.missing_key`, and never shows a raw resource key to the user.

**Supported locales** are declared in `i18n/locales.json` at the repo root — the single authoritative list. A locale present in a `.resx` but absent from `locales.json` does not load; a locale listed in `locales.json` that fails the completeness gate fails CI. Adding a locale is a `sunfish-feature-change` pipeline item; it does not alter component or service APIs.

## String storage and extraction

### .NET side

Standard layout per package:

```
packages/<pkg>/
├── Resources/
│   ├── SharedResource.cs         ← dummy type for IStringLocalizer<SharedResource>
│   ├── SharedResource.resx       ← source locale (en-US)
│   ├── SharedResource.es.resx
│   ├── SharedResource.fr-CA.resx
│   ├── SharedResource.ar.resx
│   └── …
├── Components/…
└── <pkg>.csproj                  ← NeutralLanguage=en-US; all *.resx are EmbeddedResource
```

Rules:

- **One `.resx` per package family, under `Resources/`.** Keys are **stable dotted identifiers** — `grid.column.menu.sort.ascending`, not `"Sort ascending"`. English source text is the *value*; the key never changes when English copy is reworded.
- **`IStringLocalizer<T>` is the only injection point.** Components and services take `IStringLocalizer<T>` from DI; `T` names the resource file. No `ResourceManager` usage in application code.
- **Satellite assemblies** ship automatically per the .NET resource model — `Sunfish.UIAdapters.Blazor.resources.dll` under `es/`, `fr/`, `ar/` sub-folders. Build action on every `.resx` must be `EmbeddedResource`.
- **`DataAnnotations` localization is standard.** `[Required]`, `[StringLength]`, and the rest resolve error messages via a shared `SharedResource` type registered with `AddDataAnnotationsLocalization`.
- **XML `<remarks>` on resource keys.** Every key's translator note goes in the resx comment field — *"shown above the grid column menu; verb, not noun"* — so translators don't guess context.

### JS / Lit side

- **`@lit/localize` is the adopted tool** for Web Components authored under ADR 0017. Source strings are tagged with `msg()`:
  ```ts
  import { msg } from "@lit/localize";
  render() { return html`<button>${msg("Launch")}</button>`; }
  ```
- **XLIFF is the interchange format.** `lit-localize.json` sets `interchange.format: "xliff"` with `xliffDir: ./xliff/`. Translators work in XLIFF-native tools; no hand-edited JSON catalogs.
  ```json
  {
    "sourceLocale": "en",
    "targetLocales": ["es-419", "fr-CA", "ar-SA"],
    "tsConfig": "./tsconfig.json",
    "output": {
      "mode": "transform",
      "outputDir": "./dist/locales"
    },
    "interchange": { "format": "xliff", "xliffDir": "./xliff/" }
  }
  ```
- **Transform mode for production, runtime mode for dev.** Production builds emit a standalone bundle per locale (zero runtime localize overhead); dev builds use runtime mode so live locale-switch works during kitchen-sink demos.
- **Source locale is `en-US`.** `sourceLocale: "en"` in `lit-localize.json`; target locales tracked in `i18n/locales.json` at the repo root.
- **Parametric messages** use the Lit `str` tag with positional placeholders:
  ```ts
  msg(str`Welcome, ${name}`, { id: "shell.greeting" });
  ```
  Complex plural/select strings are authored as ICU MessageFormat literals that `@lit/localize` extracts verbatim into XLIFF.

### Templates (ADR 0005)

- Localized template variants are **keyed by BCP-47** inside the template envelope: `{ "en-US": {…}, "fr-CA": {…} }`.
- Tenant overlays (ADR 0005 Layer 3, JSON Merge Patch) may **add a locale without forking the base**. A Québec tenant ships `fr-CA` strings as an overlay; the base's English copy is unchanged.
- Missing-locale behavior: resolve up the BCP-47 chain, then to source locale. Never render a raw key to a user.

## Formatting conventions

- **Dates.** Store ISO-8601 UTC with offset (`2026-04-19T14:30:00Z`) or a zoned representation (`2026-04-19T09:30:00-05:00[America/Chicago]`). Display via `CultureInfo` on .NET (`ToString("d", CultureInfo.CurrentCulture)`) and `Intl.DateTimeFormat` on JS. Explicit timezone is mandatory at every display point; a naked `DateTime` rendered as "4/19/2026" with no zone context is a correctness bug, not a cosmetic issue.
- **Numbers.** Never string-interpolate raw numbers into UI strings (`$"There are {count} items"` is wrong in every locale). Use `ToString("N", culture)` / `Intl.NumberFormat`, or — for parametric messages — ICU MessageFormat parameters (`{count, number}`). Grouping separators, decimal marks, and digit shapes (Arabic-Indic vs. Western) come from the locale.
- **Currency.** Store ISO-4217 code alongside amount (`{ amount: 1299, currency: "USD" }`, where `amount` is minor units); display via `Intl.NumberFormat(locale, { style: "currency", currency })` / `ToString("C", culture)`. Currency code and locale are independent — a USD amount rendered in `fr-FR` is still USD, not EUR. Currency conversion is an explicit domain operation (`Foundation.Money`), never an implicit display-time transform.
- **Sort order.** Locale-aware collation via `CultureInfo.CompareInfo` on .NET and `Intl.Collator` on JS. Ordinal comparison is for machine identifiers only (GUIDs, slugs, storage keys); any user-visible list — tenant directories, contact names, dropdown options, grid rows — goes through locale-aware collation. Case and accent sensitivity are explicit collator options, not defaults that silently change by locale.
- **Names and addresses.** Name order (given-family vs. family-given) and address format vary by locale. Canonical name and address records carry structured fields (`givenName`, `familyName`, `honorific`, `line1`, `locality`, `administrativeArea`, `postalCode`, `country`) and render through a locale-aware formatter — never string-concatenate.

## RTL layout

- **Logical CSS properties only** in component CSS. `margin-inline-start`, `padding-inline-end`, `inset-inline-*`, `border-start-end-radius`. Physical `margin-left` / `margin-right` in new component code is a lint error (a follow-up analyzer adds this to [ci-quality-gates.md](../engineering/ci-quality-gates.md)).
- **`dir="rtl"` at the HTML root** when the active locale is RTL. Components use the `:dir(rtl)` selector for direction-specific rules; they never sniff `html[lang]` manually.
- **Mirroring rules.**
  - **Mirror:** chevrons, back/next arrows, progress bars, breadcrumb separators, drawer-open direction, resize handles.
  - **Do not mirror:** numerals, brand logos, media controls (play icon), the camera/photo icons, time-direction icons (clockwise is still clockwise).
  - **Data grids:** column order reverses; numeric cells keep LTR numeric orientation via `<bdi>` / `unicode-bidi: isolate`.
- **Snapshot test per component in both directions.** The parity test harness ([adapter-parity.md](../engineering/adapter-parity.md)) asserts an RTL snapshot for every layout-bearing component.

## Pluralization

- **ICU MessageFormat everywhere plural semantics matter** — never English-style `count === 1 ? "item" : "items"`. The resource value looks like:
  ```
  {count, plural, one {# item} other {# items}}
  ```
- **Languages with >2 plural forms are first-class.** Arabic has six categories (`zero`, `one`, `two`, `few`, `many`, `other`), Polish has four (`one`, `few`, `many`, `other`), Russian has four (`one`, `few`, `many`, `other`). The `few`/`many` split in Slavic languages is governed by the last digit and last two digits of the count — "2 książki" (few), "5 książek" (many), "22 książki" (few again). A translator cannot fix this with creative wording; the message envelope must expose every category the target locale needs.
- **Every pluralized string ships with explicit test cases** covering each category the target locales require — see testing below.
- **Never concatenate a count prefix to a localized noun.** `$"{count} {Loc["items"]}"` is wrong in every locale with agreement rules. Use a MessageFormat string.
- **Select (gender, variant) alongside plural.** `{gender, select, female {…} male {…} other {…}}` is the same mechanism; use it where pronoun or agreement forms differ. Nested plural-inside-select is legal and common for notification copy.
- **Ordinals are a separate selector.** `{n, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}` — do not hand-roll suffix rules.

## Timezones

- **Stored datetimes** are UTC-with-offset (`DateTimeOffset`) or zoned (IANA zone id — `America/New_York`, not Windows zone ids). Naive `DateTime.Now` / `new Date()` without explicit zoning is banned in domain code.
- **Displayed datetimes** render in the tenant display timezone by default, with per-user override. Aggregation bucket boundaries (daily reports, monthly rollups) use the tenant display zone so "Tuesday's inspections" means the same thing to everyone on the tenant.
- **.NET uses `TimeProvider`.** Domain services take `TimeProvider` via DI (never `DateTime.UtcNow` directly) for testability; tests use `Microsoft.Extensions.Time.Testing.FakeTimeProvider`. Long-range calendar arithmetic that needs civil-calendar correctness (DST transitions, historical zone changes) uses NodaTime.
- **Browser side** uses `Intl.DateTimeFormat` with an explicit `timeZone` option. Temporal API is tracked as a follow-up when it reaches Baseline support.
- **NativeAOT deployments** must preload ICU data — flagged on the relevant accelerator README.

## Translation management workflow

- **Source strings ship in English** (`en-US`) alongside the source tree. Translators never edit source code; they edit XLIFF (JS side) or `.resx` (satellite) files via the translation platform.
- **Platform recommendation: [Weblate](https://weblate.org/)** (OSS, self-hostable, GPL) as the tier-2 candidate per [GOVERNANCE.md](../../GOVERNANCE.md) triggers; [Crowdin](https://crowdin.com/open-source) (managed, free for OSS) is the fallback until self-hosted Weblate clears the GOVERNANCE triggers.
- **Translator onboarding.** External translators contribute via the chosen platform; each locale has a language coordinator listed in `i18n/coordinators.md`. Coordinators have merge rights on `i18n/**`; no other paths. GitHub-native contributors can also submit XLIFF or resx PRs directly if they prefer — the translation platform and the repo are bidirectionally synced.
- **Review cycle.** Translator submits → coordinator reviews in-platform → platform PR auto-opens → CI runs XLIFF-validity + no-missing-keys checks → coordinator merges. No full code review required on `i18n/**` translation-only PRs; structural XLIFF changes (key additions, context edits, source rewrites) still go through standard review.
- **Glossaries and term bases.** Each locale ships a glossary in the translation platform so "tenant," "resident," "lease," "capability," "federation," and other platform-specific terms are translated consistently across packages. Bundles may register their own glossaries (a medical bundle's "encounter" differs from a property bundle's "inspection").
- **Source-string stability.** English source copy is not cost-free to change — every reword invalidates the match in every target locale. When the meaning is unchanged, edit the `.resx` value in place (translations stay attached); when the meaning changes, introduce a new key and deprecate the old one for one minor version so translators can migrate.
- **Locale completeness, pre-1.0.**
  - English (`en-US`) is the complete baseline.
  - Spanish (`es-US`, `es-419`) and French (`fr-CA`, `fr-FR`) land alongside the small-medical-office and small-landlord pilots respectively.
  - **Arabic (`ar-SA`) is baked in early** even before a target market requires it — to catch RTL layout regressions before they compound. A "50% strings, 100% layout" Arabic build is the continuous CI target.
- **Post-1.0 locale additions** flow through the normal `sunfish-feature-change` variant; adding a locale does not change APIs.

## Automated testing

- **Unit tests for pluralization** — every ICU MessageFormat string has test cases covering each plural category required by the target locales, at minimum `one` and `other` for English baseline plus the full Arabic six-way set when Arabic is a declared target and the Slavic four-way set when Polish or Russian are declared. Test fixtures live alongside the `.resx` / XLIFF and are generated from the MessageFormat grammar so a plural-form change in source fails the suite until the fixtures are refreshed.
- **Snapshot tests for RTL layout** — every layout-bearing component renders an RTL snapshot in the adapter parity harness; diff against LTR baseline catches leaked physical properties. The snapshot suite runs against `ar-SA` even when Arabic strings are incomplete — layout correctness is locale-independent once direction is set.
- **axe-core runs in both LTR and RTL modes** on every component test suite. RTL accessibility is not a separate concern; it is the same accessibility bar in both directions. See [accessibility.md](accessibility.md).
- **Visual regression** — the kitchen-sink app is captured in both LTR (`en-US`) and RTL (`ar-SA`) for every provider theme. A surprise pixel diff in RTL without a matching LTR diff indicates a direction-specific regression.
- **CI fail gate — no hardcoded user-visible strings.** An analyzer (`Sunfish.Analyzers.NoHardcodedStrings`, follow-up package) flags string literals in component markup, notification builders, and template authoring APIs. The allowlist is narrow: test fixtures, developer logs, and explicitly tagged `[UnlocalizedOk]` members.
- **CI fail gate — untranslated keys.** A locale that declares itself "complete" (per `i18n/locales.json`) must have zero missing keys; a partial locale (`ar-SA` during bake-in) publishes a percentage and fails the build only when coverage regresses.
- **CI fail gate — naive `DateTime` / `new Date()`.** A Roslyn analyzer forbids `DateTime.Now`, `DateTime.UtcNow`, and unqualified `new DateTime(...)` in domain and UI code; the fix is `TimeProvider` injection. Tests may use literal `DateTimeOffset` constants.
- **CI fail gate — physical CSS properties in new component code.** A stylelint rule forbids `margin-left`, `margin-right`, `padding-left`, `padding-right`, `left`, `right` in `packages/ui-adapters-*` component CSS; providers' own legacy styles are allowlisted during migration.

## Tenant-level customization

- **Template overlays carry locale-keyed strings.** A tenant's `TenantTemplateOverlay` (ADR 0005 Layer 3) can add or override any locale without touching the base template.
- **Tenant admin UI for locale addition** is a tier-2 capability — the contract is stable, the UI ships with the tenant-admin block. Until then, tenant overlays are added by the Sunfish services team or via the extension-fields CLI.
- **Per-tenant terminology overrides** use the extension-fields catalog + locale-keyed display strings. A property-management tenant that says "resident" instead of "tenant" registers a `displayStringOverride` per BCP-47 locale; the base strings are untouched and other tenants see the default.

## Commercial translation offering

Per [vision.md §Business model](../product/vision.md), Sunfish offers **professional translation and localization as a commercial service** — targeted at bundles entering new markets. Scope: translator sourcing, glossary and terminology management, review workflow operation, and sign-off of the target-market-ready locale. This is a services line, not a gated feature; the OSS pipeline supports the same workflow for anyone willing to run it themselves.

## Follow-ups and known tooling gaps

- **`@lit/localize` wiring** waits on the ADR 0017 Lit-based UI package landing. Until then, JS-side strings are hand-keyed against a placeholder catalog.
- **Weblate self-host** is deferred per GOVERNANCE tier-2 trigger; Crowdin-for-OSS carries the workflow in the interim.
- **Tenant-level locale-override UI** is P2. The contract ships with the tenant-admin block; the UI follows once a pilot tenant requests it.
- **`Sunfish.Analyzers.NoHardcodedStrings`** analyzer is a follow-up package — flagged in [ci-quality-gates.md](../engineering/ci-quality-gates.md) as a deferred gate.
- **Temporal API** adoption on the JS side awaits Baseline browser support; `Intl.DateTimeFormat` + explicit `timeZone` covers the gap.

## Cross-references

- [vision.md](../product/vision.md) §Pillar 5 — the commitment this document operationalizes.
- [accessibility.md](accessibility.md) — parallel inclusivity pillar; shared test harness, shared CI gates.
- [component-principles.md](component-principles.md) — localization contract belongs on every component's public surface, alongside accessibility.
- [tokens-guidelines.md](tokens-guidelines.md) — BCP-47 tags appear in token-naming conventions for locale-sensitive variants.
- [documentation-framework.md](../product/documentation-framework.md) — `articles/globalization/*` how-to migration target under Diátaxis.
- [coding-standards.md](../engineering/coding-standards.md) — `TreatWarningsAsErrors` and nullability rules apply to localization resource access.
- [testing-strategy.md](../engineering/testing-strategy.md) — where pluralization and RTL test cases are authored.
- [ci-quality-gates.md](../engineering/ci-quality-gates.md) — where the hardcoded-string and missing-key gates are registered.
- [ADR 0005](../../docs/adrs/0005-type-customization-model.md) — template-overlay three-way merge that supports locale additions without forking.
- [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md) — Lit basis that motivates `@lit/localize` adoption on the JS side.
- [BCP-47 / RFC 5646](https://www.rfc-editor.org/rfc/rfc5646) — locale-tag format.
- [Unicode CLDR](https://cldr.unicode.org/) — canonical locale-data source.
- [ICU MessageFormat](https://unicode-org.github.io/icu/userguide/format_parse/messages/) — pluralization and select grammar.
- [.NET Globalization](https://learn.microsoft.com/dotnet/core/extensions/globalization) and [Localization](https://learn.microsoft.com/dotnet/core/extensions/localization) — the `IStringLocalizer<T>` + resx + satellite-assembly stack.
- [.NET `TimeProvider`](https://learn.microsoft.com/dotnet/standard/datetime/timeprovider-overview) and [`FakeTimeProvider`](https://learn.microsoft.com/dotnet/core/extensions/timeprovider-testing) — timezone abstraction for testability.
- [NodaTime](https://nodatime.org/) — civil-calendar correctness for complex zone arithmetic.
- [@lit/localize overview](https://lit.dev/docs/localization/overview/) — `msg()` tagging, XLIFF interchange, runtime vs transform modes.
- [Weblate](https://weblate.org/) — OSS translation platform; tier-2 self-host candidate.
- [Crowdin for Open Source](https://crowdin.com/open-source) — interim managed platform.
