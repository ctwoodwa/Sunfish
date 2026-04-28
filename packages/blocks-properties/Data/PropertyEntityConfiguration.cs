using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Properties.Data;

/// <summary>
/// EF Core configuration for <see cref="Property"/>. Maps strong-typed ids
/// and tenant id to <c>string</c> columns via value converters, owns
/// <see cref="PostalAddress"/> as a complex type, and locks the entity to
/// the <c>properties_property</c> table.
/// </summary>
public sealed class PropertyEntityConfiguration : IEntityTypeConfiguration<Property>
{
    /// <summary>Table name — stable, reverse-DNS-adjacent snake_case.</summary>
    public const string TableName = "properties_property";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new PropertyId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .HasConversion(tid => tid.Value, value => new TenantId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.ParcelNumber)
            .HasMaxLength(64);

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.AcquisitionCost)
            .HasPrecision(18, 4);

        builder.Property(x => x.AcquiredAt);

        builder.Property(x => x.YearBuilt);

        builder.Property(x => x.TotalSquareFeet)
            .HasPrecision(12, 2);

        builder.Property(x => x.TotalBedrooms);

        builder.Property(x => x.TotalBathrooms)
            .HasPrecision(5, 2);

        builder.Property(x => x.Notes);

        builder.Property(x => x.PrimaryPhotoBlobRef)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.DisposedAt);

        builder.Property(x => x.DisposalReason)
            .HasMaxLength(512);

        builder.OwnsOne(x => x.Address, address =>
        {
            address.Property(a => a.Line1).HasMaxLength(256).IsRequired();
            address.Property(a => a.Line2).HasMaxLength(256);
            address.Property(a => a.City).HasMaxLength(128).IsRequired();
            address.Property(a => a.Region).HasMaxLength(128).IsRequired();
            address.Property(a => a.PostalCode).HasMaxLength(32).IsRequired();
            address.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
            address.Property(a => a.Latitude);
            address.Property(a => a.Longitude);
        });

        builder.HasIndex(x => new { x.TenantId, x.DisposedAt })
            .HasDatabaseName("ix_properties_property_tenant_disposed");

        builder.HasIndex(x => new { x.TenantId, x.ParcelNumber })
            .HasDatabaseName("ix_properties_property_tenant_parcel");
    }
}
