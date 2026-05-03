# XLIFF Tool Ecosystem Survey: .NET `.resx <-> XLIFF` Pipeline

**Date:** 2026-04-26
**Author:** Research assistant (AI-assisted)
**Purpose:** Inform Sunfish build-vs-adopt decision for a `.resx <-> XLIFF` conversion pipeline
feeding Weblate for 12-locale translation (Arabic RTL, Hindi, Chinese zh-Hans/zh-Hant, Japanese,
Korean, Russian, Portuguese-BR, Spanish-LA, French, German).

---

## 1. Existing Tool Landscape

### 1a. Tools Surveyed

| Tool | Type | XLIFF Ver | Last Active | Status |
|---|---|---|---|---|
| [Microsoft Multilingual App Toolkit (MAT) v4.1](https://learn.microsoft.com/en-us/windows/apps/design/globalizing/use-mat) | VS extension + MSBuild | 1.2 only | Oct 2025 EOL | **Discontinued** [1] |
| [dotnet/xliff-tasks](https://github.com/dotnet/xliff-tasks) (`Microsoft.DotNet.XliffTasks`) | MSBuild package | 1.2 (.xlf) | Active (dotnet org) | Maintained; internal-Microsoft use [2] |
| [dotnet/ResXResourceManager](https://github.com/dotnet/ResXResourceManager) | VS ext + standalone | 1.2 only | Feb 2025 (v1.104) | Actively maintained [3] |
| [fmdev.XliffParser](https://www.nuget.org/packages/fmdev.XliffParser) | Parser library | 1.2 | May 2017 | **Abandoned** [4] |
| [Xliff.OM](https://www.nuget.org/packages/Xliff.OM/) (`microsoft/XLIFF2-Object-Model`) | Object model | 2.0 | ~2017 (no NuGet pkg; `Xliff.OM` published 2017) | Archived/no owner [5] |
| [Xliff.OM.NetStandard](https://www.nuget.org/packages/Xliff.OM.NetStandard) | Object model fork | 2.0 | Sep 2022 (v2.0.1) | Community fork; dormant since 2022 [6] |
| [Blackbird.Xliff.Utils](https://www.nuget.org/packages/Blackbird.Xliff.Utils) | Manipulation library | 1.2 | 2024–2025 (v1.1.14) | Active; targets .NET 8+ [7] |
| [LocalizationProvider.Xliff](https://www.nuget.org/packages/LocalizationProvider.Xliff) | DbLocalizationProvider bridge | 1.2/2.0 unclear | 2024 (v8.2.6) | Active; tied to DbLocalizationProvider ecosystem |
| [OrchardCore Localization](https://docs.orchardcore.net/en/main/reference/modules/Localize/) | CMS module | PO only | Active | Not XLIFF; excluded from comparison [8] |

**Notes on MAT deprecation:** Microsoft confirmed MAT reached end-of-support on **October 15, 2025**.
No new installations are possible; the VS Marketplace listing is marked deprecated. [1]

**Notes on dotnet/xliff-tasks:** This package is distributed via the Microsoft Arcade toolset
feed, not the public NuGet.org gallery. It generates `.xlf` files in **XLIFF 1.2** format during
build and compiles them into satellite assemblies. It is primarily an internal Microsoft
infrastructure tool, not designed for round-trip translation workflows. [2]

**Notes on ResXResourceManager:** The most actively maintained open-source tool in this space.
It syncs `.resx` with `.xlf` files and updates both on edit. However, the XLIFF it generates is
**version 1.2** — confirmed by community issues and the XLIFF format produced. [3]

---

### 1b. NuGet Search Results (nuget.org `q=xliff`)

Searched [nuget.org/packages?q=xliff](https://www.nuget.org/packages?q=xliff). Key packages:

| Package | Author | Last Published | Downloads (approx) | XLIFF Ver |
|---|---|---|---|---|
| `Microsoft.DotNet.XliffTasks` | Microsoft | Via Arcade feed | Internal only | 1.2 |
| `Blackbird.Xliff.Utils` | Blackbird | 2024–2025 | Low (niche) | 1.2 |
| `Xliff.OM.NetStandard` | VladimirRybalko | Sep 2022 | Low | 2.0 |
| `fmdev.XliffParser` | fmuecke | May 2017 | Low | 1.2 |
| `Xliff.OM` | Microsoft (archived) | ~2017 | Low | 2.0 |
| `LocalizationProvider.Xliff` | valdis.iljuconoks | 2024 | Moderate | 1.2 |

No package on NuGet.org combines active maintenance, XLIFF 2.0 support, and a `.resx`
round-trip in a single artifact as of April 2026.

---

### 1c. GitHub Search (`xliff dotnet`, `resx-to-xliff`)

Repositories with last commit within 18 months (i.e., after October 2024):

| Repo | What It Does | XLIFF Ver | Maintained? |
|---|---|---|---|
| [dotnet/ResXResourceManager](https://github.com/dotnet/ResXResourceManager) | Full GUI + XLIFF sync | 1.2 | Yes (v1.104, Feb 2025) |
| [dotnet/xliff-tasks](https://github.com/dotnet/xliff-tasks) | MSBuild tasks | 1.2 | Yes (Microsoft internal) |
| [WeblateOrg/weblate](https://github.com/WeblateOrg/weblate) | Translation platform | 1.2 + 2.0 | Yes (v5.17.1, 2025) |

Repositories **not** maintained within 18 months (stale):

- [TheMATDude/ResourceToXliff](https://github.com/TheMATDude/ResourceToXliff) — RESX to XLIFF 1.2 for MAT; abandoned with MAT
- [StefanFabian/ResxToXliff](https://github.com/StefanFabian/ResxToXliff) — fork of above; last commit unknown, no recent activity
- [microsoft/XLIFF2-Object-Model](https://github.com/microsoft/XLIFF2-Object-Model) — declared to have "no clear MS owner"; effectively archived [5]

---

## 2. XLIFF 1.2 vs 2.0 Coverage

| Tool | XLIFF 1.2 | XLIFF 2.0 | Notes |
|---|---|---|---|
| MAT v4.1 | Yes | No | Deprecated Oct 2025 |
| dotnet/xliff-tasks | Yes | No | Internal; 1.2 `.xlf` format only |
| ResXResourceManager | Yes | No | Generates XLIFF 1.2 `.xlf` |
| fmdev.XliffParser | Yes | No | Abandoned 2017 |
| Xliff.OM / Xliff.OM.NetStandard | No | Yes | Object model only; no resx bridge; dormant |
| Blackbird.Xliff.Utils | Yes | No | Active but 1.2 only |
| Weblate 5.x | Yes | Yes (bilingual) | Both formats supported natively [9] |

**Key gap:** Every actively maintained .NET library targets XLIFF 1.2. XLIFF 2.0 libraries
(`Xliff.OM`, `Xliff.OM.NetStandard`) exist but are dormant, have no `.resx` integration, and
lack a published NuGet pipeline artifact. Weblate supports both, but the bridge from `.resx`
to XLIFF 2.0 has no off-the-shelf .NET implementation.

**XLIFF spec note:** XLIFF 2.2 became an OASIS Specification on March 13, 2025. XLIFF 2.1 was
approved as ISO 21720:2024 in July 2024. The 2.x family is not backward-compatible with 1.2. [10]

---

## 3. Build vs Adopt Recommendation

**Recommendation: BUILD**

### Rationale

No existing tool satisfies all three of Sunfish's requirements simultaneously:

1. `.resx <-> XLIFF` round-trip with idempotency (so translator edits survive re-generation)
2. **XLIFF 2.0** output (required by Weblate's preferred format for richer unit IDs and notes)
3. MSBuild integration (fits Sunfish's build pipeline without a separate CLI step)

The closest candidate, **dotnet/ResXResourceManager**, is actively maintained and produces XLIFF,
but outputs XLIFF 1.2 only, is a GUI tool rather than an MSBuild task, and has no published
package for automated CI use. Using it would require maintaining a separate manual export step
and a downstream XLIFF 1.2 → 2.0 conversion, neither of which has a maintained .NET library.

**MAT** is end-of-life. **dotnet/xliff-tasks** is internal Microsoft infrastructure, not
distributed on NuGet.org, and produces 1.2. The XLIFF 2.0 object model libraries are dormant.

A custom MSBuild task is the only path to a fully automated, XLIFF 2.0-native, CI-integrated
`.resx <-> XLIFF` round-trip. Effort is estimated at **~1 week** for an experienced .NET developer
given the straightforward XML-to-XML mapping and the `Xliff.OM.NetStandard` library as a
parsing foundation (or direct `System.Xml.Linq` serialization against the OASIS schema).

**Important caveat — Weblate XLIFF 2.0 mode is bilingual-only:** Weblate's XLIFF 2.0 support
(as of v5.17.1) is documented as bilingual only, meaning each file contains one source+target
language pair, matching how `.resx` satellite files are structured. This is compatible with the
proposed design below. [9]

---

## 4. BUILD: Design Sketch

### MSBuild Target: `SunfishExportXliff` / `SunfishImportXliff`

#### Directory layout

```
/localization/
  source/
    Resources.resx              ← authoritative source strings
  xliff/
    Resources.de.xlf            ← one bilingual XLIFF 2.0 file per locale
    Resources.ja.xlf
    Resources.ar.xlf
    ...                         ← 12 files total; checked into git
  generated/
    Resources.de.resx           ← imported back from approved Weblate translations
    Resources.ja.resx
    ...                         ← excluded from git; generated at build time
```

#### Target definitions (in a `.targets` file, shipped as a NuGet package)

```xml
<!-- Export: resx → xliff (run manually or in CI before pushing to Weblate) -->
<Target Name="SunfishExportXliff"
        Inputs="@(EmbeddedResource)"
        Outputs="localization/xliff/%(Filename).%(Culture).xlf">
  <SunfishResxToXliffTask
    SourceResx="%(EmbeddedResource.Identity)"
    OutputXliff="localization/xliff/%(Filename).%(Culture).xlf"
    XliffVersion="2.0"
    SourceLanguage="en-US"
    TargetLanguage="%(Culture)"
    PreserveExistingTargets="true" />   <!-- idempotency: never overwrite approved targets -->
</Target>

<!-- Import: xliff → resx (runs automatically before Compile) -->
<Target Name="SunfishImportXliff"
        BeforeTargets="BeforeBuild"
        Inputs="@(XliffFile)"
        Outputs="localization/generated/%(XliffFile.Filename).resx">
  <SunfishXliffToResxTask
    SourceXliff="%(XliffFile.Identity)"
    OutputResx="localization/generated/%(XliffFile.Filename).resx" />
</Target>
```

#### Idempotency guarantee

- **Export direction:** The task reads the existing `.xlf` file (if present), preserves any
  `<target>` elements whose `state="translated"` or `state="final"`, and only adds or updates
  `<unit>` elements whose `<source>` text has changed. Units not present in the `.resx` are
  marked `state="obsolete"` but retained for 90 days (configurable).
- **Import direction:** Always regenerates `.resx` from `.xlf`; output files are not checked in
  so there is no conflict risk. MSBuild incremental build (`Inputs`/`Outputs`) skips re-generation
  when `.xlf` files are unchanged.

#### Weblate integration

Weblate is configured with a Git repository remote pointing to Sunfish's `localization/xliff/`
directory. The workflow is:

1. Developer runs `dotnet build -t:SunfishExportXliff` locally or in CI on string freeze.
2. Changes to `.xlf` files are committed and pushed.
3. Weblate detects new/changed units and queues them for translation across all 12 locales.
4. Translators work in Weblate's UI; Weblate commits translated `.xlf` files back to the branch.
5. Normal `dotnet build` (no special target) triggers `SunfishImportXliff` automatically,
   producing locale-specific `.resx` files that compile into satellite assemblies.

#### XLIFF 2.0 namespace skeleton (for implementer reference)

```xml
<?xml version="1.0" encoding="utf-8"?>
<xliff xmlns="urn:oasis:names:tc:xliff:document:2.0"
       version="2.0"
       srcLang="en-US"
       trgLang="de-DE">
  <file id="f1" original="Resources.resx">
    <unit id="Greeting">
      <notes><note category="location">Resources.resx</note></notes>
      <segment state="initial">
        <source>Hello</source>
        <target></target>
      </segment>
    </unit>
  </file>
</xliff>
```

Unit `id` is the `.resx` key name, enabling lossless round-trip without heuristic matching.

#### Implementation dependencies

- `System.Xml.Linq` (in-box) for both RESX and XLIFF XML manipulation
- Optionally `Xliff.OM.NetStandard` (v2.0.1, Sep 2022) as a typed object model if the
  maintainer resumes it, or as a vendored dependency — though direct `XDocument` manipulation
  against the OASIS schema is straightforward enough to not require it
- `Microsoft.Build.Framework` + `Microsoft.Build.Utilities.Core` for the MSBuild task base class

#### Effort estimate

| Work item | Days |
|---|---|
| RESX parser (read `<data>` elements) | 0.5 |
| XLIFF 2.0 writer (export direction) with idempotency logic | 1.5 |
| XLIFF 2.0 reader + RESX writer (import direction) | 1.0 |
| MSBuild task wrappers + `.targets` file | 0.5 |
| Unit tests (round-trip, idempotency, RTL string pass-through) | 1.5 |
| NuGet packaging + CI integration | 0.5 |
| **Total** | **~5.5 days** |

---

## 5. Adopt: N/A

The ADOPT path is not recommended. See §3 for rationale.

If the team later revisits ADOPT, the nearest candidate is **ResXResourceManager** with a
manual export step plus a XLIFF 1.2 → 2.0 XSLT transform (the schema difference is
mechanical). This would trade automation for zero new code, but introduces a manual handoff
that breaks CI round-trips and requires maintaining a non-standard XSLT.

---

## References

1. [MAT Announcements — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/design/globalizing/mat-announcements) — deprecation and October 2025 EOL confirmed.
2. [dotnet/xliff-tasks — GitHub](https://github.com/dotnet/xliff-tasks) — MSBuild XLIFF 1.2 tasks; Arcade-distributed.
3. [dotnet/ResXResourceManager — GitHub](https://github.com/dotnet/ResXResourceManager) — last release v1.104, Feb 2025; XLIFF 1.2.
4. [fmdev.XliffParser — NuGet](https://www.nuget.org/packages/fmdev.XliffParser) — last publish May 2017.
5. [microsoft/XLIFF2-Object-Model — GitHub](https://github.com/microsoft/XLIFF2-Object-Model) — XLIFF 2.0 object model; no active owner.
6. [Xliff.OM.NetStandard — NuGet](https://www.nuget.org/packages/Xliff.OM.NetStandard) — community fork; last publish Sep 2022.
7. [Blackbird.Xliff.Utils — NuGet](https://www.nuget.org/packages/Blackbird.Xliff.Utils) — active; XLIFF 1.2; .NET 8+.
8. [OrchardCore Localization docs](https://docs.orchardcore.net/en/main/reference/modules/Localize/) — PO files only; no XLIFF support.
9. [Weblate XLIFF 2.0 format docs (v5.17.1)](https://docs.weblate.org/en/latest/formats/xliff2.html) — bilingual XLIFF 2.0 supported natively.
10. [XLIFF Wikipedia](https://en.wikipedia.org/wiki/XLIFF) — XLIFF 2.2 became OASIS Specification March 2025; XLIFF 2.1 = ISO 21720:2024.
11. [DevExpress MAT deprecation post (April 2026)](https://community.devexpress.com/Blogs/news/archive/2026/04/01/microsoft-multilingual-app-toolkit-mat-has-been-deprecated-what-s-next-for-devexpress-powered-app-localization.aspx) — confirms MAT deprecated in the broader community.
