# ICU4N Health Check — 2026-04-25

**Purpose:** Week-0 triage gate before Sunfish commits to ICU4N as its CLDR/ICU MessageFormat
implementation for 12 target locales (ar, hi, zh, ja, ko, ru, pt-BR, es-LA, fr, de, en, and
their regional variants).

---

## 1. Repository Activity

Source: [https://github.com/NightOwl888/ICU4N](https://github.com/NightOwl888/ICU4N)

| Signal | Value |
|---|---|
| Stars | 44 |
| Forks | 8 |
| Watchers (subscribers) | 7 |
| Open issues | 24 |
| Last commit | 2026-04-24 (yesterday) |
| Last release tag | `v60.1.0-alpha.439` — 2025-11-02 |
| Repo status | Active (not archived) |
| License | Apache-2.0 |
| Sole maintainer | NightOwl888 (single contributor) |

The repo is actively worked on — the most recent commit landed yesterday, fixing two `UChar` test
failures. However, all 439 releases are pre-release alphas; there has never been a stable v1.x.
The star count (44) is very low for a library Sunfish would depend on for production i18n.
The project is effectively a one-person effort.

Open issue sample (top 10 by recency) includes: C# 14.0 support tasks, satellite assembly warnings
with .NET 10 SDK, a high-priority failing collation monkey-test
(`CollationCreationMethodTest::TestRuleVsLocaleCreationMonkey`), and a pending task to remove
resource data from the repo itself. No MessageFormat-specific blockers were visible in the open
set, but the single-maintainer bus factor is a structural concern.

---

## 2. CLDR Version Lag

ICU4N's version number (`60.1.x`) is the upstream ICU version it ports, not an independent version.
ICU 60 (released 2017-10-26) bundled **CLDR 32** and Unicode 10.0.

Sources:
- [ICU 60 release page — icu.unicode.org](https://icu.unicode.org/download/60): confirms CLDR 32 data
- [unicode-org/icu releases](https://github.com/unicode-org/icu/releases): latest upstream is **ICU 78.3** (released 2026-03-17), which bundles CLDR 46
- [unicode-org/cldr latest release](https://github.com/unicode-org/cldr/releases/latest): **CLDR 48.2** (released 2026-03-17)

| Item | Version | Released |
|---|---|---|
| ICU4N (NuGet) | ICU 60 base / CLDR 32 | ICU 60: Oct 2017 |
| Upstream ICU (latest stable) | ICU 78.3 | 2026-03-17 |
| CLDR (latest stable) | CLDR 48.2 | 2026-03-17 |
| **CLDR lag** | **16 major versions behind** | ~8.5 years |

**This is the critical finding.** ICU4N bundles CLDR 32 locale data. CLDR has shipped 16 major
versions since then (33 through 48). Locale data added between CLDR 32 and CLDR 48 includes Arabic
extended plural rules, Hindi conjunct cluster handling, Korean and Japanese calendar fixes, and the
full MessageFormat 2.0 specification (introduced in CLDR 45). Sunfish's Arabic and Hindi plural
rules in particular depend on post-CLDR-32 refinements.

---

## 3. Test Coverage and CI Status

Source: [github.com/NightOwl888/ICU4N — .github directory](https://github.com/NightOwl888/ICU4N/tree/main/.github)

| Signal | Status |
|---|---|
| GitHub Actions workflows | 1 workflow: "Copilot code review" (PR reviewer bot, added 2026-04-20) |
| CI build/test workflow | **None found via API** |
| Azure Pipelines templates | Present under `.build/azure-templates/` (run-tests, publish-test-results) |
| Test framework | `src/ICU4N.TestFramework` — ported from ICU4J test harness |
| Test project | `src/ICU4N.Dev.Test.*` namespaces visible in tree |

There is no public GitHub Actions CI badge or workflow running automated tests on push/PR. The
`.build/azure-templates/` tree suggests tests run on a private Azure Pipelines instance.
The high-priority open issue "Investigate Failing Test:
`CollationCreationMethodTest::TestRuleVsLocaleCreationMonkey`" (open since 2024-11-25) indicates
at least one known intermittently-failing test has been unresolved for five months.

---

## 4. Binary Size

Source: [nuget.org/packages/ICU4N](https://www.nuget.org/packages/ICU4N)

| Package | Version | .nupkg size | Total NuGet downloads |
|---|---|---|---|
| ICU4N (core) | 60.1.0-alpha.439 | 7.19 MB | 1.8 M |
| ICU4N.Resources | 60.1.0-alpha.439 | 7.87 MB | 553.6 K |
| ICU4N.Transliterator | latest alpha | 910 KB | 908.8 K |
| ICU4N.LanguageData | latest alpha | 2.17 MB | 88.0 K |

A minimal integration (core + resources) ships roughly **15 MB of compressed NuGet data**,
expanding further on disk. For Sunfish's 12-locale target the `ICU4N.Resources` satellite
assemblies would be scoped to only required locales via `SatelliteResourceLanguages`, reducing
deploy size materially — but the full package graph is still heavyweight relative to alternatives.

GitHub release artifact for `v60.1.0-alpha.439`: `ICU4N.60.1.0-alpha.439.nupkg` — 7,529,267 bytes
(confirmed via [GitHub releases API](https://api.github.com/repos/NightOwl888/ICU4N/releases/assets/311547334)).

---

## 5. Verdict

**PIVOT TO FALLBACK**

### Rationale

| Factor | Assessment |
|---|---|
| CLDR data currency | Red — 16 versions behind (CLDR 32 vs. CLDR 48.2); Arabic/Hindi plural rules are materially stale |
| MessageFormat 2.0 | Red — MF2 arrived in CLDR 45; ICU4N has no roadmap item for it |
| Stable release | Red — 439 pre-release alphas, no v1.x in 8 years |
| CI transparency | Yellow — no public GitHub Actions CI; private Azure Pipelines |
| Bus factor | Red — single maintainer |
| Download volume | Yellow — 1.8 M total pulls shows real usage, but mostly pre-ICU4N, via NuGet |
| Active commits | Green — commit yesterday confirms maintainer engagement |
| License | Green — Apache-2.0, compatible |

ICU4N's core problem for Sunfish is not activity — it is data staleness. Plural rules for Arabic
(which has 6 plural forms with CLDR refinements post-32) and Hindi are the exact use cases CLDR
has improved most aggressively since 2018. Shipping CLDR-32 data in 2026 is a correctness risk,
not merely a currency concern.

### Named Fallback: Two-Layer Strategy

**Layer 1 — Plural/select logic:** [SmartFormat.NET](https://github.com/axuno/SmartFormat)
(MIT, actively maintained, CLDR plural rules kept current separately) combined with
[Humanizer](https://github.com/Humanizr/Humanizer) for locale-aware formatting primitives.
SmartFormat supports ICU-style `{count, plural, one{...} other{...}}` syntax and can be wired
behind `IStringLocalizer<T>`.

**Layer 2 — Number/date/currency formatting:** .NET's own `System.Globalization` (`CultureInfo`,
`NumberFormatInfo`, `DateTimeFormatInfo`) covers the 12 target locales natively on .NET 8+ and
tracks CLDR data via the OS or the [ICU-on-.NET globalization mode](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization)
(`DOTNET_SYSTEM_GLOBALIZATION_USENLS=false`), which bundles ICU 74+ on .NET 8.

This two-layer approach gives Sunfish full ICU plural/select semantics without taking a dependency
on a single-maintainer library frozen at a 2017 ICU snapshot.

**If ICU4N is required anyway** (e.g., transliteration for Cyrillic/CJK in a future workstream):
adopt it as an optional, feature-gated dependency only — not as the core i18n foundation.

---

## References

- [NightOwl888/ICU4N — GitHub](https://github.com/NightOwl888/ICU4N)
- [ICU4N on NuGet](https://www.nuget.org/packages/ICU4N)
- [ICU4N.Resources on NuGet](https://www.nuget.org/packages/ICU4N.Resources/)
- [ICU 60 release notes — icu.unicode.org](https://icu.unicode.org/download/60)
- [unicode-org/icu releases (ICU 78.3 latest)](https://github.com/unicode-org/icu/releases)
- [unicode-org/cldr releases (CLDR 48.2 latest)](https://github.com/unicode-org/cldr/releases/tag/release-48-2)
- [.NET globalization ICU mode — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization)
- [SmartFormat.NET — GitHub](https://github.com/axuno/SmartFormat)
