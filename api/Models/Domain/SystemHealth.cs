namespace Maestro.Api.Models.Domain;

public sealed class SystemHealth
{
    public bool Git { get; set; }
    public string? GitVersion { get; set; }
    public bool Claude { get; set; }
    public string? ClaudeVersion { get; set; }
    public bool Codex { get; set; }
    public string? CodexVersion { get; set; }
    public List<string> Errors { get; } = [];
    public DateTimeOffset CheckedAt { get; set; }
}
