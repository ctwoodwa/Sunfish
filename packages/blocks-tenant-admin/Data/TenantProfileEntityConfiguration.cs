using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.TenantAdmin.Models;

namespace Sunfish.Blocks.TenantAdmin.Data;

/// <summary>
/// EF Core entity configuration for <see cref="TenantProfile"/>. Bridge composes
/// this via <see cref="TenantAdminEntityModule"/> per ADR 0015.
/// </summary>
internal sealed class TenantProfileEntityConfiguration
    : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        builder.ToTable("tenant_admin_profiles");

        builder.HasKey(p => p.TenantId);

        builder.Property(p => p.TenantId)
            .HasConversion(v => v.Value, v => new Sunfish.Foundation.Assets.Common.TenantId(v))
            .IsRequired();

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.ContactEmail)
            .HasMaxLength(320);

        builder.Property(p => p.ContactPhone)
            .HasMaxLength(64);

        builder.Property(p => p.BundleKey)
            .HasMaxLength(128);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Tenant query-filter is applied centrally by Bridge (ADR 0015).
    }
}
