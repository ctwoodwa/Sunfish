using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.ShipsOffice.Services;

/// <summary>
/// Minimal stub — pending canonical <c>foundation-forms</c> /
/// <c>foundation-ships-office</c> substrate hand-off per ADR 0055.
/// Local declaration in this assembly per xo-ruling-T02-43Z (Option (a):
/// local stub pending canonical home — TBD). Tagged for sweep-and-
/// migrate when canonical lands.
/// </summary>
/// <remarks>
/// TODO-RELOCATE-WHEN-CANONICAL: replace with
/// <c>Sunfish.Foundation.Forms.IFormSchemaStore</c> (or equivalent
/// canonical home) once its Stage 06 hand-off ships. Minimal surface
/// only — W#55 Phase 5 consumes <see cref="ListByTenantAsync"/> for
/// the <c>DynamicTemplate</c> kind branch.
/// </remarks>
public interface IFormSchemaStore
{
    Task<FormSchema?> GetByIdAsync(FormSchemaId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FormSchema>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op default. Returns null / empty. Host composition root overrides
/// with a real implementation when ADR 0055 substrate is wired.
/// </summary>
/// <remarks>
/// TODO-RELOCATE-WHEN-CANONICAL: drop when
/// <see cref="IFormSchemaStore"/> moves to its canonical home.
/// </remarks>
public sealed class NoopFormSchemaStore : IFormSchemaStore
{
    public Task<FormSchema?> GetByIdAsync(FormSchemaId id, CancellationToken cancellationToken = default)
        => Task.FromResult<FormSchema?>(null);

    public Task<IReadOnlyList<FormSchema>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<FormSchema>>(Array.Empty<FormSchema>());
}
