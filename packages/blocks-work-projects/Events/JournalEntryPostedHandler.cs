using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects.Events;

/// <summary>
/// <see cref="IEventHandler{TPayload}"/> for
/// <c>Financial.JournalEntryPosted</c>. For every line that carries a
/// <c>"projectId"</c> dimension, upserts a single
/// <see cref="ProjectActual"/> row. Idempotent on the composite key
/// <c>(projectId, sourceKind, sourceRefId)</c>.
/// </summary>
/// <remarks>
/// Reversal handling is OUT OF SCOPE — see Stage 06 PR 4 hand-off
/// "Reversal handling (deferred)" note. When financial publishes
/// <c>Financial.JournalEntryReversed</c>, a separate handler will
/// either tombstone the matching row or post a new compensating row.
/// </remarks>
public sealed class JournalEntryPostedHandler : IEventHandler<JournalEntryPostedPayload>
{
    /// <summary>System principal for projector-authored audit rows.</summary>
    public static readonly Guid ProjectorPrincipalId = new("00000000-0000-0000-0000-00000000a1ac");

    private readonly IProjectActualRepository _repository;
    private readonly IGlAccountCategoryResolver _categoryResolver;
    private readonly Func<Instant> _now;

    public JournalEntryPostedHandler(
        IProjectActualRepository repository,
        IGlAccountCategoryResolver? categoryResolver = null,
        Func<Instant>? now = null)
    {
        _repository       = repository ?? throw new ArgumentNullException(nameof(repository));
        _categoryResolver = categoryResolver ?? new FallbackGlAccountCategoryResolver();
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

            var existing = await _repository.FindAsync(
                envelope.TenantId, projectId, sourceKind, payload.EntryId, cancellationToken)
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

            await _repository.InsertAsync(actual, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Maps the financial cluster's <c>JournalEntrySource</c> string
    /// onto <see cref="ActualSourceKind"/>. Unknown values fall back
    /// to <see cref="ActualSourceKind.JournalEntry"/>.
    /// </summary>
    public static ActualSourceKind MapSourceKind(string financialSourceKind) =>
        financialSourceKind switch
        {
            "TimeEntry" => ActualSourceKind.TimeEntry,
            "Invoice"   => ActualSourceKind.Invoice,
            "Manual"    => ActualSourceKind.Manual,
            _           => ActualSourceKind.JournalEntry,
        };
}
