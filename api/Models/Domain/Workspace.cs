namespace Maestro.Api.Models.Domain;

public enum WorkspaceStatus
{
    Idle,
    Running,
    Completed,
    Errored,
}

public sealed class Workspace
{
    public required string Id { get; init; }
    public required string RepoId { get; set; }
    public required string Name { get; set; }
    public required string BranchName { get; set; }
    public required string BaseBranch { get; set; }
    public required string WorktreePath { get; set; }
    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Idle;
    public DateTimeOffset CreatedAt { get; init; }
    public List<Agent> Agents { get; set; } = [];
}
