using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.TenantAdmin.Models;

namespace Sunfish.Blocks.TenantAdmin.Data;

/// <summary>
/// EF Core entity configuration for <see cref="BundleActivation"/>. Bridge
/// composes this via <see cref="TenantAdminEntityModule"/> per ADR 0015.
/// </summary>
internal sealed class BundleActivationEntityConfiguration
    : IEntityTypeConfiguration<BundleActivation>
{
    public void Configure(EntityTypeBuilder<BundleActivation> builder)
    {
        builder.ToTable("tenant_admin_bundle_activations");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(v => v.Value, v => new BundleActivationId(v))
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasConversion(v => v.Value, v => new Sunfish.Foundation.Assets.Common.TenantId(v))
            .IsRequired();

        builder.Property(a => a.BundleKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.Edition)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.ActivatedAt).IsRequired();
        builder.Property(a => a.DeactivatedAt);

        builder.HasIndex(a => new { a.TenantId, a.BundleKey });

        // Tenant query-filter is applied centrally by Bridge (ADR 0015).
    }
}
