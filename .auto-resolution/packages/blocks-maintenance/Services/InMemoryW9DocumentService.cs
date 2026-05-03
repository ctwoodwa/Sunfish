using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.Crypto;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// In-memory <see cref="IW9DocumentService"/> for tests + non-production
/// hosts (W#18 Phase 4 / ADR 0058). Encrypts the TIN at write-time
/// via the supplied <see cref="IFieldEncryptor"/>; decrypts on demand
/// via <see cref="IFieldDecryptor"/>. Per-tenant indexed so cross-tenant
/// reads return null even when ids collide.
/// </summary>
public sealed class InMemoryW9DocumentService : IW9DocumentService
{
    private readonly IFieldEncryptor _encryptor;
    private readonly IFieldDecryptor _decryptor;
    private readonly ConcurrentDictionary<(TenantId Tenant, W9DocumentId Id), W9Document> _documents = new();

    public InMemoryW9DocumentService(IFieldEncryptor encryptor, IFieldDecryptor decryptor)
    {
        ArgumentNullException.ThrowIfNull(encryptor);
        ArgumentNullException.ThrowIfNull(decryptor);
        _encryptor = encryptor;
        _decryptor = decryptor;
    }

    public async Task<W9Document> CreateAsync(CreateW9DocumentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var encrypted = await _encryptor.EncryptAsync(request.PlaintextTin, request.Tenant, ct).ConfigureAwait(false);
        var document = new W9Document
        {
            Id = new W9DocumentId(Guid.NewGuid()),
            Vendor = request.Vendor,
            LegalName = request.LegalName,
            DbaName = request.DbaName,
            TaxClassification = request.TaxClassification,
            TinEncrypted = encrypted,
            Address = request.Address,
            SignatureRef = request.SignatureRef,
            ReceivedAt = request.ReceivedAt,
        };
        _documents[(request.Tenant, document.Id)] = document;
        return document;
    }

    public Task<W9Document?> GetAsync(W9DocumentId id, TenantId tenant, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _documents.TryGetValue((tenant, id), out var document);
        return Task.FromResult(document);
    }

    public async Task<W9DocumentView> GetWithDecryptedTinAsync(
        W9DocumentId id,
        IDecryptCapability capability,
        TenantId tenant,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (!_documents.TryGetValue((tenant, id), out var document))
        {
            throw new InvalidOperationException($"W9Document '{id.Value}' not found in tenant '{tenant.Value}'.");
        }

        var tin = await _decryptor.DecryptAsync(document.TinEncrypted, capability, tenant, ct).ConfigureAwait(false);
        return new W9DocumentView
        {
            Id = document.Id,
            Vendor = document.Vendor,
            LegalName = document.LegalName,
            DbaName = document.DbaName,
            TaxClassification = document.TaxClassification,
            Tin = tin,
            Address = document.Address,
            SignatureRef = document.SignatureRef,
            ReceivedAt = document.ReceivedAt,
            VerifiedAt = document.VerifiedAt,
            VerifiedBy = document.VerifiedBy,
        };
    }

    public Task<W9Document> VerifyAsync(W9DocumentId id, ActorId verifiedBy, TenantId tenant, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_documents.TryGetValue((tenant, id), out var existing))
        {
            throw new InvalidOperationException($"W9Document '{id.Value}' not found in tenant '{tenant.Value}'.");
        }

        if (existing.VerifiedAt is not null)
        {
            return Task.FromResult(existing);
        }

        var verified = existing with { VerifiedAt = DateTimeOffset.UtcNow, VerifiedBy = verifiedBy };
        _documents[(tenant, id)] = verified;
        return Task.FromResult(verified);
    }
}
