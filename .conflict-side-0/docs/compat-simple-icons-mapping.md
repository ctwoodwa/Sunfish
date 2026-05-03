# compat-simple-icons Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Simple Icons code maps to after migration. Any change to an entry
> (promoting a parameter from "mapped" to "throws", changing a default, adding a
> divergence, expanding the `SimpleIconSlug` starter set) is a **breaking change** for
> consumers and must land under the policy gate in the same PR as the code change. See
> `packages/compat-simple-icons/POLICY.md`.

## Conventions

- **Mapped** — Parameter value translates 1:1 to a Sunfish parameter / CSS class on
  the emitted `<i>`.
- **Forwarded** — Attribute is passed through via `AdditionalAttributes` (e.g. `class`,
  `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic
  parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 3.

---

## License — CC0-1.0 (public domain)

Simple Icons' upstream SVGs and slug catalog are licensed **CC0-1.0**. This is **the most
permissive license in the compat-icon-expansion workstream**: no runtime attribution,
no license-notice preservation in rendered output, no derivative-work restrictions.

**This is NOT the norm for icon libraries.** Every other compat-icon package ships
against an MIT, Apache-2.0, ISC, or CC-BY-4.0 upstream — all of which carry at least
a notice / attribution obligation. Simple Icons' CC0-1.0 release is a deliberate
choice by the maintainers to remove downstream friction.

Recommended practice despite the absence of a legal obligation:

1. **Review each depicted brand's trademark / usage policy.** CC0-1.0 covers the
   artwork, not the brands. Most brands publish usage policies covering the
   common "indicator of integration / compatibility" case
   (e.g. "Sign in with GitHub", "Share to Twitter", "Pay with Stripe").
2. **Cite Simple Icons' public-domain release as a courtesy.** The project does
   substantial ongoing curation work. A footer note or `/credits` page citation
   sustains the community that maintains the catalog — even though none is
   legally required.

See `packages/compat-simple-icons/POLICY.md` for the full license-scope discussion.

---

## SimpleIcon

- **Upstream target (framework-agnostic):** `<i class="si si-github"></i>`
- **Common Blazor wrapper shape:** `<SimpleIcon Slug="github" />` — identifier is
  the brand's lowercase-alphanumeric slug.
- **Sunfish compat target:** `<SimpleIcon Slug="SimpleIconSlug.Github" />` (typed)
  or `<SimpleIcon SlugString="github" />` (raw — recommended for most consumers,
  see below).
- **Rendered markup:** `<i class="si si-github sf-simple-icon [fs-*]"
  style="color: #181717" role="..." aria-label="..." ...AdditionalAttributes></i>`.

| Parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Slug` | `SimpleIconSlug?` | Translated via `SimpleIconSlugExtensions.ToSlug(SimpleIconSlug)` → emitted as `si-<slug>` on the `<i>` element. Starter set covers 50 brands out of Simple Icons' ~2,800. | Supported |
| `SlugString` | `string?` | Emitted verbatim as `si-<SlugString>`. Bypasses the typed enum. **Expected primary call-site** for Simple Icons consumers because the starter enum covers under 2% of the upstream catalog. When both `Slug` and `SlugString` are set, `SlugString` wins and a warning is logged. | Supported |
| `Size` | `Sunfish.Foundation.Enums.IconSize?` | Small → `fs-6`, Medium → `fs-5`, Large → `fs-3`, ExtraLarge → `fs-1`. Null → no size class (inherits ambient font-size). | Supported |
| `Color` | `string?` | Passed through as inline `style="color: @Color"`. Consumers typically pass the brand's official hex (e.g. `"#181717"` for GitHub, `"#1DA1F2"` for Twitter) or `"currentColor"` for monochrome. No validation. **Simple Icons' distinguishing feature vs. other icon libraries.** | Supported |
| `AriaLabel` | `string?` | When set, emits `role="img" aria-label="..."`; otherwise `role="presentation" aria-hidden="true"`. | Supported |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes` — merged with the compat-emitted class string and inline style. | Forwarded |

### Divergences

- **`Color` is a first-class parameter.** Unlike `FontAwesomeIcon` or
  `BootstrapIcon` — which defer color styling to consumer CSS — `SimpleIcon`
  exposes a dedicated `Color` parameter that threads to inline `style="color: ..."`.
  This is a deliberate nod to Simple Icons' distinguishing feature: the project
  curates each brand's **official color** (GitHub `#181717`, Twitter `#1DA1F2`,
  Google `#4285F4`, Stripe `#635BFF`, etc.). Consumers rendering brand icons in
  their official colors improves recognition; threading that through as a
  first-class parameter rather than making consumers write `style="color:..."`
  via splat is worth the API-surface cost.
