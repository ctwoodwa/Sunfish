namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Per-tenant email routing record per ADR 0067 §3.12. Identifies
/// which registered <c>TransactionalEmail</c> /
/// <c>MarketingEmail</c> provider is currently active. The Atlas
/// integration-config UI surface renders + edits this record;
/// downstream message-send paths consult it when deciding which
/// provider to route an outbound email through.
/// </summary>
/// <remarks>
/// <b>Optional-by-design:</b> tenants without an email need omit
/// this record entirely (or keep both fields null). The host MUST
/// fail-closed when a send attempt has no routing configured —
/// silent fallback to a default provider would defeat the
/// tenant-scoped configuration intent.
/// </remarks>
/// <param name="TransactionalProvider">
/// Stable provider id for transactional email (e.g.,
/// <c>"sendgrid"</c>, <c>"mailgun"</c>, <c>"ses"</c>). Null when
/// the tenant has not configured a transactional sender.
/// </param>
/// <param name="MarketingProvider">
/// Stable provider id for marketing email. Null when the tenant
/// has not configured a marketing sender.
/// </param>
public sealed record IntegrationEmailRouting(
    string? TransactionalProvider,
    string? MarketingProvider);
