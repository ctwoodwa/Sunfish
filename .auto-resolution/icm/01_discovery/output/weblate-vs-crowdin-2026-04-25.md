# Weblate (Self-Hosted) vs. Crowdin (SaaS) — Translator Platform Triage

**Date:** 2026-04-25
**Author:** Research assistant (AI)
**Status:** Draft — pending author review and legal sign-off on §1
**Blocking:** Phase 1 Day 1 implementation of the Sunfish i18n / 12-locale workflow
**Default pick per spec:** Weblate self-hosted

---

## 1. AGPL Network-Service Obligations

### Exact AGPL §13 text

> "Notwithstanding any other provision of this License, if you modify the Program, your modified
> version must prominently offer all users interacting with it remotely through a computer network
> (if your version supports such interaction) an opportunity to receive the Corresponding Source of
> your version by providing access to the Corresponding Source from a network server at no charge,
> through some standard or customary means of facilitating copying of software."
>
> — GNU Affero General Public License v3, §13 "Remote Network Interaction; Use with the GNU General
> Public License" ([gnu.org](https://www.gnu.org/licenses/agpl-3.0.en.html))

### Analysis

The AGPL §13 trigger has two preconditions: (a) Sunfish **modifies** Weblate, and (b) modified
Weblate is **accessed over a network** by third parties. Both conditions are realistic if Sunfish
operates a managed translation service.

| Scenario | §13 triggered? | Required action |
|---|---|---|
| Weblate deployed internally, unmodified, no external user access | No | None |
| Weblate deployed internally, modified, no external user access | No — distribution not triggered | None (but track modifications) |
| Weblate run as a service (managed hosting) and **not modified** | No | Must provide unmodified AGPL source (trivially satisfied by pointing to upstream) |
| Weblate run as a service (managed hosting) and **modified** | **Yes** | Must offer Corresponding Source (all modifications) via a network server at no charge |

Sunfish's `vision.md` frames managed hosting as an optional future revenue stream, not a current
commitment. However, any workflow automation, branding changes, or custom MT backends integrated
*inside* the Weblate codebase (rather than as separate microservices) constitute modifications.

**Verdict: Open legal question requiring counsel review.**

The obligation is contingent on (1) whether Sunfish commercializes a hosted translation service and
(2) whether changes are made inside Weblate's codebase vs. outside it. If Sunfish does modify and
expose Weblate externally, the source-offer requirement is real and specific: all modified source
must be published or offered on request. Weblate's commercial entity also sells a proprietary
relicensing option that removes AGPL obligations for enterprise customers — that path is available
if counsel advises against AGPL exposure.

