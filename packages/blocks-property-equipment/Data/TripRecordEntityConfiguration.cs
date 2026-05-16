using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyEquipment.Data;

/// <summary>
/// EF Core configuration for <see cref="TripRecord"/>. W#61 — append-only
/// log of vehicle trips. Indexes the per-equipment and per-property lookup
/// paths used by <see cref="Services.ITripStore.GetForEquipmentAsync"/>
/// and future per-property trip queries.
/// </summary>
public sealed class TripRecordEntityConfiguration : IEntityTypeConfiguration<TripRecord>
{
    /// <summary>Table name — stable, reverse-DNS-adjacent snake_case.</summary>
    public const string TableName = "property_equipment_trip_record";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TripRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new TripRecordId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .HasConversion(tid => tid.Value, value => new TenantId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.EquipmentId)
            .HasConversion(id => id.Value, value => new EquipmentId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.PropertyId)
            .HasConversion(pid => pid.Value, value => new PropertyId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TripDate).IsRequired();
        builder.Property(x => x.StartOdometer).HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.EndOdometer).HasPrecision(12, 2).IsRequired();
        // Miles is computed; not persisted.
        builder.Ignore(x => x.Miles);

        builder.Property(x => x.Purpose).HasMaxLength(128);
        builder.Property(x => x.Notes);

        builder.HasIndex(x => new { x.TenantId, x.EquipmentId })
            .HasDatabaseName("ix_property_equipment_trip_record_tenant_equipment");

        builder.HasIndex(x => new { x.TenantId, x.PropertyId })
            .HasDatabaseName("ix_property_equipment_trip_record_tenant_property");
    }
}
