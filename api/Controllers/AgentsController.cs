using Maestro.Api.Models.Dtos;
using Maestro.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Maestro.Api.Controllers;

[ApiController]
public sealed class AgentsController(
    WorkspaceStore store,
    AgentProcessService agents,
    ILogger<AgentsController> logger) : ControllerBase
{
    [HttpPost("api/workspaces/{workspaceId}/agents")]
    public ActionResult<AgentDto> Start(string workspaceId, [FromBody] CreateAgentRequest req)
    {
        var ws = store.GetWorkspace(workspaceId);
        if (ws is null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.InitialPrompt))
        {
            return BadRequest(new { error = "initialPrompt is required." });
        }
        try
        {
            var agent = agents.Start(ws, req);
            return AgentDto.From(agent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start agent");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("api/workspaces/{workspaceId}/agents/{agentId}/messages")]
    public ActionResult Send(string workspaceId, string agentId, [FromBody] SendMessageRequest req)
    {
        if (string.IsNullOrEmpty(req.Content)) return BadRequest(new { error = "content is required." });
        var ws = store.GetWorkspace(workspaceId);
        if (ws is null) return NotFound();
        var ok = agents.TrySendInput(agentId, req.Content);
        return ok ? NoContent() : NotFound(new { error = "Agent is not running." });
    }

    [HttpDelete("api/workspaces/{workspaceId}/agents/{agentId}")]
    public ActionResult Stop(string workspaceId, string agentId)
    {
        var ws = store.GetWorkspace(workspaceId);
        if (ws is null) return NotFound();
        var ok = agents.Stop(agentId);
        return ok ? NoContent() : NotFound(new { error = "Agent is not running." });
    }

    [HttpPost("api/agents/stop-all")]
    public ActionResult<object> StopAll()
    {
        var killed = agents.StopAll();
        logger.LogInformation("Kill switch fired: stopped {Count} agent(s)", killed);
        return Ok(new { stopped = killed });
    }
}
