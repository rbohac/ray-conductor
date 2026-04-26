using System.Text.Json;
using System.Text.Json.Serialization;
using Maestro.Api.Models.Domain;

namespace Maestro.Api.Services;

public sealed class StateSnapshot
{
    public List<Repo> Repos { get; set; } = [];
    public List<Workspace> Workspaces { get; set; } = [];
}

/// <summary>
/// In-memory state for repos, workspaces, and agents, mirrored to a JSON file
/// in <c>%LOCALAPPDATA%\Maestro\state.json</c> after every mutation.
/// All public methods are safe for concurrent callers.
/// </summary>
public sealed class WorkspaceStore
{
    private readonly ILogger<WorkspaceStore> _logger;
    private readonly string _statePath;
    private readonly Lock _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private StateSnapshot _state = new();

    public WorkspaceStore(ILogger<WorkspaceStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Maestro");
        Directory.CreateDirectory(dir);
        _statePath = Path.Combine(dir, "state.json");
        Load();
    }

    public string StatePath => _statePath;

    public IReadOnlyList<Repo> ListRepos()
    {
        lock (_lock) return _state.Repos.ToArray();
    }

    public Repo? GetRepo(string id)
    {
        lock (_lock) return _state.Repos.FirstOrDefault(r => r.Id == id);
    }

    public void AddRepo(Repo repo)
    {
        lock (_lock)
        {
            _state.Repos.Add(repo);
            Persist();
        }
    }

    public bool RemoveRepo(string id)
    {
        lock (_lock)
        {
            var repo = _state.Repos.FirstOrDefault(r => r.Id == id);
            if (repo is null) return false;
            _state.Repos.Remove(repo);
            Persist();
            return true;
        }
    }

    public IReadOnlyList<Workspace> ListWorkspaces()
    {
        lock (_lock) return _state.Workspaces.ToArray();
    }

    public IReadOnlyList<Workspace> ListWorkspacesForRepo(string repoId)
    {
        lock (_lock) return _state.Workspaces.Where(w => w.RepoId == repoId).ToArray();
    }

    public Workspace? GetWorkspace(string id)
    {
        lock (_lock) return _state.Workspaces.FirstOrDefault(w => w.Id == id);
    }

    public void AddWorkspace(Workspace workspace)
    {
        lock (_lock)
        {
            _state.Workspaces.Add(workspace);
            Persist();
        }
    }

    public bool RemoveWorkspace(string id)
    {
        lock (_lock)
        {
            var ws = _state.Workspaces.FirstOrDefault(w => w.Id == id);
            if (ws is null) return false;
            _state.Workspaces.Remove(ws);
            Persist();
            return true;
        }
    }

    public void Mutate(Action<StateSnapshot> mutator)
    {
        lock (_lock)
        {
            mutator(_state);
            Persist();
        }
    }

    public void MutateWorkspace(string id, Action<Workspace> mutator)
    {
        lock (_lock)
        {
            var ws = _state.Workspaces.FirstOrDefault(w => w.Id == id);
            if (ws is null) return;
            mutator(ws);
            Persist();
        }
    }

    private void Load()
    {
        if (!File.Exists(_statePath))
        {
            _logger.LogInformation("No state file at {Path}; starting fresh", _statePath);
            return;
        }
        try
        {
            var json = File.ReadAllText(_statePath);
            var loaded = JsonSerializer.Deserialize<StateSnapshot>(json, _jsonOptions);
            if (loaded is not null)
            {
                _state = loaded;
                // Any agents that were running when the previous process died are
                // gone; mark them orphaned so the UI doesn't show stale spinners.
                foreach (var ws in _state.Workspaces)
                {
                    foreach (var ag in ws.Agents)
                    {
                        if (ag.Status is AgentStatus.Starting or AgentStatus.Running)
                        {
                            ag.Status = AgentStatus.Orphaned;
                            ag.EndedAt ??= DateTimeOffset.UtcNow;
                        }
                    }
                    if (ws.Status == WorkspaceStatus.Running)
                    {
                        ws.Status = WorkspaceStatus.Idle;
                    }
                }
                _logger.LogInformation("Loaded state from {Path}: {Repos} repos, {Workspaces} workspaces",
                    _statePath, _state.Repos.Count, _state.Workspaces.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load state from {Path}; starting fresh", _statePath);
            _state = new StateSnapshot();
        }
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(_state, _jsonOptions);
            var tmp = _statePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist state to {Path}", _statePath);
        }
    }
}
