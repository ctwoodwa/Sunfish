using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// <see cref="IEventHandler{TPayload}"/> for
/// <c>Financial.JournalEntryPosted</c>. For every line that carries a
/// <c>"projectId"</c> dimension, upserts a single
/// <see cref="ProjectActual"/> row keyed on the composite
/// <c>(projectId, sourceKind, sourceRefId, glAccountId)</c> — the
/// <c>glAccountId</c> tail keeps per-line granularity intact when a
/// single JE splits cost across accounts on the same project
/// (e.g. Labor + Materials).
/// </summary>
/// <remarks>
/// Trust contract: <see cref="DomainEventEnvelope{TPayload}.TenantId"/>
/// is taken at face value; envelope authenticity is the responsibility
/// of <c>foundation-events</c> (signed envelopes, replica-id provenance).
/// Reversal handling is OUT OF SCOPE — see Stage 06 PR 4 hand-off
/// "Reversal handling (deferred)" note.
/// </remarks>
public sealed class JournalEntryPostedHandler : IEventHandler<JournalEntryPostedPayload>
{
    /// <summary>System principal for projector-authored audit rows.</summary>
    public static readonly Guid ProjectorPrincipalId = new("00000000-0000-0000-0000-00000000a1ac");

    private readonly IProjectActualReader _reader;
    private readonly IProjectActualWriter _writer;
    private readonly IGlAccountCategoryResolver _categoryResolver;
    private readonly ILogger<JournalEntryPostedHandler> _logger;
    private readonly Func<Instant> _now;

    public JournalEntryPostedHandler(
        IProjectActualRepository repository,
        IGlAccountCategoryResolver? categoryResolver = null,
        ILogger<JournalEntryPostedHandler>? logger = null,
        Func<Instant>? now = null)
        : this(repository, repository, categoryResolver, logger, now)
    {
    }

    public JournalEntryPostedHandler(
        IProjectActualReader reader,
        IProjectActualWriter writer,
        IGlAccountCategoryResolver? categoryResolver = null,
        ILogger<JournalEntryPostedHandler>? logger = null,
        Func<Instant>? now = null)
    {
        _reader           = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer           = writer ?? throw new ArgumentNullException(nameof(writer));
        _categoryResolver = categoryResolver ?? new FallbackGlAccountCategoryResolver();
        _logger           = logger ?? NullLogger<JournalEntryPostedHandler>.Instance;
        _now              = now ?? (() => Instant.Now);
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        DomainEventEnvelope<JournalEntryPostedPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var payload = envelope.Payload;
        var sourceKind = MapSourceKind(payload.SourceKind);

        foreach (var line in payload.Lines)
        {
            if (!line.Dimensions.TryGetValue("projectId", out var projectIdStr)
                || !Guid.TryParse(projectIdStr, out var projectIdGuid))
                continue;

            var projectId = new ProjectId(projectIdGuid);

            var existing = await _reader.FindAsync(
                envelope.TenantId, projectId, sourceKind, payload.EntryId, line.AccountId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null) continue;

            var category = await _categoryResolver.ResolveAsync(
                envelope.TenantId, line.AccountId, cancellationToken).ConfigureAwait(false);

            var actual = ProjectActual.Create(
                tenantId:     envelope.TenantId,
                id:           ProjectActualId.NewId(),
                projectId:    projectId,
                category:     category,
                glAccountId:  line.AccountId,
                postedAmount: line.Debit - line.Credit,
                currency:     line.Currency ?? "USD",
                postedDate:   payload.EntryDate,
                sourceKind:   sourceKind,
                sourceRefId:  payload.EntryId,
                createdAt:    _now(),
                createdBy:    ProjectorPrincipalId);

            await _writer.InsertAsync(actual, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Maps the financial cluster's <c>JournalEntrySource</c> string
    /// onto <see cref="ActualSourceKind"/>. Unknown values fall back
    /// to <see cref="ActualSourceKind.JournalEntry"/> + log a warning
    /// so protocol drift between clusters is observable.
    /// </summary>
    internal ActualSourceKind MapSourceKind(string financialSourceKind)
    {
        switch (financialSourceKind)
        {
            case "TimeEntry": return ActualSourceKind.TimeEntry;
            case "Invoice":   return ActualSourceKind.Invoice;
            case "Manual":    return ActualSourceKind.Manual;
            case "Bill":
            case "Payment":
            case "Receipt":
            case "Reversal":  return ActualSourceKind.JournalEntry;
            default:
                _logger.LogWarning(
                    "Unknown JournalEntrySource '{SourceKind}'; projecting as JournalEntry.",
                    financialSourceKind);
                return ActualSourceKind.JournalEntry;
        }
    }
}
