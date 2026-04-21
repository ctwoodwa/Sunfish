using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.TenantAdmin.Models;

namespace Sunfish.Blocks.TenantAdmin.Data;

/// <summary>
/// EF Core entity configuration for <see cref="TenantUser"/>. Bridge composes
/// this via <see cref="TenantAdminEntityModule"/> per ADR 0015.
/// </summary>
internal sealed class TenantUserEntityConfiguration
    : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("tenant_admin_users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(v => v.Value, v => new TenantUserId(v))
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .HasConversion(v => v.Value, v => new Sunfish.Foundation.Assets.Common.TenantId(v))
            .IsRequired();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(256);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.InvitedAt).IsRequired();
        builder.Property(u => u.AcceptedAt);

        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

        // Tenant query-filter is applied centrally by Bridge (ADR 0015).
    }
}
