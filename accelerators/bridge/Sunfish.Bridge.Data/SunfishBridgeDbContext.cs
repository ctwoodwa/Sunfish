using Sunfish.Foundation.Authorization;
using Sunfish.Bridge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Sunfish.Bridge.Data;

public class SunfishBridgeDbContext : DbContext
{
    private readonly string _currentTenantId;

    public SunfishBridgeDbContext(DbContextOptions<SunfishBridgeDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _currentTenantId = tenant.TenantId;
    }

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

        // Lowercase table names so DAB (and any plain-SQL consumer) can reference
        // them without needing to quote case-preserving identifiers.
        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<ProjectMember>().ToTable("project_members");
        modelBuilder.Entity<TaskItem>().ToTable("tasks");
        modelBuilder.Entity<Subtask>().ToTable("subtasks");
        modelBuilder.Entity<Comment>().ToTable("comments");
        modelBuilder.Entity<Milestone>().ToTable("milestones");
        modelBuilder.Entity<Risk>().ToTable("risks");
        modelBuilder.Entity<BudgetLine>().ToTable("budget_lines");
        modelBuilder.Entity<AuditRecord>().ToTable("audit_records");

        modelBuilder.Entity<Project>().HasQueryFilter(e => e.TenantId == _currentTenantId);
        modelBuilder.Entity<TaskItem>().HasQueryFilter(e => e.TenantId == _currentTenantId);
        modelBuilder.Entity<AuditRecord>().HasQueryFilter(e => e.TenantId == _currentTenantId);
    }
}
