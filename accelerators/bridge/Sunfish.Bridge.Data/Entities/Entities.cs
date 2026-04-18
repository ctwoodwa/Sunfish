namespace Sunfish.Bridge.Data.Entities;

public enum ProjectStatus { Planning, Active, OnHold, Completed, Cancelled }
public enum TaskStatus { Backlog, ToDo, InProgress, InReview, Done }
public enum TaskPriority { Low, Medium, High, Critical }
public enum MilestoneStatus { OnTrack, AtRisk, Late, Done }
public enum RiskCategory { Technical, Schedule, Budget, Resource, External }
public enum RiskLevel { Low, Medium, High, Critical }
public enum RiskStatus { Open, Mitigating, Closed }

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

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
}

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

public class Subtask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentTaskId { get; set; }
    public string Title { get; set; } = "";
    public bool IsComplete { get; set; }
}

public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Milestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public DateTime DueDate { get; set; }
    public MilestoneStatus Status { get; set; }
}

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

public class BudgetLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Category { get; set; } = "";
    public string Phase { get; set; } = "";
    public decimal Budgeted { get; set; }
    public decimal Actual { get; set; }
}

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
