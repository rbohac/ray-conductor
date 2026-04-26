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

        // Phase 3 will hook agent process termination here. For Phase 2, no
        // running processes exist, so we can go straight to worktree removal.
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