- **Enum-naming divergence from `compat-bootstrap-icons`.** Bootstrap's compat
  package uses `IconName` + `Name` parameter; this package uses `SimpleIconSlug`
  + `Slug` parameter. The rename reflects Simple Icons' own terminology — the
  project calls brand identifiers "slugs" uniformly.
- **Simple Icons assets must be loaded by the consumer.** The compat package
  does NOT emit a `<link>` or bundle any sprite / stylesheet / webfont. Consumers
  continue loading their chosen Simple Icons distribution (CDN `<link>`, NPM
  install, self-hosted sprite, or inlined SVGs keyed to the `si-*` classes).
  Without a Simple Icons stylesheet loaded, the `<i>` renders empty.
- **`Size` semantics differ from upstream.** Simple Icons has no native `Size`
  parameter (sizing is done via `font-size` on the surrounding element). This
  package accepts the Sunfish `IconSize` enum and maps to Bootstrap's `fs-*`
  utility classes — consistent with `compat-bootstrap-icons` for migration
  symmetry. Consumers who need finer control can omit `Size` (inheriting ambient
  font-size) and apply their own `style="font-size:..."` via attribute splat.
- **`Slug` vs `SlugString` precedence.** When both are set, `SlugString` wins
  and a warning is logged — intentional source-shape-parity trade-off, same as
  `compat-bootstrap-icons`.

### Cross-reference — typed enum + raw-string escape hatch pattern

This package follows the typed-enum + raw-string-escape-hatch pattern established
by `compat-bootstrap-icons` (Phase 2C). See
[`docs/compat-bootstrap-icons-mapping.md`](./compat-bootstrap-icons-mapping.md) for
the pattern's rationale and expected-usage notes. The key difference: on
`BootstrapIcon`, `NameString` is a fallback — the typed enum covers the common
case. On `SimpleIcon`, `SlugString` is expected to be the **primary** call-site,
because a 50-brand starter enum covers under 2% of Simple Icons' ~2,800 upstream
brands. Shipping a complete typed enum would be impractical to maintain against a
catalog that churns with every brand-identity refresh upstream.

---

## SimpleIconSlug starter set

Phase 3C ships a **50-slug starter set** covering high-recognition brands in the
categories most app-migration scenarios include (Git forges, social login, payment
providers, dev tooling). Simple Icons' upstream catalog exceeds **2,800 brand icons**.

| Category | Slugs |
|---|---|
| Git forges | `Github`, `Gitlab`, `Bitbucket` |
| Social networks | `Twitter`, `Facebook`, `Instagram`, `Linkedin`, `Youtube`, `Tiktok` |
| Messaging / collaboration | `Discord`, `Slack`, `Telegram`, `Whatsapp` |
| Big tech | `Microsoft`, `Google`, `Apple`, `Amazon`, `Meta` |
| Media / streaming | `Netflix`, `Spotify` |
| Storage / content / community | `Dropbox`, `Pinterest`, `Reddit`, `Stackoverflow`, `Medium` |
| Commerce / payments | `Wordpress`, `Shopify`, `Stripe`, `Paypal`, `Visa`, `Mastercard` |
| Design tools | `Figma`, `Adobe`, `Sketch` |
| DevOps / infrastructure | `Docker`, `Kubernetes` |
| Runtimes / languages | `Nodejs`, `Python`, `Rust`, `Go`, `Dotnet` |
| Front-end frameworks | `React`, `Vuejs`, `Angular` |
| Languages / platforms | `Typescript`, `Javascript`, `Html5`, `Css3` |
| CSS frameworks | `Tailwindcss`, `Bootstrap` |

Consumers whose brand is not in the starter set can:

1. **Use the `SlugString` parameter** (expected primary path) to pass the raw
   Simple Icons slug: `<SimpleIcon SlugString="opentelemetry" />`.
2. Submit a policy-gated PR to extend the `SimpleIconSlug` enum and `ToSlug()` map.

### Divergences

