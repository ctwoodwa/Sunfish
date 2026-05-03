using System.Linq.Expressions;
using System.Text.Json;
using Sunfish.Foundation.Authorization;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Sunfish.Bridge.Data;

/// <summary>
/// Bridge's shared DbContext. Composes entity configurations contributed by
/// every registered <see cref="ISunfishEntityModule"/> (ADR 0015) and applies
/// a per-tenant query filter to every <see cref="IMustHaveTenant"/> entity
/// registered by any module.
/// </summary>
public class SunfishBridgeDbContext : DbContext
{
    private readonly IEnumerable<ISunfishEntityModule> _modules;
    private readonly string _currentTenantId;

    public SunfishBridgeDbContext(
        DbContextOptions<SunfishBridgeDbContext> options,
        IEnumerable<ISunfishEntityModule> modules,
        Sunfish.Foundation.Authorization.ITenantContext tenant)
        : base(options)
    {
        _modules = modules;
        _currentTenantId = tenant.TenantId;
    }

    // Control-plane DbSet (ADR 0031 Wave 5.1). Tenant registration is the only
    // authoritative concern Bridge's shared control plane owns — everything else
    // here is legacy team data waiting to move to the per-tenant data plane.
    public DbSet<TenantRegistration> TenantRegistrations => Set<TenantRegistration>();

    // Legacy PM-domain DbSets — per ADR 0031 these represent "team data" that
    // belongs in the per-tenant data plane (Wave 5.2 pending). The DbSet
    // properties are preserved so BridgeSeeder + the demo shell keep rendering
    // during the refactor; new code must not depend on these.
#pragma warning disable CS0618 // legacy DbSet exposure; see note above
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
#pragma warning restore CS0618

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Control-plane entity configuration (ADR 0031).
        ConfigureTenantRegistration(modelBuilder);

#pragma warning disable CS0618 // legacy PM-domain mappings; see DbSet block above
        // Legacy PM-domain table name mappings. These move to blocks-* with each
        // phase of the bridge-data-audit move plan; module-contributed entities
        // supply their own ToTable() calls.
        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<ProjectMember>().ToTable("project_members");
        modelBuilder.Entity<TaskItem>().ToTable("tasks");
        modelBuilder.Entity<Subtask>().ToTable("subtasks");
        modelBuilder.Entity<Comment>().ToTable("comments");
        modelBuilder.Entity<Milestone>().ToTable("milestones");
        modelBuilder.Entity<Risk>().ToTable("risks");
        modelBuilder.Entity<BudgetLine>().ToTable("budget_lines");
        modelBuilder.Entity<AuditRecord>().ToTable("audit_records");

        // Legacy per-entity tenant filters (string TenantId). Preserved until
        // these entities move to blocks-* and adopt IMustHaveTenant.
        modelBuilder.Entity<Project>().HasQueryFilter(e => e.TenantId == _currentTenantId);
        modelBuilder.Entity<TaskItem>().HasQueryFilter(e => e.TenantId == _currentTenantId);
        modelBuilder.Entity<AuditRecord>().HasQueryFilter(e => e.TenantId == _currentTenantId);
#pragma warning restore CS0618

        // Compose every registered module's entity configurations (ADR 0015).
        foreach (var module in _modules)
        {
            module.Configure(modelBuilder);
        }

        // Apply tenant query filter to every IMustHaveTenant entity contributed
        // by any module. EF Core parameterizes the captured _currentTenantId
        // field per DbContext instance, so each scope sees only its tenant's rows.
        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>Configures the <see cref="TenantRegistration"/> entity — the only
    /// authoritative record Bridge's shared control plane owns (ADR 0031 Wave 5.1).
    /// Explicitly NOT tenant-filtered: control-plane queries need to see every tenant
    /// row for admin/billing/support operations.</summary>
    private static void ConfigureTenantRegistration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRegistration>(e =>
        {
            e.ToTable("tenant_registrations");
            e.HasKey(t => t.TenantId);

            e.Property(t => t.Slug).HasMaxLength(64).IsRequired();
            e.Property(t => t.DisplayName).HasMaxLength(256).IsRequired();
            e.Property(t => t.Plan).HasMaxLength(32).IsRequired().HasDefaultValue("Free");
            e.Property(t => t.TeamPublicKey).HasColumnType("bytea");
            e.Property(t => t.TrustLevel).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.Property(t => t.CreatedAt);
            e.Property(t => t.UpdatedAt);

            // SupportContacts — serialized as a single jsonb column via a value converter.
            // EF Core preview.3 obsoleted OwnsMany().ToJson(); complex types are the new
            // guidance but require more migration work. A value converter over the list
            // keeps the shape addressable from LINQ projections without a separate table
            // until the support domain demands richer relational shape.
            var supportContactsConverter = new ValueConverter<List<SupportContact>, string>(
                v => JsonSerializer.Serialize(v, SupportContactJsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<SupportContact>()
                    : JsonSerializer.Deserialize<List<SupportContact>>(v, SupportContactJsonOptions) ?? new List<SupportContact>());
            var supportContactsComparer = new ValueComparer<List<SupportContact>>(
                (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
                v => v == null ? 0 : v.Aggregate(0, (h, c) => HashCode.Combine(h, c.Name, c.Email, c.Role)),
                v => v.Select(c => new SupportContact { Name = c.Name, Email = c.Email, Role = c.Role }).ToList());

            e.Property(t => t.SupportContacts)
                .HasColumnType("jsonb")
                .HasConversion(supportContactsConverter, supportContactsComparer);

            // Slug must be unique across all tenants (subdomain routing depends on it).
            e.HasIndex(t => t.Slug).IsUnique();
        });
    }

    private static readonly JsonSerializerOptions SupportContactJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (!typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // Build: (T e) => e.TenantId.Value == _currentTenantId
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(IMustHaveTenant.TenantId));
            var tenantIdValue = Expression.Property(tenantIdProperty, "Value");
            var currentTenantRef = Expression.Field(
                Expression.Constant(this),
                nameof(_currentTenantId));
            var equal = Expression.Equal(tenantIdValue, currentTenantRef);
            var lambda = Expression.Lambda(equal, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
