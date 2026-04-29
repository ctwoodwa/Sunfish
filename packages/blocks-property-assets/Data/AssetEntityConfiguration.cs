using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyAssets.Data;

/// <summary>
/// EF Core configuration for <see cref="Asset"/>. Maps strong-typed ids
/// and tenant id to <c>string</c> columns via value converters, owns
/// <see cref="WarrantyMetadata"/> as a complex type, and locks the entity
/// to the <c>property_assets_asset</c> table.
/// </summary>
public sealed class AssetEntityConfiguration : IEntityTypeConfiguration<Asset>
{
    /// <summary>Table name — stable, reverse-DNS-adjacent snake_case.</summary>
    public const string TableName = "property_assets_asset";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new AssetId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .HasConversion(tid => tid.Value, value => new TenantId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Property)
            .HasConversion(pid => pid.Value, value => new PropertyId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Class)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Make).HasMaxLength(128);
        builder.Property(x => x.Model).HasMaxLength(128);
        builder.Property(x => x.SerialNumber).HasMaxLength(128);
        builder.Property(x => x.LocationInProperty).HasMaxLength(256);
        builder.Property(x => x.InstalledAt);

        builder.Property(x => x.AcquisitionCost)
            .HasPrecision(18, 4);

        builder.Property(x => x.AcquisitionReceiptRef).HasMaxLength(128);
        builder.Property(x => x.ExpectedUsefulLifeYears);
        builder.Property(x => x.Notes);
        builder.Property(x => x.PrimaryPhotoBlobRef).HasMaxLength(512);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.DisposedAt);
        builder.Property(x => x.DisposalReason).HasMaxLength(512);

        builder.OwnsOne(x => x.Warranty, warranty =>
        {
            warranty.Property(w => w.StartsAt).IsRequired();
            warranty.Property(w => w.ExpiresAt).IsRequired();
            warranty.Property(w => w.Provider).HasMaxLength(256);
            warranty.Property(w => w.PolicyNumber).HasMaxLength(128);
            warranty.Property(w => w.CoverageNotes);
        });

        builder.HasIndex(x => new { x.TenantId, x.Property, x.DisposedAt })
            .HasDatabaseName("ix_property_assets_asset_tenant_property_disposed");

        builder.HasIndex(x => new { x.TenantId, x.Class })
            .HasDatabaseName("ix_property_assets_asset_tenant_class");
    }
}
