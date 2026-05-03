# Sunfish Weblate Self-Hosted

Weblate 5.17.1 is the translator platform Sunfish runs for the Global-First UX initiative
(see [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../../docs/superpowers/specs/2026-04-24-global-first-ux-design.md) §3B).
The translator editor + glossary + XLIFF 2.0 round-trip with the Sunfish repo's
`localization/xliff/` directory all run from this stack.

This README is the operator-facing reference. The planning-side ops runbook lives at
[`waves/global-ux/week-2-weblate-ops-runbook.md`](../../waves/global-ux/week-2-weblate-ops-runbook.md).

## Initial bring-up

1. Provision a VM sized per Plan 2 Task 2.2 (baseline 4 GB RAM, 2 vCPU, 40 GB SSD).
2. Install Docker Engine + `docker compose` plugin. Verify `docker --version` ≥ 24.
3. Clone this repo onto the VM; `cd` to `infra/weblate`.
4. Copy `.env.example` → `.env` and fill every variable. Generate the secret keys; never reuse.
5. `mkdir -p data/weblate data/postgres data/valkey`
6. `docker compose up -d`
7. Watch the healthcheck: `docker compose ps` — all three services should reach `healthy` within 2-3 minutes. First run provisions the Postgres schema (~90 seconds).
8. Browse to `http://<vm-ip>:8080` (or your reverse-proxy hostname). Log in with the admin credentials from `.env`. **Rotate the password immediately.**

## Everyday operations

### Log into a running container

```bash
docker compose exec weblate weblate shell  # Django shell
docker compose logs -f weblate             # Tail the app log
docker compose logs -f postgres            # Tail DB
```

### Upgrade to a patch release

Weblate releases roughly monthly. Keep within the 5.x line; major-version bumps may
need config migration.

```bash
# On the VM:
cd infra/weblate
git pull                                    # Get the new docker-compose.yml pin
docker compose pull                         # Pull the new images
docker compose exec weblate weblate makemessages --force  # optional: refresh locale files
docker compose down
docker compose up -d
```

Expected downtime on upgrade: 30-90 s for the web app; Postgres and Valkey don't restart unless their image tags change.

### Daily backup

`infra/weblate/ops/backup.sh` runs in cron at 04:00 local. Backs up:

1. `pg_dump` of the Postgres database → S3-compatible blob store.
2. `tar` of `data/weblate/` (Weblate's settings + component cache) → same store.
3. BorgBackup repository of the whole `data/` dir with 14-day retention.

Restore tests monthly; see [`waves/global-ux/week-2-weblate-ops-runbook.md`](../../waves/global-ux/week-2-weblate-ops-runbook.md) §DR Drill.

### Add a new repository component (Sunfish repo integration)

Weblate watches `localization/xliff/*.xlf` in the Sunfish repo. First-time component setup:

1. Log in as admin.
2. *Projects → Sunfish → Add new translation component*.
3. **Name:** `sunfish-core` (one component per top-level package; more components can be added later).
4. **Source code repository:** `https://github.com/<org>/sunfish.git` (SSH preferred once Sunfish's deploy keys are set).
5. **Repository branch:** `main`.
6. **File format:** *XLIFF translation file*.
7. **File mask:** `localization/xliff/*.xlf`.
8. **Monolingual base language file:** leave blank (Sunfish uses bilingual XLIFF 2.0; one file per locale pair).
9. **Source language:** `en-US`.
10. Save; Weblate clones the repo and parses existing XLIFFs. Takes ~30 s for a fresh clone.

### Add the Sunfish git webhook

To trigger Weblate re-imports on Sunfish push:

1. In the Weblate admin: *Sunfish project settings → "Webhook secret"* — generate or reuse.
2. In the Sunfish repo: `gh api -X POST repos/<org>/sunfish/hooks --input hooks/weblate-webhook.json` (committed; substitutes the secret from env).
3. Test: push a trivial XLIFF change; Weblate log should show *"Webhook received, pulling repository"* within 60 s.

## Monitoring

Weblate exposes `/healthz/` (200 when healthy); `docker compose ps` checks it every 30 s.
External monitoring should add:
- HTTP uptime ping on `/healthz/` every 60 s, alert after 3 consecutive failures.
- Postgres disk-space check (alert < 20% free).
- Valkey memory-usage check (alert > 80% of 256 MB max).
- MADLAD MT-backend latency metric; p95 > 10 s triggers review (see `mt-backends.md`).

Metrics are exposed at `/metrics/` (Prometheus format) once `prometheus_client` is enabled
via `WEBLATE_ENABLE_METRICS=1`; not enabled by default in the .env.example because the
endpoint is unauthenticated and needs a firewall or reverse-proxy auth shim before opening.

## Security posture

- **AGPL §13 note:** running Weblate self-hosted for Sunfish's own translator workflow
  does NOT trigger AGPL §13 obligations (internal use). Exposing Weblate to third parties
  as a managed product WOULD trigger §13 — see the
  [Weblate vs Crowdin memo](../../icm/01_discovery/output/weblate-vs-crowdin-2026-04-25.md)
  §1 and the [AGPL legal caveat](../../waves/global-ux/decisions.md) entry.
- **Secret rotation:** rotate `WEBLATE_SECRET_KEY` + admin password after first bring-up and
  at every release cut. Postgres + Valkey passwords live with the deploy; rotate quarterly.
- **Backups are encrypted:** BorgBackup uses repokey-blake2 passphrases; the passphrase lives
  in the VM's secret-management side (not in this repo).
- **Reverse proxy mandatory for production:** Weblate behind HTTPS (Caddy or nginx) for
  public access; never expose port 8080 directly.

## Troubleshooting

- *"Session not found" after login:* Valkey connection died; `docker compose restart valkey`.
- *Webhook not triggering pull:* check Weblate's Django log for `"Webhook received"` messages;
  if nothing, verify the GitHub secret matches Weblate's expected secret in the Webhooks admin panel.
- *"Database is locked":* Postgres connection saturation; increase `POSTGRES_MAX_CONNECTIONS`
  (raise from default 100 to 200) and restart Postgres.
- *MT backend timeouts:* llama.cpp server either slow or not running on the host; confirm
  `curl http://127.0.0.1:8080/v1/models` from inside the weblate container works. See
  `mt-backends.md` for the wiring.
