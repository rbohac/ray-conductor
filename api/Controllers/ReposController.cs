using Maestro.Api.Models.Domain;
using Maestro.Api.Models.Dtos;
using Maestro.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maestro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReposController(
    WorkspaceStore store,
    GitService git,
    ILogger<ReposController> logger) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<RepoDto>> List()
        => Ok(store.ListRepos().Select(RepoDto.From));

    [HttpPost]
    public async Task<ActionResult<RepoDto>> Create([FromBody] CreateRepoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(req.Path);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Invalid path: {ex.Message}" });
        }

        if (!Directory.Exists(fullPath))
        {
            return BadRequest(new { error = $"Path does not exist: {fullPath}" });
        }

        if (!await git.IsGitRepoAsync(fullPath, ct))
        {
            return BadRequest(new { error = $"Not a git repository: {fullPath}" });
        }

        if (store.ListRepos().Any(r => string.Equals(r.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { error = "That repo is already registered." });
        }

        var defaultBranch = await git.GetDefaultBranchAsync(fullPath, ct);
        var displayName = string.IsNullOrWhiteSpace(req.DisplayName)
            ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : req.DisplayName.Trim();

        var repo = new Repo
        {
            Id = Guid.NewGuid().ToString("N"),
            Path = fullPath,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? fullPath : displayName,
            DefaultBranch = defaultBranch,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

        store.AddRepo(repo);
        logger.LogInformation("Registered repo {Id} at {Path}", repo.Id, repo.Path);
        return CreatedAtAction(nameof(Get), new { id = repo.Id }, RepoDto.From(repo));
    }

    [HttpGet("{id}")]
    public ActionResult<RepoDto> Get(string id)
    {
        var repo = store.GetRepo(id);
        return repo is null ? NotFound() : Ok(RepoDto.From(repo));
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        var repo = store.GetRepo(id);
        if (repo is null) return NotFound();

        var workspaces = store.ListWorkspacesForRepo(id);
        if (workspaces.Count > 0)
        {
            return Conflict(new
            {
                error = $"Repo has {workspaces.Count} workspace(s). Delete those first.",
            });
        }

        store.RemoveRepo(id);
        logger.LogInformation("Unregistered repo {Id}", id);
        return NoContent();
    }
}
