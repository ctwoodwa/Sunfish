using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Subscriptions.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Subscriptions.Data;

/// <summary>
/// EF Core configuration for <see cref="MeteredUsage"/>. Maps the tenant-scoped
/// usage sample record to the <c>subscriptions_usage</c> table.
/// </summary>
public sealed class MeteredUsageEntityConfiguration : IEntityTypeConfiguration<MeteredUsage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MeteredUsage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subscriptions_usage");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId)
            .HasConversion(v => v.Value, v => new TenantId(v))
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.MeterId)
            .HasConversion(v => v.Value, v => new UsageMeterId(v))
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.Quantity).HasPrecision(18, 4);
        builder.Property(u => u.RecordedAtUtc);

        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => new { u.TenantId, u.MeterId, u.RecordedAtUtc });
    }
}
