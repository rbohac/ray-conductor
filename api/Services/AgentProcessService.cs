using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Maestro.Api.Models.Domain;
using Maestro.Api.Models.Dtos;

namespace Maestro.Api.Services;

/// <summary>
/// Spawns and manages Claude Code / Codex CLI processes inside workspace
/// worktrees. Each agent runs as a child process with stdio piped through the
/// event bus so the SPA can stream output via SSE.
/// </summary>
public sealed class AgentProcessService(
    WorkspaceStore store,
    EventBus bus,
    WorktreeWatcherService watcher,
    ILogger<AgentProcessService> logger,
    IConfiguration config) : IHostedService
{
    private readonly ConcurrentDictionary<string, RunningAgent> _running = new();

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // App is shutting down — kill every child process. Worktree-scoped
        // agents must not survive the API lifetime; reattaching is impossible
        // because we don't have a stable IPC channel. Use the same per-agent
        // Stop() so the Exited handler classifies these as `Stopped`.
        var ids = _running.Keys.ToArray();
        if (ids.Length == 0) return;
        logger.LogInformation("Shutting down: killing {Count} agent process(es)", ids.Length);
        foreach (var id in ids) Stop(id);
        await Task.CompletedTask;
    }

    public Agent Start(Workspace workspace, CreateAgentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.InitialPrompt))
        {
            throw new ArgumentException("Initial prompt is required.", nameof(req));
        }

        var (file, args) = BuildCommand(req);

        var psi = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = workspace.WorktreePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            // Force UTF-8 so em-dashes and other non-ASCII chars from the
            // child process don't render as mojibake on Windows consoles
            // whose default codepage is CP850/CP437.
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var agentId = Guid.NewGuid().ToString("N");
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            bus.Publish(workspace.Id, new AgentEvent("agent-output",
                new { agentId, stream = "stdout", line = e.Data, ts = DateTimeOffset.UtcNow }));
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            bus.Publish(workspace.Id, new AgentEvent("agent-output",
                new { agentId, stream = "stderr", line = e.Data, ts = DateTimeOffset.UtcNow }));
        };

        proc.Exited += (_, _) =>
        {
            // Stop()/StopAll() remove from _running BEFORE killing the
            // process, so a successful TryRemove here means the agent died
            // on its own. A failure means we asked it to die and the
            // non-zero exit code is just the SIGKILL artifact.
            var natural = _running.TryRemove(agentId, out _);
            var finalStatus = natural
                ? (proc.ExitCode == 0 ? AgentStatus.Completed : AgentStatus.Errored)
                : AgentStatus.Stopped;
            store.MutateWorkspace(workspace.Id, ws =>
            {
                var ag = ws.Agents.FirstOrDefault(a => a.Id == agentId);
                if (ag is null) return;
                ag.Status = finalStatus;
                ag.ExitCode = proc.ExitCode;
                ag.EndedAt = DateTimeOffset.UtcNow;
                if (ws.Agents.All(a => a.Status is not (AgentStatus.Running or AgentStatus.Starting))
                    && ws.Status == WorkspaceStatus.Running)
                {
                    ws.Status = ws.Agents.Any(a => a.Status == AgentStatus.Errored)
                        ? WorkspaceStatus.Errored
                        : WorkspaceStatus.Idle;
                }
            });
            // No more running agents in this workspace — stop watching so we
            // don't burn FS handles on idle worktrees.
            var ws = store.GetWorkspace(workspace.Id);
            if (ws is not null && !ws.Agents.Any(a => a.Status is AgentStatus.Running or AgentStatus.Starting))
            {
                watcher.StopWatching(workspace.Id);
            }
            bus.Publish(workspace.Id, new AgentEvent("agent-status",
                new { agentId, status = finalStatus.ToString().ToLowerInvariant(), exitCode = proc.ExitCode, ts = DateTimeOffset.UtcNow }));
            try { proc.Dispose(); } catch { /* ignore */ }
        };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to spawn {File} for agent {AgentId}", file, agentId);
            throw new InvalidOperationException($"Failed to launch {file}: {ex.Message}", ex);
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        var agent = new Agent
        {
            Id = agentId,
            Provider = req.Provider,
            Model = req.Model,
            InitialPrompt = req.InitialPrompt,
            Status = AgentStatus.Running,
            ProcessId = proc.Id,
            StartedAt = DateTimeOffset.UtcNow,
        };

        _running[agentId] = new RunningAgent(workspace.Id, agentId, proc);

        store.MutateWorkspace(workspace.Id, ws =>
        {
            ws.Agents.Add(agent);
            ws.Status = WorkspaceStatus.Running;
        });

        watcher.StartWatching(workspace.Id, workspace.WorktreePath);

        bus.Publish(workspace.Id, new AgentEvent("agent-status",
            new { agentId, status = "running", exitCode = (int?)null, ts = DateTimeOffset.UtcNow }));

        logger.LogInformation("Started {Provider} agent {AgentId} (pid {Pid}) in {Path}",
            req.Provider, agentId, proc.Id, workspace.WorktreePath);

        return agent;
    }

    public bool Stop(string agentId)
    {
        // Take ownership of the process before signalling — the Exited
        // handler reads this same dictionary to distinguish a clean exit
        // from an intentional kill.
        if (!_running.TryRemove(agentId, out var ra)) return false;
        try
        {
            KillProcessTree(ra.Process.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error killing agent {AgentId}", agentId);
        }
        // The Exited handler will publish the final agent-status event and
        // persist `Stopped`. We don't touch state here.
        return true;
    }

    public int StopAll()
    {
        var ids = _running.Keys.ToArray();
        foreach (var id in ids) Stop(id);
        return ids.Length;
    }

    public bool TrySendInput(string agentId, string content)
    {
        if (!_running.TryGetValue(agentId, out var ra)) return false;
        try
        {
            ra.Process.StandardInput.WriteLine(content);
            ra.Process.StandardInput.Flush();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write stdin for agent {AgentId}", agentId);
            return false;
        }
    }

    private (string file, IEnumerable<string> args) BuildCommand(CreateAgentRequest req)
    {
        // Both CLIs run with their auto-approve flags so we never need to
        // forward an interactive prompt through SSE. The worktree is the
        // isolation boundary for v1 — see PHASE 3 docs in progress.md.
        var rawFile = req.Provider switch
        {
            AgentProvider.Claude => config["Maestro:ClaudeCommand"] ?? "claude",
            AgentProvider.Codex => config["Maestro:CodexCommand"] ?? "codex",
            _ => throw new ArgumentException($"Unknown provider {req.Provider}", nameof(req)),
        };
        // On Windows, npm-installed CLIs land as `claude.cmd` / `codex.cmd`;
        // CreateProcess only auto-appends `.exe`, so resolve via PATHEXT.
        var file = CommandResolver.Resolve(rawFile);

        var args = new List<string>();
        switch (req.Provider)
        {
            case AgentProvider.Claude:
                args.Add("--dangerously-skip-permissions");
                if (!string.IsNullOrWhiteSpace(req.Model))
                {
                    args.Add("--model");
                    args.Add(req.Model);
                }
                args.Add(req.InitialPrompt);
                break;
            case AgentProvider.Codex:
                args.Add("--yolo");
                if (!string.IsNullOrWhiteSpace(req.Model))
                {
                    args.Add("--model");
                    args.Add(req.Model);
                }
                args.Add(req.InitialPrompt);
                break;
        }
        return (file, args);
    }

    private void KillProcessTree(int pid)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Use taskkill /T /F to ensure the entire child tree dies on
            // Windows — agents typically spawn helper processes (npm, git,
            // etc.) and we don't want zombies in Task Manager.
            var psi = new ProcessStartInfo("taskkill", $"/T /F /PID {pid}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        else
        {
            // Cross-platform fallback for development on Linux/macOS.
            try
            {
                using var p = Process.GetProcessById(pid);
                if (!p.HasExited) p.Kill(entireProcessTree: true);
            }
            catch (ArgumentException) { /* already gone */ }
            catch (InvalidOperationException) { /* already exited */ }
        }
    }

    private sealed record RunningAgent(string WorkspaceId, string AgentId, Process Process);
}
