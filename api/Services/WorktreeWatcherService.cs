using System.Collections.Concurrent;

namespace Maestro.Api.Services;

/// <summary>
/// Singleton that owns one <see cref="FileSystemWatcher"/> per workspace
/// while that workspace has running agents. Filesystem events are debounced
/// (500 ms quiet window) and republished on the bus as
/// <c>worktree-changed</c> so the SPA can invalidate its diff query.
/// </summary>
public sealed class WorktreeWatcherService(EventBus bus, ILogger<WorktreeWatcherService> logger) : IDisposable
{
    private readonly ConcurrentDictionary<string, WatcherEntry> _watchers = new();

    public void StartWatching(string workspaceId, string worktreePath)
    {
        if (_watchers.ContainsKey(workspaceId)) return;
        if (!Directory.Exists(worktreePath))
        {
            logger.LogWarning("Cannot watch {Path}: directory does not exist", worktreePath);
            return;
        }

        FileSystemWatcher? fsw = null;
        try
        {
            fsw = new FileSystemWatcher(worktreePath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite
                    | NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
            };

            var entry = new WatcherEntry(workspaceId, worktreePath, fsw, bus, logger);
            fsw.Changed += entry.OnChanged;
            fsw.Created += entry.OnChanged;
            fsw.Deleted += entry.OnChanged;
            fsw.Renamed += entry.OnRenamed;
            fsw.Error += entry.OnError;
            fsw.EnableRaisingEvents = true;

            if (_watchers.TryAdd(workspaceId, entry))
            {
                logger.LogInformation("Watching worktree at {Path} for workspace {Id}", worktreePath, workspaceId);
            }
            else
            {
                entry.Dispose();
            }
            fsw = null; // ownership transferred to entry
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start watcher for {Path}", worktreePath);
            fsw?.Dispose();
        }
    }

    public void StopWatching(string workspaceId)
    {
        if (_watchers.TryRemove(workspaceId, out var entry))
        {
            entry.Dispose();
            logger.LogInformation("Stopped watching workspace {Id}", workspaceId);
        }
    }

    public void Dispose()
    {
        foreach (var e in _watchers.Values) e.Dispose();
        _watchers.Clear();
    }

    private sealed class WatcherEntry(
        string workspaceId,
        string worktreePath,
        FileSystemWatcher fsw,
        EventBus bus,
        ILogger logger) : IDisposable
    {
        private readonly Lock _lock = new();
        private Timer? _debounce;

        private void Handle(string fullPath)
        {
            string rel;
            try { rel = Path.GetRelativePath(worktreePath, fullPath); }
            catch { return; }

            // Skip git's bookkeeping and any nested maestro worktrees so we
            // never feed FSW into a loop with our own state writes.
            if (rel.StartsWith(".git", StringComparison.Ordinal)
                || rel.StartsWith(".maestro-worktrees", StringComparison.Ordinal)
                || rel.Equals(".git", StringComparison.Ordinal))
            {
                return;
            }

            lock (_lock)
            {
                _debounce?.Dispose();
                _debounce = new Timer(_ =>
                {
                    try
                    {
                        bus.Publish(workspaceId, new AgentEvent("worktree-changed", new
                        {
                            ts = DateTimeOffset.UtcNow,
                        }));
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Publish worktree-changed failed for {Id}", workspaceId);
                    }
                }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            }
        }

        public void OnChanged(object? sender, FileSystemEventArgs e) => Handle(e.FullPath);
        public void OnRenamed(object? sender, RenamedEventArgs e) => Handle(e.FullPath);
        public void OnError(object? sender, ErrorEventArgs e) =>
            logger.LogWarning(e.GetException(), "FileSystemWatcher error for {Id}", workspaceId);

        public void Dispose()
        {
            try { fsw.EnableRaisingEvents = false; } catch { /* ignore */ }
            fsw.Dispose();
            _debounce?.Dispose();
        }
    }
}
