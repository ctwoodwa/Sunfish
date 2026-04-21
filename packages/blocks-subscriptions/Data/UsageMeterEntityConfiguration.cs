using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Subscriptions.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Subscriptions.Data;

/// <summary>
/// EF Core configuration for <see cref="UsageMeter"/>. Maps the tenant-scoped
/// meter record to the <c>subscriptions_meters</c> table.
/// </summary>
public sealed class UsageMeterEntityConfiguration : IEntityTypeConfiguration<UsageMeter>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UsageMeter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subscriptions_meters");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(v => v.Value, v => new UsageMeterId(v))
            .HasMaxLength(64);

        builder.Property(m => m.TenantId)
            .HasConversion(v => v.Value, v => new TenantId(v))
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.SubscriptionId)
            .HasConversion(v => v.Value, v => new SubscriptionId(v))
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.Code).IsRequired().HasMaxLength(64);
        builder.Property(m => m.Unit).IsRequired().HasMaxLength(32);

        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => new { m.TenantId, m.SubscriptionId, m.Code }).IsUnique();
    }
}
