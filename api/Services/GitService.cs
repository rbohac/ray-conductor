using System.Diagnostics;
using System.Text;

namespace Maestro.Api.Services;

public sealed record GitResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Thin wrapper around the system <c>git</c> executable. All git interactions
/// in Maestro go through this service so they are easy to mock or trace.
/// </summary>
public sealed class GitService(ILogger<GitService> logger)
{
    public async Task<GitResult> RunAsync(
        string? workingDirectory,
        IEnumerable<string> args,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        logger.LogDebug("git {Args} (cwd={Cwd})", string.Join(' ', psi.ArgumentList), workingDirectory ?? "<inherit>");

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        return new GitResult(proc.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }

    public async Task<bool> IsGitRepoAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }
        var result = await RunAsync(path, ["rev-parse", "--is-inside-work-tree"], ct).ConfigureAwait(false);
        return result.Success && result.StdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetDefaultBranchAsync(string path, CancellationToken ct = default)
    {
        // Try origin/HEAD first.
        var origin = await RunAsync(path, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], ct).ConfigureAwait(false);
        if (origin.Success && !string.IsNullOrWhiteSpace(origin.StdOut))
        {
            var name = origin.StdOut.Trim();
            const string prefix = "origin/";
            return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
        }
        // Fall back to current branch.
        var head = await RunAsync(path, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return head.Success ? head.StdOut.Trim() : null;
    }

    public Task<GitResult> WorktreeAddAsync(string repoPath, string worktreePath, string newBranch, string baseBranch, CancellationToken ct = default)
        => RunAsync(repoPath, ["worktree", "add", "-b", newBranch, worktreePath, baseBranch], ct);

    public Task<GitResult> WorktreeRemoveAsync(string repoPath, string worktreePath, bool force, CancellationToken ct = default)
    {
        var args = new List<string> { "worktree", "remove" };
        if (force) args.Add("--force");
        args.Add(worktreePath);
        return RunAsync(repoPath, args, ct);
    }

    public Task<GitResult> BranchDeleteAsync(string repoPath, string branch, bool force, CancellationToken ct = default)
        => RunAsync(repoPath, ["branch", force ? "-D" : "-d", branch], ct);

    public Task<GitResult> CheckoutAsync(string repoPath, string branch, CancellationToken ct = default)
        => RunAsync(repoPath, ["checkout", branch], ct);

    public Task<GitResult> MergeNoFfAsync(string repoPath, string branch, CancellationToken ct = default)
        => RunAsync(repoPath, ["merge", "--no-ff", branch], ct);

    public Task<GitResult> DiffAsync(string repoPath, string baseRef, string headRef, CancellationToken ct = default)
        => RunAsync(repoPath, ["diff", $"{baseRef}...{headRef}"], ct);

    public Task<GitResult> VersionAsync(CancellationToken ct = default)
        => RunAsync(null, ["--version"], ct);
}
