namespace Maestro.Api.Models.Dtos;

public enum DiffFileStatus
{
    Added,
    Modified,
    Deleted,
    Renamed,
}

public sealed class DiffHunk
{
    public required string Header { get; set; }
    public required int OldStart { get; set; }
    public required int OldLines { get; set; }
    public required int NewStart { get; set; }
    public required int NewLines { get; set; }
    public required string Body { get; set; }
}

public sealed class DiffFile
{
    public required string Path { get; set; }
    public string? OldPath { get; set; }
    public DiffFileStatus Status { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public List<DiffHunk> Hunks { get; set; } = [];
}

public sealed class DiffResult
{
    public required string BaseBranch { get; init; }
    public required string HeadBranch { get; init; }
    public required IReadOnlyList<DiffFile> Files { get; init; }
}
