namespace Maestro.Api.Models.Domain;

public enum AgentProvider
{
    Claude,
    Codex,
}

public enum AgentStatus
{
    Starting,
    Running,
    Completed,
    Errored,
    Stopped,
    Orphaned,
}

public sealed class Agent
{
    public required string Id { get; init; }
    public required AgentProvider Provider { get; set; }
    public string? Model { get; set; }
    public required string InitialPrompt { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Starting;
    public int? ProcessId { get; set; }
    public int? ExitCode { get; set; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; set; }
}
