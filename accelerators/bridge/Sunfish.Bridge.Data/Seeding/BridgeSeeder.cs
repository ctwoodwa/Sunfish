using Sunfish.Bridge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sunfish.Bridge.Data.Seeding;

public sealed class BridgeSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BridgeSeeder> _logger;

    public BridgeSeeder(IServiceProvider services, ILogger<BridgeSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SunfishBridgeDbContext>();

        // Schema migrations are owned by Sunfish.Bridge.MigrationService — the seeder
        // assumes the schema already exists. It only inserts demo data.
        if (await db.Projects.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Bridge data already present; skipping seed.");
            return;
        }

        const string tenant = "demo-tenant";

        var project = new Project
        {
            TenantId = tenant,
            Name = "Apollo Website Redesign",
            Description = "Modernize the Acme corporate site with a new design system and CMS.",
            Status = ProjectStatus.Active,
        };
        db.Projects.Add(project);

        // 5 team members
        string[] members = ["alex.kim", "blair.osei", "casey.nguyen", "drew.patel", "ellis.romero"];
        string[] roles = [Authorization.Roles.Manager, Authorization.Roles.TeamMember, Authorization.Roles.TeamMember, Authorization.Roles.TeamMember, Authorization.Roles.Viewer];
        for (var i = 0; i < members.Length; i++)
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = members[i],
                Role = roles[i],
            });
        }

        // 20 tasks across all 5 statuses (4 each)
        var statuses = Enum.GetValues<Entities.TaskStatus>();
        var rng = new Random(42);
        for (var i = 0; i < 20; i++)
        {
            db.Tasks.Add(new TaskItem
            {
                TenantId = tenant,
                ProjectId = project.Id,
                Title = $"Task {i + 1:00}",
                Status = statuses[i % statuses.Length],
                Priority = (TaskPriority)rng.Next(0, 4),
                AssigneeId = members[rng.Next(members.Length)],
                DueDate = DateTime.UtcNow.AddDays(rng.Next(-5, 60)),
            });
        }

        // 3 milestones
        db.Milestones.AddRange(
            new Milestone { ProjectId = project.Id, Title = "Discovery Complete", DueDate = DateTime.UtcNow.AddDays(7), Status = MilestoneStatus.OnTrack },
            new Milestone { ProjectId = project.Id, Title = "Design System v1", DueDate = DateTime.UtcNow.AddDays(21), Status = MilestoneStatus.AtRisk },
            new Milestone { ProjectId = project.Id, Title = "Beta Launch", DueDate = DateTime.UtcNow.AddDays(-3), Status = MilestoneStatus.Late });

        // 8 risks
        var riskTitles = new[]
        {
            ("Vendor delivery slip", RiskCategory.Schedule, RiskLevel.High),
            ("Scope creep on CMS", RiskCategory.External, RiskLevel.Medium),
            ("Auth provider migration", RiskCategory.Technical, RiskLevel.Critical),
            ("Designer availability", RiskCategory.Resource, RiskLevel.Medium),
            ("Budget overrun on hosting", RiskCategory.Budget, RiskLevel.Low),
            ("Accessibility compliance", RiskCategory.Technical, RiskLevel.High),
            ("Content migration", RiskCategory.Schedule, RiskLevel.Medium),
            ("Stakeholder alignment", RiskCategory.External, RiskLevel.Low),
        };
        foreach (var (title, cat, sev) in riskTitles)
        {
            db.Risks.Add(new Risk
            {
                ProjectId = project.Id,
                Title = title,
                Category = cat,
                Probability = sev,
                Impact = sev,
                Severity = sev,
                OwnerId = members[rng.Next(members.Length)],
                Status = RiskStatus.Open,
            });
        }

        // Budget lines for 4 phases
        string[] phases = ["Discovery", "Design", "Build", "Launch"];
        foreach (var phase in phases)
        {
            db.BudgetLines.Add(new BudgetLine
            {
                ProjectId = project.Id,
                Category = "Labor",
                Phase = phase,
                Budgeted = 50_000m,
                Actual = 50_000m * (decimal)(0.8 + rng.NextDouble() * 0.5),
            });
        }

        // 15 audit records
        for (var i = 0; i < 15; i++)
        {
            db.AuditRecords.Add(new AuditRecord
            {
                TenantId = tenant,
                ActorId = members[rng.Next(members.Length)],
                ResourceType = "Task",
                ResourceId = Guid.NewGuid().ToString(),
                Action = i % 2 == 0 ? "StatusChanged" : "Updated",
                Timestamp = DateTime.UtcNow.AddHours(-i),
            });
        }

        // 10 comments spread across tasks
        var taskIds = db.Tasks.Local.Select(t => t.Id).ToList();
        for (var i = 0; i < 10; i++)
        {
            db.Comments.Add(new Comment
            {
                TaskId = taskIds[i % taskIds.Count],
                AuthorId = members[rng.Next(members.Length)],
                Body = $"Comment {i + 1}: looks good, will follow up.",
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Bridge seed complete: 1 project, 20 tasks, 3 milestones, 8 risks, 4 budget lines, 15 audits, 10 comments.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
