using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Bridge.Field;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Field;

/// <summary>
/// Contract tests for <c>POST /api/v1/field/unpair</c>.
/// Council Test-Coverage-B2 (2026-05-13): new endpoint, zero prior coverage.
/// </summary>
public sealed class FieldUnpairEndpointTests
{
    private static readonly TenantId BridgeAnonymousTenant = new("bridge-anonymous");

    // ===== Success path =====

    [Fact]
    public async Task Unpair_ReturnsNoContent_OnSuccess()
    {
        var (audit, signer) = MakeServices();
        var result = await FieldEndpoints.HandleFieldUnpairAsync(
            BuildRequest(deviceId: null), audit, signer, CancellationToken.None);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Unpair_EmitsFieldDeviceRevokedAudit()
    {
        var (audit, signer) = MakeServices();
        await FieldEndpoints.HandleFieldUnpairAsync(
            BuildRequest(deviceId: null), audit, signer, CancellationToken.None);

        var records = await CollectAuditRecordsAsync(audit, BridgeAnonymousTenant);
        Assert.Single(records, r => r.EventType == AuditEventType.FieldDeviceRevoked);
    }

    [Fact]
    public async Task Unpair_EmitsAuditWithBridgeAnonymousTenant_NotTenantIdSystem()
    {
        // Regression guard: Council Security amendment + ADR 0084 §1 — do NOT
        // attribute pre-validation records to TenantId.System.
        var (audit, signer) = MakeServices();
        await FieldEndpoints.HandleFieldUnpairAsync(
            BuildRequest(deviceId: null), audit, signer, CancellationToken.None);

        var records = await CollectAuditRecordsAsync(audit, BridgeAnonymousTenant);
        var record = Assert.Single(records);
        Assert.NotEqual(TenantId.System, record.TenantId);
        Assert.Equal(BridgeAnonymousTenant, record.TenantId);
    }

    // ===== Device-ID header =====

    [Fact]
    public async Task Unpair_AuditPayloadContainsDeviceIdKey_WhenHeaderProvided()
    {
        var (audit, signer) = MakeServices();
        await FieldEndpoints.HandleFieldUnpairAsync(
            BuildRequest(deviceId: "1a2b3c4d5e6f7890"), audit, signer, CancellationToken.None);

        var records = await CollectAuditRecordsAsync(audit, BridgeAnonymousTenant);
        var record = Assert.Single(records);
        Assert.True(record.Payload.Payload.Body.TryGetValue("device_id", out var id));
        Assert.Equal("1a2b3c4d5e6f7890", id?.ToString());
    }

    [Fact]
    public async Task Unpair_AuditPayloadDeviceIdIsNull_WhenHeaderAbsent()
    {
        var (audit, signer) = MakeServices();
        await FieldEndpoints.HandleFieldUnpairAsync(
            BuildRequest(deviceId: null), audit, signer, CancellationToken.None);

        var records = await CollectAuditRecordsAsync(audit, BridgeAnonymousTenant);
        var record = Assert.Single(records);
        Assert.True(record.Payload.Payload.Body.TryGetValue("device_id", out var id));
        Assert.Null(id);
    }

    [Fact]
    public async Task Unpair_AuditPayloadDeviceIdIsNull_WhenHeaderFailsValidation()
    {
        // Council Security-B1: invalid header values are not written verbatim.
        var (audit, signer) = MakeServices();
        await FieldEndpoints.HandleFieldUnpairAsync(
            BuildRequest(deviceId: "../../etc/passwd"), audit, signer, CancellationToken.None);

        var records = await CollectAuditRecordsAsync(audit, BridgeAnonymousTenant);
        var record = Assert.Single(records);
        Assert.True(record.Payload.Payload.Body.TryGetValue("device_id", out var id));
        Assert.Null(id);
    }

    [Fact]
    public async Task Unpair_AuditPayloadContainsSourceClientClaimed()
    {
        // Council Security-B1: distinguishes asserted-vs-verified identity in audit.
        var (audit, signer) = MakeServices();
        await FieldEndpoints.HandleFieldUnpairAsync(
            BuildRequest(deviceId: "abcdef1234567890"), audit, signer, CancellationToken.None);

        var records = await CollectAuditRecordsAsync(audit, BridgeAnonymousTenant);
        var record = Assert.Single(records);
        Assert.True(record.Payload.Payload.Body.TryGetValue("source", out var source));
        Assert.Equal("client-claimed", source?.ToString());
    }

    // ===== Helpers =====

    private static (InMemoryAuditTrail Audit, IOperationSigner Signer) MakeServices()
    {
        var audit = new InMemoryAuditTrail();
        var signer = new Ed25519Signer(KeyPair.Generate());
        return (audit, signer);
    }

    private static HttpRequest BuildRequest(string? deviceId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (deviceId is not null)
            ctx.Request.Headers["X-Sunfish-Device-Id"] = deviceId;
        return ctx.Request;
    }

    private static async Task<List<AuditRecord>> CollectAuditRecordsAsync(
        InMemoryAuditTrail audit, TenantId tenantId)
    {
        var list = new List<AuditRecord>();
        var query = new AuditQuery(TenantId: tenantId);
        await foreach (var r in audit.QueryAsync(query))
            list.Add(r);
        return list;
    }
}
