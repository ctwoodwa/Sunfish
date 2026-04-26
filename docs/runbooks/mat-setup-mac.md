# Microsoft Multilingual App Toolkit (MAT) on Mac — Setup & Workflow

**Audience:** The Sunfish maintainer (or anyone) running localization work on macOS who wants the GUI translator workflow rather than the pure-CLI 3-stage AI gate.
**Goal:** Install MAT (or a viable Mac-friendly substitute), wire it to Azure Translator's free tier, and integrate it with the existing `tooling/Sunfish.Tooling.LocalizationXliff/` round-trip.
**Companion runbook:** [`i18n-translation-validation.md`](./i18n-translation-validation.md) — the 3-stage AI validation gate that should run on MAT's machine-translation output before any human review.

> **Reality check:** Microsoft's first-party Multilingual App Toolkit is a **Visual Studio (Windows)** extension. Visual Studio for Mac was retired by Microsoft on 2024-08-31 and the legacy MAT-for-VS-Mac extension is **not maintained**. This runbook documents the recommended path (Azure Translator + Sunfish's existing XLIFF tooling, with OmegaT or Weblate as the GUI editor) and lists the second-best fallbacks if you specifically need MAT-branded behavior.

---

## Why this workflow

MAT's value proposition decomposes into three pieces:

1. **Native `.resx` integration.** Sunfish already has this via `tooling/Sunfish.Tooling.LocalizationXliff/` (see `SunfishResxToXliffTask.cs` / `SunfishXliffToResxTask.cs` — they round-trip resx ↔ XLIFF 2.0).
2. **Azure Translator free tier (2M chars/month) for first-pass machine translation.** Available via the REST API; no Windows-VS dependency.
3. **Translator UI for per-key review + translation memory.** OmegaT or Weblate cover this on Mac. Both consume XLIFF natively.

So on Mac, "MAT workflow" really means: **Sunfish XLIFF round-trip + Azure Translator + OmegaT/Weblate as the editor**. That is what this runbook installs.

---

## Prerequisites