- **Not the full catalog.** Shipping 2,800+ enum members would inflate the package
  and lock us into upstream Simple Icons brand-identity evolutions (every rebrand
  — Twitter → X, Facebook → Meta, etc. — would churn the enum). The starter set
  is intentionally scoped; expanding it is cheap but deliberate.
- **Naming is hand-authored.** `ToSlug()` is a `switch` expression rather than a
  derived `ToString().ToLowerInvariant()`, because canonical Simple Icons slugs
  drop special characters that a naive lowercase transform would not handle
  (`Node.js` → `nodejs`, `.NET` → `dotnet`, `Vue.js` → `vuejs`, `HTML5` →
  `html5`). Future additions should add an explicit arm.
- **Twitter / X legacy alias.** `SimpleIconSlug.Twitter` maps to `"twitter"`,
  not `"x"`. Upstream Simple Icons now publishes the post-rebrand mark under
  `"x"` but preserves the legacy `"twitter"` slug. The `"twitter"` slug matches
  most migrator source-shape (pre-rebrand); consumers who want the new-brand
  mark can use `<SimpleIcon SlugString="x" />`.

---

## Color pass-through reference (examples)

Per the "Color is a first-class parameter" divergence above. No validation is
performed on the value — any CSS color expression works.

| Slug | Official brand color | Example call-site |
|---|---|---|
| `Github` | `#181717` | `<SimpleIcon Slug="SimpleIconSlug.Github" Color="#181717" />` |
| `Twitter` (pre-rebrand blue) | `#1DA1F2` | `<SimpleIcon Slug="SimpleIconSlug.Twitter" Color="#1DA1F2" />` |
| `Google` | `#4285F4` | `<SimpleIcon Slug="SimpleIconSlug.Google" Color="#4285F4" />` |
| `Stripe` | `#635BFF` | `<SimpleIcon Slug="SimpleIconSlug.Stripe" Color="#635BFF" />` |
| `Spotify` | `#1DB954` | `<SimpleIcon Slug="SimpleIconSlug.Spotify" Color="#1DB954" />` |
| *(monochrome — inherit text color)* | — | `<SimpleIcon Slug="SimpleIconSlug.Github" Color="currentColor" />` |

---

## Future coverage

Deferred to follow-up PRs (track as separate ICM intake items if prioritized):

- **Enum starter-set expansion** — per-request additions under the policy gate.
- **Hex-color lookup helper** — a companion `SimpleIconColors.For(SimpleIconSlug)`
  static lookup returning each brand's official hex. Would save consumers the
  step of looking up each color manually. Gated on maintenance cost (upstream
  brand-color refreshes would churn the lookup).
- **Direct SVG inlining** — a future mode where the compat package inlines
  Simple Icons SVGs (CC0-1.0 allows this without attribution) rather than
  relying on the consumer's stylesheet. Would eliminate the setup step but
  violates the no-vendor-NuGet invariant unless we vendor the SVGs into the
  Sunfish repo — a separate policy decision.
- **Post-rebrand Twitter alias** — if migrator traffic prefers the `"x"` slug,
  evaluate renaming `SimpleIconSlug.Twitter` → `SimpleIconSlug.X` (breaking) or
  adding both members (cheap, minor duplication).

---

## Notes for future compat-icon packages

These notes seed architecture decisions for icon-compat packages shipping after
Simple Icons:

1. **Brand / logo packages need a dedicated color parameter.** `SimpleIcon`'s
   `Color` parameter maps directly to inline `style="color: ..."` — an explicit
   recognition that brand icons are frequently rendered in each brand's
   official palette. Non-brand icon packages (`compat-bootstrap-icons`,
   `compat-lucide`, `compat-heroicons`, `compat-octicons`) can defer color to
   CSS on the surrounding element without loss.
2. **License permissiveness does NOT imply brand-permissiveness.** Simple Icons'
   CC0-1.0 is the most permissive upstream in the workstream, but depicted
   brands retain trademark rights. POLICY.md captures this explicitly — future
   brand-icon compat packages should do the same.
3. **Raw-string escape hatch is primary, not fallback, when the catalog is
   large.** Bootstrap Icons ships ~2,000 icons — a 50-icon starter set covers
   the common case, `NameString` is the escape hatch. Simple Icons ships
   ~2,800 brands, but the top-50 most-migrated fraction is much smaller as a
   share of the whole — `SlugString` is expected to see heavier traffic than
   `NameString` did. Phase-3 brand-catalog packages should plan for this shift.
