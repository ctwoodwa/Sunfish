using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Subscriptions.Models;

namespace Sunfish.Blocks.Subscriptions.Data;

/// <summary>
/// EF Core configuration for <see cref="Plan"/>. Maps the catalog-level plan
/// record to the <c>subscriptions_plans</c> table.
/// </summary>
public sealed class PlanEntityConfiguration : IEntityTypeConfiguration<Plan>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subscriptions_plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(v => v.Value, v => new PlanId(v))
            .HasMaxLength(64);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Edition).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(p => p.Description).HasMaxLength(512);
    }
}
