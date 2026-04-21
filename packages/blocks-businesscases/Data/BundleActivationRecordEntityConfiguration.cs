using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.BusinessCases.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.BusinessCases.Data;

/// <summary>
/// EF Core configuration for <see cref="BundleActivationRecord"/>. Maps the
/// strong-typed ids and tenant id to <c>string</c> columns via value converters
/// and locks the entity to the <c>businesscases_bundle_activation_records</c> table.
/// </summary>
public sealed class BundleActivationRecordEntityConfiguration
    : IEntityTypeConfiguration<BundleActivationRecord>
{
    /// <summary>Table name — stable, reverse-DNS-adjacent snake_case.</summary>
    public const string TableName = "businesscases_bundle_activation_records";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BundleActivationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new BundleActivationRecordId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .HasConversion(tid => tid.Value, value => new TenantId(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.BundleKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Edition)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ActivatedAt)
            .IsRequired();

        builder.Property(x => x.DeactivatedAt);

        builder.HasIndex(x => new { x.TenantId, x.BundleKey })
            .IsUnique()
            .HasDatabaseName("ix_businesscases_activation_tenant_bundle");
    }
}
