# Global-First UX — Decisions Log

Append-only. New entries at the top. Older entries at the bottom.

---

## 2026-04-25 — ICU4N → SmartFormat.NET + .NET 8 System.Globalization (PIVOT)

**Triggering condition:** Week-0 triage gate (spec Section 3A) on ICU4N health.
[Memo](../../icm/01_discovery/output/icu4n-health-check-2026-04-25.md) finding:
ICU4N is maintained but ports ICU 60 (Oct 2017) with CLDR 32. Latest CLDR is 48.2
(Mar 2026) — 16 major versions behind. Arabic/Hindi plural rules materially stale;
MessageFormat 2.0 absent from roadmap; single-maintainer bus factor; 439 pre-release
alphas with no v1.x in 8 years.

**Original spec position:** Adopt ICU4N as the CLDR / ICU MessageFormat implementation
behind `IStringLocalizer<T>`.

**Chosen alternative — two-layer strategy:**
- **Plural/select/message logic:** [SmartFormat.NET](https://github.com/axuno/SmartFormat)
  (MIT, actively maintained, CLDR plural rules kept current). Supports ICU-style
  `{count, plural, one{...} other{...}}` syntax. Wired behind `IStringLocalizer<T>`.
- **Number/date/currency formatting:** .NET 8+ `System.Globalization` in ICU mode
  (`DOTNET_SYSTEM_GLOBALIZATION_USENLS=false`; bundles ICU 74+).

**Downstream impact:**
- Task 14 (ICU4N wrapper scaffold) — wrapper interface stays, implementation pivots
  to SmartFormat.NET. Rename `SunfishLocalizer` implementation class if useful, but
  the public `ISunfishLocalizer` contract is unchanged.
- Task 15 (en/ar/ja smoke tests) — test SmartFormat.NET behavior rather than ICU4N.
- Spec Section 3A needs a revision note pointing to this decisions.md entry; the
  ICU4N → SmartFormat swap does not change the broader Section 3A architecture.
- If transliteration becomes needed in a later workstream, ICU4N may be adopted as
  an optional, feature-gated dependency only (not the core i18n foundation).

**Confidence:** High. Three independent evidence lines converge (GitHub API confirms
`v60.1.0-alpha.*` release train; ICU 60 release notes confirm CLDR 32 bundling;
upstream CLDR 48.2 release confirms the gap).

---

*(Decisions are appended here as rollback criteria trigger or tool choices pivot.)*
