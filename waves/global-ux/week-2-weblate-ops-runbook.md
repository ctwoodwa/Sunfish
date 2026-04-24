# Week 2 Weblate Ops Runbook

**Date:** 2026-04-24 (Plan 2 Task 2.3 deliverable).
**Purpose:** Operator-facing runbook for Sunfish's self-hosted Weblate 5.17.1 stack.
Complements `infra/weblate/README.md` (day-to-day reference) with procedure-level content.

---

## Bring-up acceptance criteria

Phase 1 Week 2 acceptance for the Weblate stack is met when ALL of:

- ☐ Stack is up on the target VM with all three services (weblate, postgres, valkey) `healthy` for ≥ 24 hours.
- ☐ Admin account created; initial password rotated; **`WEBLATE_ADMIN_PASSWORD` in `.env` has been replaced with a placeholder** (rotating doesn't scrub the file).
- ☐ Reverse-proxy (Caddy / nginx) terminates HTTPS at `translate.sunfish.example` (or chosen domain); port 8080 NOT exposed publicly.
- ☐ First component configured: `sunfish-core` watching `localization/xliff/*.xlf` from `main`.
- ☐ Webhook from the Sunfish repo confirmed working: a test push reflects in Weblate logs within 60 s.
- ☐ MADLAD-400 MT backend returns suggestions for at least 3 of the 12 target locales at p95 < 10 s (see `infra/weblate/mt-backends.md`).
- ☐ First backup run succeeds; restore-from-backup drill completes without data loss.
- ☐ Uptime monitor on `/healthz/` hooked into the Sunfish on-call paging channel.

---

## Standard operating cadence

| Cadence | Activity | Who | Runbook step |
|---|---|---|---|
| Daily 04:00 | Automated backup (pg_dump + tar + BorgBackup → S3) | cron | `ops/backup.sh` |
| Weekly Mon | Review Weblate Django log for errors + CVE patches | on-call | `docker compose logs weblate --since 168h | grep -i "error\|warn"` |
| Monthly | **DR drill: restore from yesterday's backup to a fresh VM** | ops lead | §DR Drill below |
| Monthly | MT-backend latency re-measurement (Plan 3 Task 2.5) | ops lead | `infra/weblate/mt-backends.md` §Validation gate |
| Monthly | Weblate minor-version upgrade (~5.18, 5.19, etc.) | ops lead | `infra/weblate/README.md` §Upgrade |
| Quarterly | Postgres + Valkey password rotation | ops lead | §Secret rotation |
| Quarterly | Review translator roster; revoke inactive accounts | translator coordinator | Weblate admin UI |
| Annually | Major-version upgrade review + full DR drill | ops lead + BDFL | Custom playbook per version |

---

## DR Drill (monthly)

The point of a DR drill is not to exercise ops muscle — it's to catch backup corruption
before a real incident does. Do it fully, on a different VM than production.

1. **Provision a throwaway VM** with the same sizing as production (4 GB RAM, 2 vCPU, 40 GB SSD).
2. **Sync the most recent nightly backup** from S3 to the VM.
3. **Restore in this order:**
   1. `data/postgres/` from tar.
   2. `pg_restore` the pg_dump into the fresh Postgres instance.
   3. `data/weblate/` from tar.
   4. `data/valkey/` can be empty — Valkey is cache-only; no persistence needed.
4. **Start the stack:** `docker compose up -d`.
5. **Verify:**
   - Log in as admin.
   - Browse to `sunfish-core` component; confirm translations are present.
   - Check the *History* tab on at least 3 entries — drift from production logs means the backup lagged or was corrupted.
6. **Drill pass criteria:** all 12 locale XLIFFs present; glossary populated; at least one entry with state=`final` visible; total restore time ≤ 30 minutes.
7. **Drill fail criteria:** any data missing, or restore time > 1 hour → file an ops incident, don't ship to production until the backup chain is fixed.
8. **Tear down the throwaway VM.** Record the drill outcome in `infra/weblate/ops/dr-drill-log.md` (create if missing).

---

## Incident response

Common incidents in rough order of frequency:

### Weblate returns 5xx

1. `docker compose ps` — which service is unhealthy?
2. If `weblate`: `docker compose logs weblate --tail 200`. Most common: Postgres connection exhausted (see below) or secret key mismatch after a rotation.
3. If `postgres`: `docker compose exec postgres pg_isready`. Restart with `docker compose restart postgres`; worst case, restore from backup.
4. If `valkey`: `docker compose restart valkey`. Sessions will clear; translators re-log in. Low severity.

### Postgres connection exhaustion

Weblate's default uses a lot of connections at translator peak. Signs: `"too many connections"` in logs.

1. Raise Postgres's `max_connections`: add `POSTGRES_MAX_CONNECTIONS=200` to `.env`, `docker compose up -d`.
2. If still hitting the ceiling at peak: add a connection pooler (PgBouncer sidecar). Non-trivial; coordinate with ops lead.

### Sunfish git push not triggering Weblate re-import

1. In Weblate admin, *Sunfish → Repository maintenance → Update*. If that works, the webhook secret is wrong.
2. Rotate the webhook secret in both the Sunfish repo webhook config and the Weblate admin UI. Test with a trivial commit.

### MT-backend (llama.cpp) unreachable

1. SSH to the Weblate VM: `systemctl status llamacpp-madlad`.
2. If stopped: `systemctl start llamacpp-madlad`.
3. Translators can still work — MADLAD suggestions are optional, not required. Suggestions will resume automatically once the backend is back.

### Translator can't log in

1. Verify the translator has been invited: Weblate admin → Users.
2. If invitation email never arrived, check the Weblate SMTP config: `infra/weblate/.env` — `WEBLATE_EMAIL_*` variables.
3. Worst case: admin can set a temporary password directly in Weblate admin UI; translator resets on first login.

---

## Secret rotation

Rotation order matters — rotate in-place with zero translator-visible downtime:

### Postgres password rotation

1. `docker compose exec postgres psql -U weblate -c "ALTER USER weblate PASSWORD 'NEW';"`
2. Update `POSTGRES_PASSWORD` in `.env`.
3. `docker compose up -d weblate`  (restarts only the weblate service with the new env).
4. Verify translators can still log in.

### Valkey password rotation

1. Update `REDIS_PASSWORD` in `.env`.
2. `docker compose up -d valkey weblate`  (both restart).
3. Sessions cleared; translators re-log in. Low-cost; do during low-traffic window.

### Weblate secret key

**⚠️ Rotating `WEBLATE_SECRET_KEY` invalidates all existing sessions, invites, and password-reset tokens.** Coordinate with the translator roster before rotating.

1. Announce a 15-minute downtime window.
2. Generate a new secret; update `WEBLATE_SECRET_KEY` in `.env`.
3. `docker compose up -d weblate`.
4. Notify translators; anyone with an in-flight session must re-log in.

### Admin password

Through the Weblate UI: *My settings → Security → Password*. No restart required.

---

## Cost tracking

Baseline operating cost per spec §3B Task 3 memo (Weblate vs Crowdin, `icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md`):

| Line item | Monthly | Notes |
|---|---|---|
| VM (4 GB, 2 vCPU, 40 GB SSD) | ~$12-18 | Hetzner CX22 or DigitalOcean Basic |
| Managed Postgres (optional) | ~$15 | Skip if self-managed Postgres on the same VM |
| Object storage for backups | ~$2-5 | Backblaze B2 or S3 IA tier |
| Bandwidth | ~$0 | 1 TB/month egress is free on most hosts |
| **Total** | **~$30-40** | Compared to Crowdin Business at $175-450+ |

When MADLAD-400 moves to a GPU host (if latency demands): +$30-60/month for a GPU-VM.

Review monthly; update the Plan 2 Success Criteria table if actuals drift > 2× budget.

---

## Escalation matrix

| Issue | First responder | Escalate to |
|---|---|---|
| Stack down > 1 hour | on-call | ops lead |
| Data loss in backup chain | ops lead | BDFL + legal (if translator work is lost) |
| AGPL §13 legal question surfaces | — | legal counsel (per decisions.md entry 2026-04-25) |
| Translator complaint about MT quality | translator coordinator | ops lead + MADLAD-400 author-of-record |
| CVE in Weblate upstream | on-call | ops lead; patch within 48 h per spec §8 security policy |

---

## Cross-references

- [`infra/weblate/README.md`](../../infra/weblate/README.md) — day-to-day ops reference
- [`infra/weblate/docker-compose.yml`](../../infra/weblate/docker-compose.yml) — stack definition
- [`infra/weblate/mt-backends.md`](../../infra/weblate/mt-backends.md) — MADLAD + DeepL wiring
- [Plan 2](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md) Workstream B — source plan
- [Plan 3](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md) — translator-assist MT integration depends on MADLAD backend
- [Weblate vs Crowdin memo](../../icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md) — tool-choice rationale + AGPL §13 caveat
