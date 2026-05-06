using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.EngineRoom;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.EngineRoom.Tests;

public class HealthSummaryTests
{
    [Fact]
    public void EngineRoomHealthSummary_ForHelper_ReturnsCorrectSubsystem()
    {
        var summary = new EngineRoomHealthSummary(new[]
        {
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Warning, "1 quarantine pending"),
        });

        var dc = summary.For(EngineRoomSubsystem.DamageControl);
        Assert.NotNull(dc);
        Assert.Equal(SubsystemStatus.Warning, dc!.Status);
        Assert.Equal("1 quarantine pending", dc.Message);

        Assert.Null(summary.For(EngineRoomSubsystem.QaWorkshop));
    }

    [Fact]
    public void SubsystemHealth_Status_Roundtrip()
    {
        foreach (var status in Enum.GetValues<SubsystemStatus>())
        {
            var h = new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, status, null);
            Assert.Equal(status, h.Status);
        }
    }

    [Fact]
    public void EngineRoomSubsystem_HasFourValues()
    {
        Assert.Equal(4, Enum.GetValues<EngineRoomSubsystem>().Length);
    }

    [Fact]
    public void SubsystemStatus_HasFourValues()
    {
        Assert.Equal(4, Enum.GetValues<SubsystemStatus>().Length);
    }

    [Fact]
    public void SyncDaemonStatus_HasThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<SyncDaemonStatus>().Length);
    }
}

public class EngineRoomMetricsTests
{
    [Fact]
    public void EngineRoomMetrics_NamesMatchOtelSpec()
    {
        // All instrument names are lowercase-with-dots (OTel convention)
        // except Meter / ActivitySource which use the dotted-PascalCase
        // assembly identifier.
        Assert.Equal("Sunfish.EngineRoom", EngineRoomMetrics.MeterName);
        Assert.Equal("Sunfish.EngineRoom", EngineRoomMetrics.ActivitySourceName);
        Assert.Equal("sunfish.engine_room.peer_count", EngineRoomMetrics.PeerCount);
        Assert.Equal("sunfish.engine_room.events_throughput", EngineRoomMetrics.EventsThroughput);
        Assert.Equal("sunfish.engine_room.gossip_cycles", EngineRoomMetrics.GossipCycles);
        Assert.Equal("sunfish.engine_room.crdt_total_bytes", EngineRoomMetrics.CrdtTotalBytes);
        Assert.Equal("sunfish.engine_room.crdt_compaction_eligible", EngineRoomMetrics.CrdtCompactionEligible);
        Assert.Equal("sunfish.engine_room.subsystem_status", EngineRoomMetrics.SubsystemStatusGauge);
    }

    [Fact]
    public void EngineRoomMetrics_AllConstantsAreNonEmpty()
    {
        var fields = typeof(EngineRoomMetrics).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotEmpty(fields);
        foreach (var f in fields)
        {
            var value = f.GetValue(null) as string;
            Assert.False(string.IsNullOrEmpty(value), $"{f.Name} must be non-empty");
        }
    }
}

public class ExceptionTests
{
    [Fact]
    public void EngineRoomUnauthorizedException_IsUnauthorizedAccessException()
    {
        var ex = new EngineRoomUnauthorizedException("denied");
        Assert.IsAssignableFrom<UnauthorizedAccessException>(ex);
        Assert.Equal("denied", ex.Message);
    }

    [Fact]
    public void EngineRoomUnauthorizedException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new EngineRoomUnauthorizedException("denied", inner);
        Assert.Same(inner, ex.InnerException);
    }
}

public class ContractSurfaceTests
{
    [Fact]
    public void IEngineRoomDataProvider_HasFiveMethods()
    {
        var t = typeof(IEngineRoomDataProvider);
        Assert.NotNull(t.GetMethod("GetHealthSummaryAsync"));
        Assert.NotNull(t.GetMethod("GetSyncDaemonHealthAsync"));
        Assert.Equal(2, t.GetMethods().Where(m => m.Name == "GetCrdtGrowthMetricsAsync").Count());
        Assert.NotNull(t.GetMethod("SubscribeHealthAsync"));
    }

