using Maestro.Api.Models.Domain;

namespace Maestro.Api.Models.Dtos;

public sealed class CreateAgentRequest
{
    public required AgentProvider Provider { get; set; }
    public string? Model { get; set; }
    public required string InitialPrompt { get; set; }
    public string? AutonomyLevel { get; set; }
}

public sealed class SendMessageRequest
{
    public required string Content { get; set; }
}
