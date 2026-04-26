namespace Maestro.Api.Services;

/// <summary>
/// Resolves a bare command name against <c>PATH</c> + <c>PATHEXT</c> on
/// Windows so that npm-installed shims like <c>claude.cmd</c> /
/// <c>codex.cmd</c> are found by <see cref="System.Diagnostics.Process"/>,
/// which otherwise only auto-appends <c>.exe</c> when launched with
/// <c>UseShellExecute=false</c>.
/// </summary>
internal static class CommandResolver
{
    public static string Resolve(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return command;
        if (!OperatingSystem.IsWindows()) return command;

        // Absolute or relative path with separators — trust as-is.
        if (command.Contains(Path.DirectorySeparatorChar)
            || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return command;
        }

        // Already has an extension — trust as-is (e.g. user passed claude.cmd).
        if (Path.HasExtension(command))
        {
            return command;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathExtEnv = Environment.GetEnvironmentVariable("PATHEXT")
            ?? ".COM;.EXE;.BAT;.CMD";

        var extensions = pathExtEnv
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .ToArray();
        if (extensions.Length == 0)
        {
            extensions = [".EXE", ".CMD", ".BAT"];
        }

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim().Trim('"');
            if (string.IsNullOrEmpty(trimmed)) continue;
            foreach (var ext in extensions)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(trimmed, command + ext);
                }
                catch
                {
                    continue;
                }
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return command;
    }
}
