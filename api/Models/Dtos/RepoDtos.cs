using Maestro.Api.Models.Domain;

namespace Maestro.Api.Models.Dtos;

public sealed class CreateRepoRequest
{
    public required string Path { get; set; }
    public string? DisplayName { get; set; }
}

public sealed class RepoDto
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string DisplayName { get; init; }
    public string? DefaultBranch { get; init; }
    public DateTimeOffset RegisteredAt { get; init; }

    public static RepoDto From(Repo r) => new()
    {
        Id = r.Id,
        Path = r.Path,
        DisplayName = r.DisplayName,
        DefaultBranch = r.DefaultBranch,
        RegisteredAt = r.RegisteredAt,
    };
}
