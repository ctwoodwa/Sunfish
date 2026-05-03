using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Subscriptions.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Subscriptions.Data;

/// <summary>
/// EF Core configuration for <see cref="Subscription"/>. Maps the tenant-scoped
/// subscription record to the <c>subscriptions_subscriptions</c> table.
/// </summary>
public sealed class SubscriptionEntityConfiguration : IEntityTypeConfiguration<Subscription>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subscriptions_subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(v => v.Value, v => new SubscriptionId(v))
            .HasMaxLength(64);

        builder.Property(s => s.TenantId)
            .HasConversion(v => v.Value, v => new TenantId(v))
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(s => s.PlanId)
            .HasConversion(v => v.Value, v => new PlanId(v))
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(s => s.Edition).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.StartDate);
        builder.Property(s => s.EndDate);

        builder.Ignore(s => s.AddOns);

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => new { s.TenantId, s.PlanId });
    }
}
