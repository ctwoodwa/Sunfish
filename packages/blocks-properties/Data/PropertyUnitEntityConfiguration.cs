using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Properties.Data;

/// <summary>
/// EF Core configuration for <see cref="PropertyUnit"/>. Maps the
/// <see cref="EntityId"/> primary key, <see cref="PropertyId"/> FK, and
/// <see cref="TenantId"/> to string columns via value converters; indexes
/// the (tenant, property) and (tenant) lookup paths used by
/// <see cref="Services.IPropertyUnitRepository.ListByPropertyAsync"/> and
/// <see cref="Services.IPropertyUnitRepository.ListByTenantAsync"/>.
/// </summary>
public sealed class PropertyUnitEntityConfiguration : IEntityTypeConfiguration<PropertyUnit>
{
    /// <summary>Table name — stable, reverse-DNS-adjacent snake_case.</summary>
    public const string TableName = "properties_property_unit";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PropertyUnit> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.ToString(), value => EntityId.Parse(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .HasConversion(tid => tid.Value, value => new TenantId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.PropertyId)
            .HasConversion(id => id.Value, value => new PropertyId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.UnitNumber)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Bedrooms);

        builder.Property(x => x.Bathrooms)
            .HasPrecision(5, 2);

        builder.Property(x => x.SquareFootage)
            .HasPrecision(12, 2);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.Notes);

        builder.HasIndex(x => new { x.TenantId, x.PropertyId })
            .HasDatabaseName("ix_properties_property_unit_tenant_property");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_properties_property_unit_tenant");
    }
}
