using Maestro.Api.Models.Domain;

namespace Maestro.Api.Models.Dtos;

public sealed class CreateWorkspaceRequest
{
    public required string RepoId { get; set; }
    public required string Name { get; set; }
    public required string BranchName { get; set; }
    public required string BaseBranch { get; set; }
}

public sealed class WorkspaceDto
{
    public required string Id { get; init; }
    public required string RepoId { get; init; }
    public required string Name { get; init; }
    public required string BranchName { get; init; }
    public required string BaseBranch { get; init; }
    public required string WorktreePath { get; init; }
    public required WorkspaceStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<AgentDto> Agents { get; init; }

    public static WorkspaceDto From(Workspace w) => new()
    {
        Id = w.Id,
        RepoId = w.RepoId,
        Name = w.Name,
        BranchName = w.BranchName,
        BaseBranch = w.BaseBranch,
        WorktreePath = w.WorktreePath,
        Status = w.Status,
        CreatedAt = w.CreatedAt,
        Agents = w.Agents.Select(AgentDto.From).ToArray(),
    };
}

public sealed class AgentDto
{
    public required string Id { get; init; }
    public required AgentProvider Provider { get; init; }
    public string? Model { get; init; }
    public required string InitialPrompt { get; init; }
    public required AgentStatus Status { get; init; }
    public int? ProcessId { get; init; }
    public int? ExitCode { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }

    public static AgentDto From(Agent a) => new()
    {
        Id = a.Id,
        Provider = a.Provider,
        Model = a.Model,
        InitialPrompt = a.InitialPrompt,
        Status = a.Status,
        ProcessId = a.ProcessId,
        ExitCode = a.ExitCode,
        StartedAt = a.StartedAt,
        EndedAt = a.EndedAt,
    };
}
