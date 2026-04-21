using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sunfish.Blocks.Subscriptions.Models;

namespace Sunfish.Blocks.Subscriptions.Data;

/// <summary>
/// EF Core configuration for <see cref="AddOn"/>. Maps the catalog-level add-on
/// record to the <c>subscriptions_addons</c> table.
/// </summary>
public sealed class AddOnEntityConfiguration : IEntityTypeConfiguration<AddOn>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AddOn> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subscriptions_addons");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(v => v.Value, v => new AddOnId(v))
            .HasMaxLength(64);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(128);
        builder.Property(a => a.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(a => a.Description).HasMaxLength(512);
    }
}
