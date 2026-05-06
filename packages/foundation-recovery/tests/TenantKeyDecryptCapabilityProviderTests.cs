using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Recovery.TenantKey;
using Xunit;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// W#48 Phase 1b — coverage for
/// <see cref="TenantKeyDecryptCapabilityProvider"/>'s fail-closed
/// purpose-allowlist gate, TTL clamping, and well-formed-purpose
/// happy path.
/// </summary>
public sealed class TenantKeyDecryptCapabilityProviderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task AcquireAsync_RejectsEmptyPurpose()
    {
        var sut = new TenantKeyDecryptCapabilityProvider(new InMemoryTenantKeyProvider());

        Assert.Null(await sut.AcquireAsync(new TenantId("t-1"), "", TimeSpan.FromMinutes(5), Ct));
        Assert.Null(await sut.AcquireAsync(new TenantId("t-1"), "   ", TimeSpan.FromMinutes(5), Ct));
    }

    [Fact]
    public async Task AcquireAsync_RejectsOffAllowlistPurpose()
    {
        // M2 amendment: Phase 1b only honors purposes on
        // AcceptedPurposes. An off-allowlist purpose returns null
        // even when the underlying tenant-key provider would happily
        // derive a key.
        var sut = new TenantKeyDecryptCapabilityProvider(new InMemoryTenantKeyProvider());

        Assert.Null(await sut.AcquireAsync(new TenantId("t-1"), "thread-token-hmac", TimeSpan.FromMinutes(5), Ct));
        Assert.Null(await sut.AcquireAsync(new TenantId("t-1"), "encrypted-field-aes", TimeSpan.FromMinutes(5), Ct));
        Assert.Null(await sut.AcquireAsync(new TenantId("t-1"), "recovery-attestation", TimeSpan.FromMinutes(5), Ct));
    }

    [Fact]
    public async Task AcquireAsync_RejectsNonPositiveTtl()
    {
        var sut = new TenantKeyDecryptCapabilityProvider(new InMemoryTenantKeyProvider());

        Assert.Null(await sut.AcquireAsync(new TenantId("t-1"), "integration-validation", TimeSpan.Zero, Ct));
        Assert.Null(await sut.AcquireAsync(new TenantId("t-1"), "integration-validation", TimeSpan.FromSeconds(-5), Ct));
    }

    [Fact]
    public async Task AcquireAsync_AllowedPurpose_IssuesCapability()
    {
        var sut = new TenantKeyDecryptCapabilityProvider(new InMemoryTenantKeyProvider());

        var cap = await sut.AcquireAsync(new TenantId("t-1"), "integration-validation", TimeSpan.FromMinutes(5), Ct);
        Assert.NotNull(cap);
        Assert.Contains("integration-validation", cap!.CapabilityId);

        // Tenant binding: cap validates for its issued tenant + rejects others.
        Assert.Null(cap.ValidateForDecrypt(new TenantId("t-1"), DateTimeOffset.UtcNow));
        Assert.Equal("wrong-tenant", cap.ValidateForDecrypt(new TenantId("t-2"), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task AcquireAsync_TtlExceedingCeiling_IsClampedTo30Min()
    {
        var sut = new TenantKeyDecryptCapabilityProvider(new InMemoryTenantKeyProvider());

        var cap = await sut.AcquireAsync(new TenantId("t-1"), "integration-validation", TimeSpan.FromHours(2), Ct);
        Assert.NotNull(cap);

        // FixedDecryptCapability.ValidateUntil is internal but
        // observable via ValidateForDecrypt at the boundary: a check
        // 31 min in the future MUST reject (clamp at 30 min).
        var pastClamp = DateTimeOffset.UtcNow.AddMinutes(31);
        Assert.Equal("expired", cap!.ValidateForDecrypt(new TenantId("t-1"), pastClamp));
    }

    [Fact]
    public void AcceptedPurposes_ContainsIntegrationValidation()
    {
        Assert.Contains("integration-validation", TenantKeyDecryptCapabilityProvider.AcceptedPurposes);
        // Sanity bound: the v1 allowlist is explicitly narrow.
        Assert.True(TenantKeyDecryptCapabilityProvider.AcceptedPurposes.Count <= 4);
    }
}
