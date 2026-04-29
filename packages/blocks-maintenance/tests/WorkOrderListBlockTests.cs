using System.Collections.Immutable;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

public class WorkOrderListBlockTests : BunitContext
{
    private static readonly EntityId TestPropertyId = new("property", "test", "prop-1");
    private static readonly TenantId TestTenant = new("tenant-test");

    // ── Test infrastructure (W#19 Phase 5.1: audit-trail spy) ───────────────────

    private sealed class CapturingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var r in Records.Where(r => r.TenantId == query.TenantId && (query.EventType is null || r.EventType == query.EventType.Value)))
            {
                yield return r;
            }
            await Task.CompletedTask;
        }
    }

    private sealed class StubSigner : IOperationSigner
    {
        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(new byte[32]);

        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default) =>
            ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Signature.FromBytes(new byte[64])));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(InMemoryMaintenanceService svc, CapturingAuditTrail trail, Vendor vendor, MaintenanceRequest request, WorkOrder workOrder)>
        MakePopulatedService()
    {
        var trail = new CapturingAuditTrail();
        var svc = new InMemoryMaintenanceService(trail, new StubSigner(), TestTenant);

        var vendor = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Speedy Plumbing",
            Specialty = VendorSpecialty.Plumbing,
        });

        var request = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "Alice Tenant",
            Description = "Leaky faucet",
            Priority = MaintenancePriority.High,
            RequestedDate = new DateOnly(2026, 5, 1),
        });

        var workOrder = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            Tenant = TestTenant,
            RequestId = request.Id,
            AssignedVendorId = vendor.Id,
            ScheduledDate = new DateOnly(2026, 5, 15),
            EstimatedCost = Sunfish.Foundation.Integrations.Payments.Money.Usd(250m),
        });

        return (svc, trail, vendor, request, workOrder);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyService_Renders_NoWorkOrdersPlaceholder()
    {
        Services.AddSingleton<IMaintenanceService, InMemoryMaintenanceService>();
        Services.AddSingleton<IAuditTrail, CapturingAuditTrail>();

        var cut = Render<WorkOrderListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-work-order-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains("No work orders", cut.Markup);
    }

    [Fact]
    public async Task PopulatedService_Renders_WorkOrderRows()
    {
        var (svc, trail, _, _, _) = await MakePopulatedService();
        Services.AddSingleton<IMaintenanceService>(svc);
        Services.AddSingleton<IAuditTrail>(trail);

        var cut = Render<WorkOrderListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-work-order-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains("Speedy Plumbing", cut.Markup);
        Assert.Contains("Draft", cut.Markup);
        Assert.Contains("2026-05-15", cut.Markup);
    }

    [Fact]
    public async Task StatusVendorPriorityColumns_AreCorrectlyComputed()
    {
        var (svc, trail, _, _, _) = await MakePopulatedService();
        Services.AddSingleton<IMaintenanceService>(svc);
        Services.AddSingleton<IAuditTrail>(trail);

        var cut = Render<WorkOrderListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-work-order-list__loading"), TimeSpan.FromSeconds(5));

        var row = cut.Find("tr.sf-work-order-list__row");
        Assert.NotNull(row);

        var statusCell = cut.Find("td.sf-work-order-list__col-status");
        Assert.Equal("Draft", statusCell.TextContent.Trim());

        var vendorCell = cut.Find("td.sf-work-order-list__col-vendor");
        Assert.Equal("Speedy Plumbing", vendorCell.TextContent.Trim());

        // W#19 Phase 5.1: source resolved via audit query — "High" priority on
        // the originating MaintenanceRequest is restored after Phase 5 dropped
        // the WorkOrder.RequestId FK.
        var priorityCell = cut.Find("td.sf-work-order-list__col-priority");
        Assert.Equal("High", priorityCell.TextContent.Trim());
    }
}
