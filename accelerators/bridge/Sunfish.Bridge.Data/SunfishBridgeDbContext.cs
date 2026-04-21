using System.Linq.Expressions;
using Sunfish.Foundation.Authorization;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Persistence;
using Microsoft.EntityFrameworkCore;

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

    // Legacy PM-domain DbSets — these move to blocks-* per bridge-data-audit.md
    // phases M1–M4. Keep DbSet exposure for backwards compatibility until moves land.
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
