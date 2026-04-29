using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

public class WorkOrderListBlockTests : BunitContext
{
    private static readonly EntityId TestPropertyId = new("property", "test", "prop-1");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(InMemoryMaintenanceService svc, Vendor vendor, MaintenanceRequest request, WorkOrder workOrder)>
        MakePopulatedService()
    {
        var svc = new InMemoryMaintenanceService();

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
            Tenant = new TenantId("tenant-test"),
            RequestId = request.Id,
            AssignedVendorId = vendor.Id,
            ScheduledDate = new DateOnly(2026, 5, 15),
            EstimatedCost = Sunfish.Foundation.Integrations.Payments.Money.Usd(250m),
        });

        return (svc, vendor, request, workOrder);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyService_Renders_NoWorkOrdersPlaceholder()
    {
        Services.AddSingleton<IMaintenanceService, InMemoryMaintenanceService>();

        var cut = Render<WorkOrderListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-work-order-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains("No work orders", cut.Markup);
    }

    [Fact]
    public async Task PopulatedService_Renders_WorkOrderRows()
    {
        var (svc, _, _, _) = await MakePopulatedService();
        Services.AddSingleton<IMaintenanceService>(svc);

        var cut = Render<WorkOrderListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-work-order-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains("Speedy Plumbing", cut.Markup);
        Assert.Contains("Draft", cut.Markup);
        Assert.Contains("2026-05-15", cut.Markup);
    }

    [Fact]
    public async Task StatusVendorSourceColumns_AreCorrectlyComputed()
    {
        var (svc, _, _, _) = await MakePopulatedService();
        Services.AddSingleton<IMaintenanceService>(svc);

        var cut = Render<WorkOrderListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-work-order-list__loading"), TimeSpan.FromSeconds(5));

        var row = cut.Find("tr.sf-work-order-list__row");
        Assert.NotNull(row);

        var statusCell = cut.Find("td.sf-work-order-list__col-status");
        Assert.Equal("Draft", statusCell.TextContent.Trim());

        var vendorCell = cut.Find("td.sf-work-order-list__col-vendor");
        Assert.Equal("Speedy Plumbing", vendorCell.TextContent.Trim());

        // W#19 Phase 5 dropped the request-fetching feature; Phase 5.1 will
        // restore via audit-query. The block now renders a placeholder.
        var sourceCell = cut.Find("td.sf-work-order-list__col-source");
        Assert.Contains("see audit trail", sourceCell.TextContent.Trim());
    }
}