- **macOS Sequoia 15+** (Apple Silicon recommended).
- **Homebrew** (https://brew.sh).
- **.NET SDK** matching `global.json` at the repo root. See [`docs/runbooks/mac-claude-session-setup.md`](./mac-claude-session-setup.md) §"Required installs" if not yet installed.
- **VS Code** with the C# Dev Kit extension (replaces Visual Studio for Mac for `.resx` editing).
- **Azure account** (free tier is sufficient — credit card required at signup but no charges if you stay on F0).
- **Java 17+** (only if you choose OmegaT — it's a JVM app).

---

## Step 1 — Get an Azure Translator free-tier key

1. Sign in to https://portal.azure.com.
2. **Create a resource** → search "Translator" → choose **Translator** (publisher: Microsoft).
3. Configure:
    - **Resource group:** create `sunfish-i18n-rg` if you don't already have one.
    - **Region:** `Global` (or a near region like `eastus` / `westeurope` — pick by latency, not by data residency, because translation requests aren't stored).
    - **Name:** `sunfish-translator-free`.
    - **Pricing tier:** **F0** (Free — 2M characters/month). Critical: do not pick S1 by accident.
4. Click **Review + create** → **Create**.
5. After deployment finishes, go to the resource → **Keys and Endpoint**.
6. Copy `KEY 1` and the `Region` value.

Persist them as environment variables (add to `~/.zshrc` or `~/.bash_profile`):

```bash
export AZURE_TRANSLATOR_KEY="<paste KEY 1 here>"
export AZURE_TRANSLATOR_REGION="<paste region — e.g., eastus or global>"
export AZURE_TRANSLATOR_ENDPOINT="https://api.cognitive.microsofttranslator.com"
```

Reload (`source ~/.zshrc`) and verify with a smoke test:

```bash
curl -X POST "${AZURE_TRANSLATOR_ENDPOINT}/translate?api-version=3.0&to=de" \
  -H "Ocp-Apim-Subscription-Key: ${AZURE_TRANSLATOR_KEY}" \
  -H "Ocp-Apim-Subscription-Region: ${AZURE_TRANSLATOR_REGION}" \
  -H "Content-Type: application/json" \
  -d '[{"Text":"Save changes"}]'
# Expected: [{"translations":[{"text":"Änderungen speichern","to":"de"}]}]
```

---

## Step 2 — Install an XLIFF editor

Pick **one** of the two recommended editors:

### Option A — OmegaT (free, open-source, Mac-native)

```bash
brew install --cask omegat
```

Pros: Pure-XLIFF workflow, fast, integrated translation memory (TMX), runs offline.
Cons: GUI is utilitarian.

After install: launch OmegaT once; it will create `~/Documents/OmegaT/` for project files.

### Option B — Weblate (self-hosted, web UI)

For multi-translator collaboration. Run via Docker:

```bash
brew install --cask docker
docker run -d \
  --name weblate \
  -e WEBLATE_ADMIN_PASSWORD=changeme \
  -e WEBLATE_SERVER_EMAIL=admin@example.com \
  -p 8080:8080 \
  weblate/weblate:latest
```

Open http://localhost:8080 → admin/changeme → Add project → point at the Sunfish git repo URL → tell it the XLIFF path glob.

Pros: Per-key history, web UI for community translators, machine-translation provider plugins (including Azure Translator).
Cons: Heavier setup; only worth it once you have ≥2 active translators per locale.

### Option C — VS Code with Polyglot extension

Lightweight inline XLIFF editing without leaving the IDE:

```bash
code --install-extension polyglot.languagepacks
# (or use the marketplace UI to install "Polyglot Translator")
```

Pros: Zero context switch, lives next to the .NET project.
Cons: No translation-memory features; no built-in Azure Translator integration (you call Azure from a script and paste results).

**Recommendation:** Start with **OmegaT** for solo work; graduate to **Weblate** when you have a coordinator other than `@chriswood` per `i18n/coordinators.md`.

---

## Step 3 — Wire MAT-style flow into Sunfish XLIFF tooling

The existing `tooling/Sunfish.Tooling.LocalizationXliff/` produces and consumes XLIFF 2.0. Both editors above consume XLIFF 2.0 natively. Here is the loop.

### Outbound (resx → XLIFF for translation)

The `SunfishResxToXliffTask` MSBuild task runs as part of the build for any project that imports the `Sunfish.Tooling.LocalizationXliff` package. To run it ad-hoc on a single project:

```bash
cd packages/foundation
dotnet build -t:SunfishResxToXliff
# Produces: obj/loc/<ProjectName>.<locale>.xlf for each target locale
```

Copy the generated `.xlf` files into your editor of choice:

- **OmegaT:** `File → Open Project → New` → drop the `.xlf` files into `<project>/source/`.
- **Weblate:** they're picked up automatically from the git repo path you configured.
- **VS Code Polyglot:** open the `.xlf` directly.

### Machine-translation pre-fill via Azure Translator

Before doing manual review in the editor, fill the empty `<target>` elements via Azure Translator. The repo will gain a helper script for this; until then, here is the inline pattern:

```bash
# For one locale (e.g., de-DE):
LOCALE=de-DE
INPUT=obj/loc/Sunfish.Foundation.${LOCALE}.xlf
OUTPUT=obj/loc/Sunfish.Foundation.${LOCALE}.machine.xlf

# Extract sources, send to Azure, splice translations back.
# (See _shared/engineering/subagent-briefs/i18n-cascade-brief.md for the
# scripted version of this loop, which the cascade subagents already use.)
```

For a tight feedback loop while developing locally, OmegaT has built-in MT plugins — install the **Azure Cognitive Services Translator** plugin from `Tools → Preferences → Machine Translation`, paste your `AZURE_TRANSLATOR_KEY`, and it will populate suggestions inline as you walk through the file.

### Inbound (translated XLIFF → resx)

Once review is complete and entries are marked `state="final"` (or `state="translated"` for AI-generated entries pending coordinator sign-off — see `i18n/coordinators.md`), round-trip back:

```bash
cd packages/foundation
dotnet build -t:SunfishXliffToResx
# Updates: Resources/<Resource>.<locale>.resx with the new translations
```

---

## Step 4 — Translation memory location convention

Sunfish does not yet have a TM convention. **Proposed:** `i18n/translation-memory/`.

- `i18n/translation-memory/sunfish-master.tmx` — the master TMX, with all approved translation pairs across the project.
- `i18n/translation-memory/per-locale/<locale>.tmx` — per-locale slices (auto-generated from master).
- `i18n/translation-memory/glossary.tbx` — TermBase eXchange file with brand terms (Sunfish, Bridge, Anchor, Foundation, etc.) and how they should/shouldn't be translated. Most names should pass through untranslated; this file enforces that.

Editor wiring:

- **OmegaT:** `Project → Properties → Translation Memory` → point at `i18n/translation-memory/per-locale/<locale>.tmx`.
- **Weblate:** Project Settings → Components → set TM source paths.

The TMX files **should be committed to git**. Translation memory is reusable across cascades; losing it forces re-translation of strings the team has already approved.

---

## Step 5 — Verify the loop end-to-end

A 5-minute smoke test:

```bash
# 1. Round-trip a tiny resx out
cd packages/foundation
dotnet build -t:SunfishResxToXliff -p:LocXliffLocale=de-DE

# 2. Open in OmegaT, machine-translate one entry, mark it final.

# 3. Round-trip back
dotnet build -t:SunfishXliffToResx -p:LocXliffLocale=de-DE

# 4. Verify the .resx now has your German entry
git diff Resources/*.de-DE.resx
```

If the diff shows the new translation entry, the loop works.

---

## Step 6 — Hand-off to the AI validation gate

MAT-style machine translation is **only Stage 1 equivalent** of the [3-stage validation gate](./i18n-translation-validation.md). Before promoting any AI-generated translations to `state="final"`:

1. Run Stage 2 (back-translation) over the file.
2. Run Stage 3 (cross-check with a second engine — could be Claude if Azure was Stage 1, vice versa).
3. Generate the validation report.
4. Coordinator (per `i18n/coordinators.md`) reviews flagged keys.
5. Promote `translated` → `final`.

The MAT GUI is for the human-loop step (review + correction); the AI gate is for the auto-loop step (initial translation + drift detection). They are complementary, not alternatives.

---

## Fallbacks if MAT-on-Mac is the hard requirement

If for some reason the Microsoft-branded MAT extension itself is required (e.g., an external party expects MAT-format files):

| Option | Viability | Notes |
|---|---|---|
| **Visual Studio for Mac + legacy MAT** | Dead end. | VS for Mac retired 2024-08-31; Microsoft will not ship MAT updates for it. |
| **Parallels Desktop + Windows VM + Visual Studio + MAT** | Works. | ~$100/year for Parallels + free Windows dev VM from Microsoft + free Community VS + free MAT extension. Heavyweight but supported. |
| **Wine / CrossOver + Windows MAT installer** | Untested, likely broken. | MAT depends on Windows-only WPF assemblies for its UI. Not recommended. |
| **GitHub Actions runner on Windows for MAT-only steps** | Works for batch. | Run a workflow that spins up a Windows runner, executes the MAT CLI bits, commits the result. Heavy for interactive review but fine for headless batch translation. |
| **Direct Azure Translator + OmegaT** *(this runbook's primary path)* | Recommended. | All MAT functionality except the literal MAT-branded GUI. |

---

## Operational checklist (first-time setup)

- [ ] Azure Translator F0 resource created in `sunfish-i18n-rg`.
- [ ] `AZURE_TRANSLATOR_KEY`, `AZURE_TRANSLATOR_REGION`, `AZURE_TRANSLATOR_ENDPOINT` in shell profile.
- [ ] Smoke test (the curl command above) returns a German translation.
- [ ] OmegaT (or Weblate) installed.
- [ ] OmegaT Azure Translator plugin configured with the key.
- [ ] `i18n/translation-memory/` directory created and added to git.
- [ ] `Sunfish.Tooling.LocalizationXliff` round-trip verified for one locale.
- [ ] Coordinator (per `i18n/coordinators.md`) is aware they own sign-off on this locale.

---

## See also

- [`docs/runbooks/i18n-translation-validation.md`](./i18n-translation-validation.md) — the 3-stage AI validation gate (run after MAT pre-fill).
- [`docs/runbooks/mac-claude-session-setup.md`](./mac-claude-session-setup.md) — base macOS environment setup.
- [`_shared/engineering/subagent-briefs/i18n-cascade-brief.md`](../../_shared/engineering/subagent-briefs/i18n-cascade-brief.md) — automated subagent brief that uses this same Azure key.
- [`tooling/Sunfish.Tooling.LocalizationXliff/`](../../tooling/Sunfish.Tooling.LocalizationXliff/) — the resx ↔ XLIFF 2.0 MSBuild tasks.
- [`i18n/coordinators.md`](../../i18n/coordinators.md) — locale ownership.
- [`i18n/locales.json`](../../i18n/locales.json) — locale metadata.
