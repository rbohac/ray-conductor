using System.Diagnostics;
using Maestro.Api.Models.Domain;

namespace Maestro.Api.Services;

/// <summary>
/// Probes the third-party CLIs Maestro shells out to (git, claude, codex)
/// once at app startup and caches the results into a <see cref="SystemHealth"/>
/// singleton so the SPA can render a banner without re-spawning processes
/// on every request.
/// </summary>
public sealed class HealthCheckService(
    SystemHealth health,
    GitService git,
    IConfiguration config,
    ILogger<HealthCheckService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var ts = DateTimeOffset.UtcNow;

        // git --version
        try
        {
            var r = await git.VersionAsync(cancellationToken);
            health.Git = r.Success;
            health.GitVersion = r.StdOut.Trim();
            if (!r.Success)
            {
                health.Errors.Add($"git --version failed (exit {r.ExitCode}): {r.StdErr.Trim()}");
            }
        }
        catch (Exception ex)
        {
            health.Git = false;
            health.Errors.Add($"git not found on PATH: {ex.Message}");
        }

        // claude --version
        var claudeCmd = config["Maestro:ClaudeCommand"] ?? "claude";
        var (cOk, cVer, cErr) = await TryVersion(claudeCmd, cancellationToken);
        health.Claude = cOk;
        health.ClaudeVersion = cVer;
        if (!cOk) health.Errors.Add($"{claudeCmd} --version failed: {cErr}");

        // codex --version
        var codexCmd = config["Maestro:CodexCommand"] ?? "codex";
        var (xOk, xVer, xErr) = await TryVersion(codexCmd, cancellationToken);
        health.Codex = xOk;
        health.CodexVersion = xVer;
        if (!xOk) health.Errors.Add($"{codexCmd} --version failed: {xErr}");

        health.CheckedAt = ts;
        logger.LogInformation(
            "Health check: git={Git} claude={Claude} codex={Codex}",
            health.Git, health.Claude, health.Codex);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<(bool ok, string? version, string err)> TryVersion(string command, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = CommandResolver.Resolve(command),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            psi.ArgumentList.Add("--version");

            using var proc = Process.Start(psi);
            if (proc is null) return (false, null, "process did not start");
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0)
            {
                return (false, null, string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim());
            }
            return (true, stdout.Trim(), string.Empty);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