Sources: [GNU AGPL v3](https://www.gnu.org/licenses/agpl-3.0.en.html) · [FOSSA AGPL guide](https://fossa.com/blog/open-source-software-licenses-101-agpl-license/) · [Open Core Ventures AGPL analysis](https://www.opencoreventures.com/blog/agpl-license-is-a-non-starter-for-most-companies)

---

## 2. Translator UX

| Feature | Weblate 5.17.1 | Crowdin |
|---|---|---|
| **XLIFF 2.0 native support** | Yes — added as a first-class bilingual format ([docs](https://docs.weblate.org/en/latest/formats/xliff2.html)) | Yes — native, no plugin required ([store.crowdin.com/xliff2.0](https://store.crowdin.com/xliff2.0)) |
| **Glossary integration** | Built-in glossary; source-language matches shown in editor ([weblate.org/features](https://weblate.org/en/features/)) | Built-in; integrates with Translation Memory and AI suggestions |
| **Workflow maturity** | Translation + review/approval stages; `approved` XLIFF state maps directly to Weblate "Approved" | Unlimited sequential or parallel workflow steps; task assignment, proofreading stage, per-language assignment ([Crowdin workflow docs](https://support.crowdin.com/enterprise/workflows/)) |
| **Review-stage support** | Recommended to enable review process when using XLIFF; strings flagged Waiting for Review until approved ([docs](https://docs.weblate.org/en/latest/formats/xliff2.html)) | Dedicated Proofreading step; translators and proofreaders assigned separately |
| **Mobile translator UX** | Responsive web UI; no dedicated mobile app | No native mobile app; responsive web editor described as adequate for occasional mobile use |

**Assessment:** Crowdin's workflow engine is more configurable out of the box (unlimited steps,
parallel/sequential branching). Weblate's is simpler but sufficient for a 12-locale + volunteer
model. XLIFF 2.0 parity is now equal — Weblate closed the gap in late 2025. Neither platform has a
native mobile app; mobile experience is not a differentiator.

---

## 3. MADLAD-400-3B-MT Integration (llama.cpp GGUF local inference)

The integration path is different for each platform.

### Weblate

Weblate's machine-translation backend system is plugin-based and extensible. Out of the box it
ships backends for LibreTranslate, DeepL, OpenAI (with custom base URL since v5.7), Anthropic
Claude (since v5.16), and ~15 others. The **OpenAI-compatible backend** is the practical bridge:
llama.cpp's server mode exposes an OpenAI-compatible `/v1/chat/completions` endpoint. Configure
Weblate's OpenAI backend to point at `http://localhost:8080` with a dummy key, and MADLAD-400
suggestions appear in the editor.

Configuration in `settings.py`:
```python
MT_SERVICES = ["weblate.machinery.openai.OpenAITranslation"]
MT_OPENAI_KEY = "local"
MT_OPENAI_BASE_URL = "http://localhost:8080/v1"
MT_OPENAI_MODEL = "madlad400-3b-mt"
```

This is a documented pattern; no forking of Weblate is required.

Sources: [Weblate automatic suggestions docs](https://docs.weblate.org/en/latest/admin/machine.html) · [Weblate 5.7 OpenAI custom base URL](https://docs.weblate.org/en/latest/admin/machine.html)

### Crowdin

Crowdin's **Custom MT Module** allows any MT engine not natively supported to be wired in via a
Crowdin App (a small HTTP microservice Crowdin calls). The app receives source strings and returns
translations over HTTPS — the MT engine does not need to be co-located with Crowdin. For a local
GGUF model this means Sunfish must run a small proxy service that accepts Crowdin's MT callback and
forwards to llama.cpp.

Sources: [Crowdin Custom Machine Translation App](https://support.crowdin.com/enterprise/custom-machine-translation-app/) · [Crowdin AI docs](https://support.crowdin.com/crowdin-ai/)

### Comparison

| Criterion | Weblate | Crowdin |
|---|---|---|
| Integration path for local llama.cpp | OpenAI-compatible backend — zero custom code, point at localhost | Custom MT Module — requires a small proxy microservice |
| Network exposure of local model | None (Weblate and llama.cpp co-located) | Crowdin cloud must reach Sunfish's proxy over HTTPS — exposes local model endpoint |
| Maintenance burden | Minimal — backend URL config only | Proxy must be kept alive and publicly accessible |
| Control over inference | Full | Full (proxy mediates) |

**Weblate wins this dimension.** The OpenAI-compatible endpoint makes wiring MADLAD-400 trivially
local with no network exposure and no additional service to maintain.

---

## 4. Cost

### Weblate Self-Hosted

Weblate requires Docker (Compose stack: Weblate app, PostgreSQL, Valkey/Redis cache).

| Resource | Requirement | Est. monthly cost (cloud VM) |
|---|---|---|
| RAM | ≥ 4 GB recommended for hundreds of components ([Docker install docs](https://docs.weblate.org/en/latest/admin/install/docker.html)) | — |
| CPU | 2–4 vCPU for moderate concurrency | — |
| Storage (DB) | ~300 MB per 1M hosted words ([Docker docs](https://docs.weblate.org/en/latest/admin/install/docker.html)) | — |
| VM (4 GB RAM, 2 vCPU, 40 GB SSD) | e.g., Hetzner CX22 or DigitalOcean Basic | ~$12–18/month |
| Managed Postgres (optional upgrade) | e.g., DigitalOcean managed DB | ~$15/month |
| Backups / object storage | BorgBackup to S3-compatible | ~$2–5/month |
| **Total running cost** | | **~$30–40/month** |
| Ops labor | Upgrades, monitoring, DR drills | 2–4 hrs/month |

Weblate self-hosted has no per-seat or per-word charges. 12 coordinators + 30 volunteers = 42
users at $0 licensing cost.

### Crowdin SaaS

Crowdin does not charge per translator seat — translators and proofreaders are unlimited on any
paid plan. Pricing scales by **hosted word count** and number of **manager seats**.

| Plan | Price (billed annually, approx.) | Hosted words | Managers |
|---|---|---|---|
| Team | ~$50/month | 50,000 | 1 |
| Business | Custom (starts ~$175–450/month) | Higher tiers | Multiple |
| Enterprise | Custom negotiated | Unlimited | Unlimited |

For 12 locale coordinators acting as managers, the Team plan's single-manager limit is
insufficient. Business or Enterprise pricing applies — expect $175–450+/month depending on word
volume and manager count. No infrastructure to operate.

Sources: [Crowdin Pricing](https://crowdin.com/pricing) · [AutoLocalise Crowdin pricing breakdown](https://www.autolocalise.com/blog/crowdin-pricing-breakdown-alternatives) · [Vendr Crowdin](https://www.vendr.com/marketplace/crowdin)

### Cost Summary

| | Weblate Self-Hosted | Crowdin SaaS |
|---|---|---|
| 42-user licensing | $0 | $0 (unlimited translators) |
| Infrastructure | ~$30–40/month | $0 |
| Management seats (12) | $0 | Requires Business/Enterprise tier ($175–450+/month) |
| **Estimated monthly total** | **~$30–40** | **~$175–450+** |

---

## 5. Operational Burden

| Dimension | Weblate Self-Hosted | Crowdin SaaS |
|---|---|---|
| **Who runs the instance** | Sunfish engineering | Crowdin SRE |
| **Upgrade cadence** | Frequent — Weblate releases ~monthly; Docker Compose pull + `upgrade` command; full DB backup required before each upgrade ([upgrade docs](https://docs.weblate.org/en/weblate-5.1.1/admin/upgrade.html)) | Zero — handled by Crowdin |
| **Backup strategy** | BorgBackup (built-in Celery task); RPO 24h, RTO 8h per Weblate's own DR plan ([DR plan](https://docs.weblate.org/en/latest/security/disaster-recovery-plan.html)); monthly restore tests recommended | Crowdin manages — SLA-backed |
| **Disaster recovery** | Sunfish owns DR plan: monthly backup restore tests, annual full DR drill | Crowdin SLA |
| **Security patching** | Sunfish tracks CVEs and applies Docker image updates | Crowdin's responsibility |
| **Monitoring** | Self-managed (Prometheus/Grafana or uptime checks) | Crowdin dashboard + status page |

**Verdict:** Weblate self-hosted adds real ops load — estimate 2–4 hours/month steady-state for an
experienced operator, spiking to 4–8 hours during major version upgrades. At current team size
(pre-LLC, AI-assisted development focus), this is non-trivial but manageable. If the team does not
have a designated infra owner, Crowdin removes this burden entirely.

---

## Recommendation

**Default pick confirmed: Weblate self-hosted.**

The research supports the spec's default. Weblate wins on cost ($30–40/month vs. $175–450+/month),
MADLAD integration (zero-code OpenAI-compatible backend vs. proxy microservice), and data
sovereignty (translation memory stays on-prem). XLIFF 2.0 and workflow maturity are now at feature
parity with Crowdin for Sunfish's use case.

**Named fallback: Crowdin Business tier.**

If Sunfish does not have an infra owner at Phase 1 kick-off, or if legal counsel advises avoiding
AGPL exposure entirely, Crowdin Business removes all operational burden and eliminates the AGPL
question. The cost premium ($175–450+/month) is acceptable for a well-funded team; it is
significant for a pre-LLC project funded from discretionary budget.

### AGPL verdict

**Open legal question — requires counsel review before any external hosting of Weblate.**

The §13 obligation is real but conditional: it activates only if Sunfish (a) modifies Weblate
internals and (b) exposes it over a network to third parties. Internal use for Sunfish's own
translation workflow carries no obligation. A managed translation hosting product would. Counsel
should advise before that decision is made; Weblate's commercial relicensing is an available
escape hatch if needed.

---

## References

1. GNU AGPL v3 full text: https://www.gnu.org/licenses/agpl-3.0.en.html
2. Weblate XLIFF 2.0 format docs: https://docs.weblate.org/en/latest/formats/xliff2.html
3. Weblate automatic suggestions (MT backends): https://docs.weblate.org/en/latest/admin/machine.html
4. Weblate Docker install + RAM requirements: https://docs.weblate.org/en/latest/admin/install/docker.html
5. Weblate disaster recovery plan: https://docs.weblate.org/en/latest/security/disaster-recovery-plan.html
6. Weblate upgrade guide: https://docs.weblate.org/en/weblate-5.1.1/admin/upgrade.html
7. Crowdin XLIFF 2.0 support: https://store.crowdin.com/xliff2.0
8. Crowdin workflow docs: https://support.crowdin.com/enterprise/workflows/
9. Crowdin Custom MT Module: https://support.crowdin.com/enterprise/custom-machine-translation-app/
10. Crowdin AI docs: https://support.crowdin.com/crowdin-ai/
11. Crowdin pricing: https://crowdin.com/pricing
12. AutoLocalise Crowdin pricing breakdown: https://www.autolocalise.com/blog/crowdin-pricing-breakdown-alternatives
13. FOSSA AGPL license guide: https://fossa.com/blog/open-source-software-licenses-101-agpl-license/
14. Weblate features overview: https://weblate.org/en/features/
