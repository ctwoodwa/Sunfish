namespace Sunfish.Bridge.Data.Entities;

// ADR 0031 Wave 5.1 — the entity types in this file are authoritative "team data"
// concerns (Projects, Tasks, Risks, etc.). Per ADR 0031, team data belongs in the
// per-tenant data plane (local-node-host, Wave 5.2 pending), NOT in Bridge's shared
// control plane. These types are marked [Obsolete] as a compile-time signal: they
// will be removed from Sunfish.Bridge.Data once the data-plane orchestration lands.
//
// The types are intentionally NOT deleted yet because:
//   1. BridgeSeeder + the demo shell still render this data for the accelerator's
//      local-run experience during the refactor (ADR-acknowledged scaffolding).
//   2. The existing migrations reference these tables; deleting the types would
//      force a disruptive schema drop.
//
// EF Core tolerates [Obsolete] on entity types — the DbContext still compiles and
// the demo still runs. New code must not take fresh dependencies on these types.

#pragma warning disable CS0618 // allow [Obsolete] references within this legacy file
public enum ProjectStatus { Planning, Active, OnHold, Completed, Cancelled }
public enum TaskStatus { Backlog, ToDo, InProgress, InReview, Done }
public enum TaskPriority { Low, Medium, High, Critical }
public enum MilestoneStatus { OnTrack, AtRisk, Late, Done }
public enum RiskCategory { Technical, Schedule, Budget, Resource, External }
public enum RiskLevel { Low, Medium, High, Critical }
public enum RiskStatus { Open, Mitigating, Closed }

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ProjectStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "";
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public string? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class Subtask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentTaskId { get; set; }
    public string Title { get; set; } = "";
    public bool IsComplete { get; set; }
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class Milestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public DateTime DueDate { get; set; }
    public MilestoneStatus Status { get; set; }
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class Risk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public RiskCategory Category { get; set; }
    public RiskLevel Probability { get; set; }
    public RiskLevel Impact { get; set; }
    public RiskLevel Severity { get; set; }
    public string? OwnerId { get; set; }
    public RiskStatus Status { get; set; }
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class BudgetLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Category { get; set; } = "";
    public string Phase { get; set; } = "";
    public decimal Budgeted { get; set; }
    public decimal Actual { get; set; }
}

[Obsolete("Team data lives in the per-tenant data plane; see ADR 0031. Will be removed in Wave 5.2 or later.")]
public class AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
#pragma warning restore CS0618
