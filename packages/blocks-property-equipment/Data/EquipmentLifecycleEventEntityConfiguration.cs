using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyEquipment.Data;

/// <summary>
/// EF Core configuration for <see cref="EquipmentLifecycleEvent"/>. Append-only
/// log table; ids and tenant scoping via value converters.
/// </summary>
public sealed class EquipmentLifecycleEventEntityConfiguration : IEntityTypeConfiguration<EquipmentLifecycleEvent>
{
    /// <summary>Table name — stable, reverse-DNS-adjacent snake_case.</summary>
    public const string TableName = "property_equipment_lifecycle_event";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EquipmentLifecycleEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        builder.HasKey(x => x.EventId);

        builder.Property(x => x.EventId).IsRequired();

        builder.Property(x => x.Equipment)
            .HasConversion(id => id.Value, value => new EquipmentId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Property)
            .HasConversion(id => id.Value, value => new PropertyId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .HasConversion(tid => tid.Value, value => new TenantId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.OccurredAt).IsRequired();

        builder.Property(x => x.RecordedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Notes);

        // Metadata dictionary — defer EF mapping; persistence-backed hosts handle JSON serialization.
        builder.Ignore(x => x.Metadata);

        builder.HasIndex(x => new { x.TenantId, x.Equipment, x.OccurredAt })
            .HasDatabaseName("ix_property_equipment_lifecycle_tenant_asset_time");

        builder.HasIndex(x => new { x.TenantId, x.Property, x.OccurredAt })
            .HasDatabaseName("ix_property_equipment_lifecycle_tenant_property_time");
    }
}
