# Operations and SRE

**Status:** Posture for pre-release
**Last reviewed:** 2026-04-20
**Governs:** Observability stack, SLI/SLO discipline, incident response, post-mortems, and runbooks — once Bridge or any Sunfish accelerator hosts a production tenant. Until then this document is posture-only.
**Companion docs:** [ci-quality-gates.md](ci-quality-gates.md), [supply-chain-security.md](supply-chain-security.md), [data-privacy.md](data-privacy.md), [ai-code-policy.md](ai-code-policy.md), [../product/vision.md](../product/vision.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [ADR 0006 — Bridge is a SaaS shell](../../docs/adrs/0006-bridge-is-saas-shell.md).
**Agent relevance:** Low-frequency. Loaded only once a production tenant exists or observability code is touched. Skip for pre-release work.

## Posture summary

Sunfish is pre-release and pre-community. **There is no production tenant to operate today.** SRE practice — Google's [SRE book](https://sre.google/books/) is the canonical reference — exists here as a posture that activates on well-defined triggers, not a program the BDFL performs against empty traffic. The observability stack is already wired in code; the service-level discipline that gives it meaning (published SLOs, error budgets, on-call, incident commander rotation, blameless post-mortems, tested runbooks) activates when the first production tenant signals, tracked in §Activation triggers below.

This document prescribes the **shape** that activates, so that when the trigger fires there is no scramble to invent a framework — the framework is already written, and the only work is binding numbers and names to it.

## Current state

Already wired, running in dev:

| Capability | Implementation | Status |
|---|---|---|
| Distributed tracing instrumentation | `OpenTelemetry.Instrumentation.AspNetCore` 1.15.1, `OpenTelemetry.Instrumentation.Http` 1.15.0 in [`Directory.Packages.props`](../../Directory.Packages.props) | Wired in Bridge |
| Runtime metrics | `OpenTelemetry.Instrumentation.Runtime` 1.15.0 | Wired |
| OTLP export | `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.2 | Wired, points at Aspire dashboard by default |
| Hosting integration | `OpenTelemetry.Extensions.Hosting` 1.15.2 | Wired in `Sunfish.Bridge.ServiceDefaults` |
| Structured logging | `Serilog.AspNetCore` 10.0.0 with Console / File / OpenTelemetry sinks | Wired in Bridge |
| Dev-time dashboard | .NET Aspire dashboard (via `Sunfish.Bridge.AppHost`) | Local debug only |

Not yet wired and deliberately deferred:

- No production metrics exporter configured (no Prometheus, no Azure Monitor, no CloudWatch — those are deployer choices bound at deployment time).
- No uptime monitoring — nothing to monitor yet.
- No status page, no synthetic probes, no paging integration.
- No on-call rotation — single maintainer per the BDFL model in [`GOVERNANCE.md`](../../GOVERNANCE.md).
- No published SLIs or SLOs. Making numbers up before there is traffic to measure is worse than having none.

The Aspire dashboard is **explicitly a dev-time tool** (per [Microsoft's own documentation](https://learn.microsoft.com/dotnet/core/diagnostics/observability-otlp-example)). Production observability is a separate deployment-time choice; §Tooling path below lists the options.

## Observability pillars

The four pillars, each with a defined home in the stack:

**Metrics** — OpenTelemetry `Meter` API. Three tiers:

- **System metrics** — ASP.NET Core request rate / latency / error rate, HttpClient client-call telemetry, .NET runtime GC / allocations / thread-pool saturation. All automatic via the instrumentation packages above.
- **Bundle business metrics** — one `Meter` per bundle, named per [ADR 0007 — bundle manifest schema](../../docs/adrs/0007-bundle-manifest-schema.md). Counters for domain events (e.g., `sunfish.bundle.property_management.leases_created`), histograms for durations, gauges for steady-state counts. Defined in the bundle, emitted by handlers.
- **Shell metrics** — Bridge-level counts for tenant signups, bundle activations, feature-flag evaluations, subscription edition transitions. These are the first SLI inputs once production lands.

**Traces** — OpenTelemetry `ActivitySource` / `Tracer` API. W3C `traceparent` propagation across the Bridge service graph (Client → Bridge → Data API → downstream integrations). Spans are named per [ADR 0002 — kernel module format](../../docs/adrs/0002-kernel-module-format.md) conventions: `{bundle}.{module}.{operation}`. Client-generated trace IDs flow end-to-end so a support request can reference a single trace.

**Logs** — Serilog structured JSON, exported through `Serilog.Sinks.OpenTelemetry` into the same OTLP pipeline as metrics and traces. Every log line carries `TraceId` and `SpanId` enricher output, so a log-view pivot into a trace is a single click in any OTLP-aware tool (Aspire dashboard, Grafana/Loki/Tempo, Azure Monitor). PII scrubbing baseline lives under §Privacy in telemetry.

**Events** — Domain events published through the kernel event bus (per [ADR 0003](../../docs/adrs/0003-event-bus-distribution-semantics.md)) are a **business-level** observability surface, distinct from ops telemetry. A domain event records *what happened in the business*; a trace span records *what happened in the runtime*. They correlate via trace context but should not be conflated: a tenant operator wants to see `LeaseRenewed` events; the on-call engineer wants to see request latencies. Same data model, different audiences.

## SLI and SLO framework

Published when the first production tenant lands. Pre-activation this section is the template, not live targets.

**SLI definitions** — at minimum, per service boundary:

- **Availability** — `successful_requests / total_requests` over a rolling window. Success defined as HTTP status < 500 (excluding 5xx on dependency failures not caused by Sunfish).
- **Latency** — p50, p95, p99 from the OpenTelemetry `http.server.request.duration` histogram. Separate read paths from write paths; they have different acceptable envelopes.
- **Error rate** — `5xx_responses / total_responses`.
- **Saturation** — per-service concurrency, queue depth, thread-pool pressure from `OpenTelemetry.Instrumentation.Runtime`.

**Starter SLO targets** — placeholders until real traffic exists to calibrate against:

| Stage | Availability | p95 latency (reads) | p95 latency (writes) |
|---|---|---|---|
| First production tenant | 99.5% | 500 ms | 1 000 ms |
| 1.0 GA | 99.9% | 300 ms | 800 ms |
| Hosted SaaS offering | 99.95% | 250 ms | 600 ms |

These numbers are not commitments yet — they are the shape an SLO commitment takes. Real numbers come from measuring actual traffic for at least four weeks before publishing, so the SLO reflects the service rather than a wish.

**Error budgets** — monthly windows. A 99.9% SLO over 30 days is ~43 minutes of error budget. Budget burn-rate alerts fire at 2 % of monthly budget consumed in 1 hour (fast burn) and 10 % over 6 hours (slow burn), which is the Google SRE book's standard multi-window pattern.

**Review cadence** — quarterly, co-scheduled with the roadmap review in [roadmap-tracker.md](../product/roadmap-tracker.md). SLOs that consistently meet target get tightened or retired; SLOs that consistently miss get decomposed or the underlying service gets investment.

## Incident response

**Severity taxonomy** (activates with the first production tenant):

| Severity | Definition | Response target | Escalation |
|---|---|---|---|
| **Sev1** | Data loss, broad outage, security incident affecting multiple tenants | Acknowledge in 15 min; communicate to all affected tenants in 60 min | Incident commander + security-contact coordination; status page updated |
| **Sev2** | Degraded service — SLO at risk, partial functionality loss across a tenant or bundle | Acknowledge in 60 min; update hourly until mitigated | Incident commander; status page updated |
| **Sev3** | Single-tenant issue or non-critical path broken | Acknowledge in 4 business hours | Owning committer handles; no status page unless escalated |
| **Sev4** | Non-production, cosmetic, or clearly low-impact | Logged as an issue; no paging | Normal issue triage |

**Incident commander** — one named role per incident, responsible for driving the response, coordinating communication, and declaring end-of-incident. Pre-community: the BDFL is always the IC. Post-triggers: an on-call rotation among committers, documented in a repo-internal rotation file and mirrored in the paging tool of choice.

**Communication channels** — in priority order:

1. Status page (Sunfish-hosted; see §Tooling path) — public-facing, tenant-visible.
2. GitHub Security Advisory — only for security-class incidents, per [`.github/SECURITY.md`](../../.github/SECURITY.md).
3. Direct tenant notification — email or in-app banner for tenant-scoped issues.
4. GitHub issue — for Sev3 / Sev4 post-hoc tracking.

## Post-mortems

Blameless by construction, per the [Google SRE book](https://sre.google/sre-book/postmortem-culture/). The focus is on the system and the process that allowed the incident — never the individual. Maintainers who have caused incidents in good faith and participated in the post-mortem have demonstrated the trustworthiness the project wants, not the reverse.

**Required** after every Sev1 and Sev2. **Optional** for Sev3 when a recurring pattern is visible. Not required for Sev4.

**Structure** (template lives in the pending `icm/templates/post-mortem.md`):

1. **Timeline** — wall-clock record of detection, escalation, mitigation, resolution.
2. **Impact** — what tenants saw, for how long, and the SLO budget consumed.
3. **Root cause** — the technical cause, phrased as a chain rather than a single blame-point.
4. **Contributing factors** — what made the incident possible, detectable, or recoverable (or not).
5. **Action items** — each with an owner and a due date; tracked as issues.
6. **Lessons learned** — what changes in how we build, test, or operate.

Published where tenants can read them. Sensitive details (customer data, credentials, identifying incident-reporter information) are redacted; the technical and process learnings are not. Transparency is the trade the project makes for running someone's data.

## Runbooks

**One runbook per Bridge-hosted service path** — when a service path exists. Stored in `accelerators/bridge/runbooks/` once the directory is activated.

**Format** — symptom first, not architecture first:

1. **Symptom** — what the on-call sees in the paging alert or dashboard.
2. **Investigation steps** — specific queries and dashboards to check, in order.
3. **Probable causes** — ranked by past frequency.
4. **Remediation** — commands, feature-flag toggles, rollback procedures; each step explicitly reversible or labelled irreversible.
5. **Escalation** — when to pull in a second engineer or the IC.

**Game days** — annual minimum once production exists. A game day rehearses a runbook against a simulated failure; a runbook that hasn't been exercised in a year is treated as unverified and is re-tested before it is trusted in an incident.

## Privacy in telemetry

Cross-reference to [data-privacy.md](data-privacy.md). Baseline rules that apply here regardless of what that doc adds:

- **No PII in logs or traces by default.** Serilog destructuring policies exclude known PII fields (email, name, address, phone, SSN, tenant-user identifiers mapped to people) from structured log properties. `ILogger` calls that reference a principal emit the opaque principal ID, never the email.
- **Scrubbing middleware** is applied at the log-sink boundary as belt-and-braces: if a developer accidentally logs a user object, the scrubber drops known PII properties before export.
- **Tenant ID in logs for support** — every log line carries a `TenantId` enricher so a support ticket can scope a query. Cross-tenant leakage in logs is a Sev1 — tenant isolation is a data-plane guarantee that extends to telemetry.
- **Sampling** — head-based random sampling at 10 % for traces pre-production; tail-based sampling once scale justifies the collector complexity. Always-on sampling for error traces (status >= 500) so failure modes are never lost.
- **Retention** — 30 days for logs and traces by default; 13 months for aggregated metrics. Longer retention is a per-tenant opt-in, not a platform default.

## Tooling path

Deployment-mode-dependent, deliberately not prescribed up the stack:

- **Dev** — Aspire dashboard (already wired). No production claim made.
- **Self-hosted** — deployer's choice. Grafana / Loki / Tempo (OSS, OTLP-native) is the reference recommendation; Azure Monitor, AWS CloudWatch, Datadog, Honeycomb, and any other OTLP-speaking backend are fully supported because the app exports OTLP and doesn't know or care what's on the other side.
- **Sunfish-hosted SaaS** — TBD when the first hosted tenant lands. Commercial options are evaluated against tenant data-residency and price-per-GB at that point, not speculatively.

The contract the code commits to: **OTLP out, nothing vendor-specific in the instrumentation layer.** Swapping backends is a deployment-config change, not a code change.

## Activation triggers

Tied to the transition triggers in [`GOVERNANCE.md`](../../GOVERNANCE.md):

| Trigger | Activates |
|---|---|
| **First production tenant** (Sunfish-hosted or self-hosted reported to Sunfish) | Publish SLIs + starter SLOs + status page; designate incident commander (initially BDFL with a named backup); runbook for the tenant's service path. |
| **3+ production tenants** | On-call rotation among committers; severity taxonomy formalized and paged; runbook library required for every Bridge service path. |
| **First Sev1 incident** | Post-mortem practice begins (blameless template live); runbook required for the affected service path if it didn't exist; game-day cadence scheduled. |
| **Hosted-SaaS offering launches** | Full SRE stack — dedicated observability backend, paging provider, customer status page, DPA-backed retention policy, on-call compensation model. |

## What Sunfish will not do pre-trigger

- No synthetic "99.99% uptime" marketing claims without measurement behind them. Claims with no SLI attached are lies.
- No status page before there is traffic whose status is worth reporting. A green status page with zero tenants is theatre.
- No on-call pager for a solo maintainer. A 24×7 pager for one person is a burnout plan, not an operations model.
- No pretending the Aspire dashboard is production monitoring. It is a dev tool — useful, but not what a tenant's uptime depends on.

## Commercial SRE offering

Per vision §Business model ([vision.md](../product/vision.md)), commercial revenue paths include SRE work:

- **Managed-SaaS hosting** includes the full SRE stack as the value proposition — a tenant pays Sunfish to run the service and absorb the operational complexity.
- **Implementation services** include SRE setup for self-hosters who want Sunfish expertise configuring their backend of choice (Grafana stack, Azure Monitor, Datadog), writing their first runbooks, and sitting alongside them through their first incident.

Neither offering activates until a tenant signals demand. Until then, this doc is the shape, not the sales pitch.

## Cross-references

- [`../product/vision.md`](../product/vision.md) — deployment growth path from self-hosted to managed SaaS, business model framing.
- [`../../GOVERNANCE.md`](../../GOVERNANCE.md) — transition triggers that gate activation.
- [data-privacy.md](data-privacy.md) — PII handling, tenant data protections, cross-tenant isolation in telemetry.
- [supply-chain-security.md](supply-chain-security.md) — security incident response pathway feeds into Sev1 incident handling.
- [ci-quality-gates.md](ci-quality-gates.md) — the gates that reduce the incidents this doc has to respond to.
- [ai-code-policy.md](ai-code-policy.md) — AI-generated change provenance, relevant when a post-mortem traces cause.
- [ADR 0002 — kernel module format](../../docs/adrs/0002-kernel-module-format.md) — span naming conventions.
- [ADR 0003 — event bus distribution semantics](../../docs/adrs/0003-event-bus-distribution-semantics.md) — domain-event observability.
- [ADR 0006 — Bridge is a SaaS shell](../../docs/adrs/0006-bridge-is-saas-shell.md) — Bridge owns shell-level observability; bundles emit bundle-level metrics.
- [ADR 0007 — bundle manifest schema](../../docs/adrs/0007-bundle-manifest-schema.md) — bundle metric naming.
- [`accelerators/bridge/README.md`](../../accelerators/bridge/README.md) — Bridge as the first activation target.
- External: [Google SRE book](https://sre.google/books/), [OpenTelemetry semantic conventions](https://opentelemetry.io/docs/specs/semconv/), [.NET observability with OpenTelemetry](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel), [Aspire telemetry](https://learn.microsoft.com/dotnet/aspire/fundamentals/telemetry), [Serilog](https://serilog.net/).
