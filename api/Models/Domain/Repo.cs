namespace Maestro.Api.Models.Domain;

public sealed class Repo
{
    public required string Id { get; init; }
    public required string Path { get; set; }
    public required string DisplayName { get; set; }
    public string? DefaultBranch { get; set; }
    public DateTimeOffset RegisteredAt { get; init; }
}
