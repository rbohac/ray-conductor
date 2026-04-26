using Maestro.Api.Models.Domain;
using Maestro.Api.Models.Dtos;
using Maestro.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maestro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WorkspacesController(
    WorkspaceStore store,
    WorktreeService worktrees,
    AgentProcessService agents,
    DiffService diffs,
    GitService git,
    ILogger<WorkspacesController> logger) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<WorkspaceDto>> List()
        => Ok(store.ListWorkspaces().Select(WorkspaceDto.From));

    [HttpGet("{id}")]
    public ActionResult<WorkspaceDto> Get(string id)
    {
        var ws = store.GetWorkspace(id);
        return ws is null ? NotFound() : Ok(WorkspaceDto.From(ws));
    }

    [HttpGet("{id}/diff")]
    public async Task<ActionResult<DiffResult>> GetDiff(string id, CancellationToken ct)
    {
        var ws = store.GetWorkspace(id);
        if (ws is null) return NotFound();
        var repo = store.GetRepo(ws.RepoId);
        if (repo is null) return NotFound(new { error = "Workspace repo missing." });
        var result = await diffs.GetAsync(repo.Path, ws.BaseBranch, ws.BranchName, ct);
        return Ok(result);
    }

    [HttpPost("{id}/merge")]
    public async Task<ActionResult> Merge(string id, CancellationToken ct)
    {
        var ws = store.GetWorkspace(id);
        if (ws is null) return NotFound();
        var repo = store.GetRepo(ws.RepoId);
        if (repo is null) return NotFound(new { error = "Workspace repo missing." });
        if (ws.Status == Maestro.Api.Models.Domain.WorkspaceStatus.Completed)
        {
            return Conflict(new { error = "Workspace already merged." });
        }

        // Checkout the base in the source repo's main checkout. Fails if the
        // user has uncommitted changes there or if some other worktree owns
        // the base branch — surface git's stderr so the user can act on it.
        var checkout = await git.CheckoutAsync(repo.Path, ws.BaseBranch, ct);
        if (!checkout.Success)
        {
            return BadRequest(new
            {
                error = $"Could not checkout {ws.BaseBranch}: {checkout.StdErr.Trim()}",
            });
        }

        var merge = await git.MergeNoFfAsync(repo.Path, ws.BranchName, ct);
        if (!merge.Success)
        {
            var conflictResult = await git.RunAsync(
                repo.Path,
                ["diff", "--name-only", "--diff-filter=U"],
                ct);
            var conflicts = conflictResult.StdOut
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();
            await git.RunAsync(repo.Path, ["merge", "--abort"], ct);
            logger.LogWarning("Merge {Branch} into {Base} had conflicts: {Files}",
                ws.BranchName, ws.BaseBranch, string.Join(',', conflicts));
            return Conflict(new
            {
                error = $"Merge had conflicts in {conflicts.Length} file(s). Aborted.",
                conflicts,
            });
        }

        store.MutateWorkspace(id, w => w.Status = Maestro.Api.Models.Domain.WorkspaceStatus.Completed);
        logger.LogInformation("Merged workspace {Id}: {Branch} -> {Base}", id, ws.BranchName, ws.BaseBranch);
        return Ok(new { ok = true });
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> Create(
        [FromBody] CreateWorkspaceRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RepoId)
            || string.IsNullOrWhiteSpace(req.Name)
            || string.IsNullOrWhiteSpace(req.BranchName)
            || string.IsNullOrWhiteSpace(req.BaseBranch))
        {
            return BadRequest(new { error = "repoId, name, branchName, baseBranch are all required." });
        }

        var repo = store.GetRepo(req.RepoId);
        if (repo is null) return BadRequest(new { error = "Unknown repoId." });

        var id = Guid.NewGuid().ToString("N");
        var workspace = new Workspace
        {
            Id = id,
            RepoId = repo.Id,
            Name = req.Name.Trim(),
            BranchName = req.BranchName.Trim(),
            BaseBranch = req.BaseBranch.Trim(),
            WorktreePath = worktrees.ResolveWorktreePath(repo, id),
            Status = WorkspaceStatus.Idle,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            await worktrees.CreateAsync(repo, workspace, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create worktree for workspace {Id}", id);
            return BadRequest(new { error = ex.Message });
        }

        store.AddWorkspace(workspace);
        logger.LogInformation("Created workspace {Id} ({Name}) on branch {Branch}", id, workspace.Name, workspace.BranchName);
        return CreatedAtAction(nameof(Get), new { id }, WorkspaceDto.From(workspace));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(
        string id,
        [FromQuery] bool deleteBranch = true,
        CancellationToken ct = default)
    {
        var ws = store.GetWorkspace(id);
        if (ws is null) return NotFound();

        var repo = store.GetRepo(ws.RepoId);
        if (repo is null)
        {
            logger.LogWarning("Workspace {Id} references missing repo {RepoId}; removing record only", id, ws.RepoId);
            store.RemoveWorkspace(id);
            return NoContent();
        }

        // Kill any agent processes still attached to this workspace before
        // we yank the worktree out from under them.
        foreach (var ag in ws.Agents.Where(a => a.Status is Maestro.Api.Models.Domain.AgentStatus.Running or Maestro.Api.Models.Domain.AgentStatus.Starting).ToArray())
        {
            agents.Stop(ag.Id);
        }

        try
        {
            await worktrees.RemoveAsync(repo, ws, deleteBranch, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Worktree removal failed for {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }

        store.RemoveWorkspace(id);
        logger.LogInformation("Deleted workspace {Id}", id);
        return NoContent();
    }
}
