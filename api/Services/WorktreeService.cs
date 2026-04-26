using Maestro.Api.Models.Domain;

namespace Maestro.Api.Services;

public sealed class WorktreeService(GitService git, ILogger<WorktreeService> logger)
{
    public string ResolveWorktreePath(Repo repo, string workspaceId)
        => Path.Combine(repo.Path, ".maestro-worktrees", workspaceId);

    public async Task CreateAsync(Repo repo, Workspace workspace, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.Combine(repo.Path, ".maestro-worktrees"));
        var result = await git.WorktreeAddAsync(repo.Path, workspace.WorktreePath, workspace.BranchName, workspace.BaseBranch, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"git worktree add failed (exit {result.ExitCode}): {result.StdErr.Trim()}");
        }
        logger.LogInformation("Created worktree at {Path} on branch {Branch}", workspace.WorktreePath, workspace.BranchName);
    }

    public async Task RemoveAsync(Repo repo, Workspace workspace, bool deleteBranch, CancellationToken ct = default)
    {
        if (Directory.Exists(workspace.WorktreePath))
        {
            var remove = await git.WorktreeRemoveAsync(repo.Path, workspace.WorktreePath, force: true, ct).ConfigureAwait(false);
            if (!remove.Success)
            {
                logger.LogWarning("git worktree remove returned exit {Code}: {Err}", remove.ExitCode, remove.StdErr);
                // Best-effort directory cleanup if git couldn't.
                try
                {
                    Directory.Delete(workspace.WorktreePath, recursive: true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Manual cleanup of worktree dir failed at {Path}", workspace.WorktreePath);
                }
            }
        }

        if (deleteBranch)
        {
            var del = await git.BranchDeleteAsync(repo.Path, workspace.BranchName, force: true, ct).ConfigureAwait(false);
            if (!del.Success)
            {
                logger.LogWarning("git branch -D {Branch} returned exit {Code}: {Err}",
                    workspace.BranchName, del.ExitCode, del.StdErr);
            }
        }
    }
}
