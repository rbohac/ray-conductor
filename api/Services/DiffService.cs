using System.Text.RegularExpressions;
using Maestro.Api.Models.Dtos;

namespace Maestro.Api.Services;

public sealed partial class DiffService(GitService git)
{
    public async Task<DiffResult> GetAsync(string repoPath, string baseRef, string headRef, CancellationToken ct)
    {
        var result = await git.DiffAsync(repoPath, baseRef, headRef, ct).ConfigureAwait(false);
        if (!result.Success && !string.IsNullOrEmpty(result.StdErr))
        {
            // git-diff against missing refs returns non-zero with empty stdout
            // — let the caller see the empty diff rather than 500.
        }
        return new DiffResult
        {
            BaseBranch = baseRef,
            HeadBranch = headRef,
            Files = Parse(result.StdOut),
        };
    }

    [GeneratedRegex(@"^@@ -(?<oldStart>\d+)(?:,(?<oldLines>\d+))? \+(?<newStart>\d+)(?:,(?<newLines>\d+))? @@(?<rest>.*)$")]
    private static partial Regex HunkHeaderRegex();

    [GeneratedRegex(@"^diff --git a/(?<a>.+?) b/(?<b>.+)$")]
    private static partial Regex FileHeaderRegex();

    /// <summary>
    /// Parses the unified diff produced by <c>git diff base...head</c> into
    /// a list of structured per-file diffs.
    /// </summary>
    private static List<DiffFile> Parse(string raw)
    {
        var files = new List<DiffFile>();
        if (string.IsNullOrWhiteSpace(raw)) return files;

        DiffFile? current = null;
        DiffHunk? hunk = null;
        var hunkBody = new System.Text.StringBuilder();

        void flushHunk()
        {
            if (hunk is null) return;
            hunk.Body = hunkBody.ToString().TrimEnd('\n');
            current!.Hunks.Add(hunk);
            hunkBody.Clear();
            hunk = null;
        }

        foreach (var rawLine in raw.Split('\n'))
        {
            // Don't trim — leading/trailing whitespace is meaningful inside hunks.
            var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            var fileMatch = FileHeaderRegex().Match(line);
            if (fileMatch.Success)
            {
                flushHunk();
                if (current is not null) files.Add(current);
                current = new DiffFile
                {
                    Path = fileMatch.Groups["b"].Value,
                    OldPath = fileMatch.Groups["a"].Value,
                    Status = DiffFileStatus.Modified,
                };
                continue;
            }

            if (current is null) continue;

            if (line.StartsWith("new file mode", StringComparison.Ordinal))
            {
                current.Status = DiffFileStatus.Added;
                current.OldPath = null;
                continue;
            }
            if (line.StartsWith("deleted file mode", StringComparison.Ordinal))
            {
                current.Status = DiffFileStatus.Deleted;
                continue;
            }
            if (line.StartsWith("rename from ", StringComparison.Ordinal))
            {
                current.Status = DiffFileStatus.Renamed;
                current.OldPath = line["rename from ".Length..];
                continue;
            }
            if (line.StartsWith("rename to ", StringComparison.Ordinal))
            {
                current.Path = line["rename to ".Length..];
                continue;
            }

            var hunkMatch = HunkHeaderRegex().Match(line);
            if (hunkMatch.Success)
            {
                flushHunk();
                hunk = new DiffHunk
                {
                    Header = line,
                    OldStart = int.Parse(hunkMatch.Groups["oldStart"].Value),
                    OldLines = hunkMatch.Groups["oldLines"].Success
                        ? int.Parse(hunkMatch.Groups["oldLines"].Value)
                        : 1,
                    NewStart = int.Parse(hunkMatch.Groups["newStart"].Value),
                    NewLines = hunkMatch.Groups["newLines"].Success
                        ? int.Parse(hunkMatch.Groups["newLines"].Value)
                        : 1,
                    Body = string.Empty,
                };
                continue;
            }

            if (hunk is null) continue;

            // Skip the no-newline marker but otherwise capture body lines.
            if (line.StartsWith("\\ ", StringComparison.Ordinal))
            {
                hunkBody.Append(line).Append('\n');
                continue;
            }
            if (line.Length == 0)
            {
                hunkBody.Append('\n');
                continue;
            }

            var first = line[0];
            if (first == '+')
            {
                current.Additions++;
            }
            else if (first == '-')
            {
                current.Deletions++;
            }
            // ' ', '+', '-' all become part of the hunk body
            hunkBody.Append(line).Append('\n');
        }

        flushHunk();
        if (current is not null) files.Add(current);

        return files;
    }
}