    [Fact]
    public void IEngineRoomCommandService_HasThreeMethods()
    {
        var t = typeof(IEngineRoomCommandService);
        Assert.Equal(3, t.GetMethods().Length);
        Assert.NotNull(t.GetMethod("QuarantineDocumentAsync"));
        Assert.NotNull(t.GetMethod("ReleaseQuarantineAsync"));
        Assert.NotNull(t.GetMethod("CompactDocumentAsync"));
    }
}

public class AuditEventTypeConstantsTests
{
    [Fact]
    public void AllEightEngineRoomConstants_PresentAndPascalCase()
    {
        Assert.Equal("DocumentQuarantineRequested", AuditEventType.DocumentQuarantineRequested.Value);
        Assert.Equal("DocumentQuarantined", AuditEventType.DocumentQuarantined.Value);
        Assert.Equal("DocumentQuarantineReleaseRequested", AuditEventType.DocumentQuarantineReleaseRequested.Value);
        Assert.Equal("DocumentQuarantineReleased", AuditEventType.DocumentQuarantineReleased.Value);
        Assert.Equal("ManualCompactionInitiated", AuditEventType.ManualCompactionInitiated.Value);
        Assert.Equal("ManualCompactionCompleted", AuditEventType.ManualCompactionCompleted.Value);
        Assert.Equal("EngineRoomHealthDegraded", AuditEventType.EngineRoomHealthDegraded.Value);
        Assert.Equal("DamageControlAuthorizationDenied", AuditEventType.DamageControlAuthorizationDenied.Value);
    }

    [Fact]
    public void AllEngineRoomConstants_AreDistinctValues()
    {
        var values = new[]
        {
            AuditEventType.DocumentQuarantineRequested,
            AuditEventType.DocumentQuarantined,
            AuditEventType.DocumentQuarantineReleaseRequested,
            AuditEventType.DocumentQuarantineReleased,
            AuditEventType.ManualCompactionInitiated,
            AuditEventType.ManualCompactionCompleted,
            AuditEventType.EngineRoomHealthDegraded,
            AuditEventType.DamageControlAuthorizationDenied,
        };
        Assert.Equal(values.Length, values.Distinct().Count());
    }
}

public class ShipActionConstantsTests
{
    [Fact]
    public void AllFiveEngineRoomActions_PresentAndKebabCase()
    {
        Assert.Equal("view-engine-room", ShipAction.ViewEngineRoom.Name);
        Assert.Equal("view-damage-control", ShipAction.ViewDamageControl.Name);
        Assert.Equal("quarantine-document", ShipAction.QuarantineDocument.Name);
        Assert.Equal("release-quarantine", ShipAction.ReleaseQuarantine.Name);
        Assert.Equal("compact-document", ShipAction.CompactDocument.Name);
    }
}

public class DataModelTests
{
    [Fact]
    public void CrdtGrowthQuery_DefaultsAllOptionalFieldsToNull()
    {
        var tenant = new Sunfish.Foundation.Assets.Common.TenantId("tenant-a");
        var query = new CrdtGrowthQuery(tenant);
        Assert.Null(query.CompactionEligibleOnly);
        Assert.Null(query.PageSize);
        Assert.Null(query.ContinuationToken);
    }

    [Fact]
    public void EngineRoomHealthSummary_For_Returns_Null_OnEmpty()
    {
        var summary = new EngineRoomHealthSummary(Array.Empty<SubsystemHealth>());
        Assert.Null(summary.For(EngineRoomSubsystem.MainPropulsion));
    }
}

public class DiTests
{
    [Fact]
    public void AddSunfishEngineRoom_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EngineRoomServiceCollectionExtensions.AddSunfishEngineRoom(null!));
    }

    [Fact]
    public void AddSunfishEngineRoom_ReturnsSameContainer()
    {
        var services = new ServiceCollection();
        var same = services.AddSunfishEngineRoom();
        Assert.Same(services, same);
    }
}
