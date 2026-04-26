import { useNavigate } from 'react-router-dom';
import { LayoutGrid, GitBranch, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { useWorkspaces } from '../context/useWorkspaces';
import { EmptyState } from '../components/EmptyState';
import { api, ApiError } from '../api/client';
import { StatusBadge } from '../components/StatusBadge';
import type { Workspace } from '../api/types';

interface WorkspacesPageProps {
  onNewWorkspace: () => void;
}

export function WorkspacesPage({ onNewWorkspace }: WorkspacesPageProps) {
  const { workspaces, repos, loading, refresh } = useWorkspaces();
  const navigate = useNavigate();

  function repoName(repoId: string) {
    return repos.find((r) => r.id === repoId)?.displayName ?? '(missing repo)';
  }

  async function remove(ws: Workspace, e: React.MouseEvent) {
    e.stopPropagation();
    if (!window.confirm(`Delete workspace "${ws.name}"? Worktree and branch will be removed.`)) return;
    try {
      await api.deleteWorkspace(ws.id);
      toast.success('Workspace deleted');
      await refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : err instanceof Error ? err.message : String(err);
      toast.error(msg);
    }
  }

  return (
    <div className="mx-auto max-w-6xl space-y-4 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold tracking-tight">Workspaces</h1>
        <button
          type="button"
          onClick={onNewWorkspace}
          className="flex items-center gap-1.5 rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500"
        >
          <Plus className="h-4 w-4" /> New workspace
        </button>
      </div>

      {loading ? (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-32 animate-pulse rounded-md border border-slate-800 bg-slate-900/40" />
          ))}
        </div>
      ) : workspaces.length === 0 ? (
        <EmptyState
          icon={LayoutGrid}
          title="No workspaces yet"
          description="A workspace creates a fresh git worktree on a new branch and lets you run a Claude Code or Codex agent inside it without touching your main checkout."
          action={
            repos.length === 0 ? (
              <span className="text-xs text-slate-500">Add a repo first.</span>
            ) : (
              <button
                type="button"
                onClick={onNewWorkspace}
                className="flex items-center gap-1.5 rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500"
              >
                <Plus className="h-4 w-4" /> Create workspace
              </button>
            )
          }
        />
      ) : (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
          {workspaces.map((ws) => (
            <button
              key={ws.id}
              type="button"
              onClick={() => navigate(`/workspaces/${ws.id}`)}
              className="group flex flex-col gap-2 rounded-md border border-slate-800 bg-slate-900/40 p-4 text-left hover:border-slate-600 hover:bg-slate-900"
            >
              <div className="flex items-start gap-2">
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm font-semibold text-slate-100">{ws.name}</div>
                  <div className="truncate text-xs text-slate-500">{repoName(ws.repoId)}</div>
                </div>
                <StatusBadge status={ws.status} />
              </div>
              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                <GitBranch className="h-3.5 w-3.5" />
                <span className="truncate font-mono">{ws.branchName}</span>
                <span className="text-slate-600">←</span>
                <span className="truncate font-mono">{ws.baseBranch}</span>
              </div>
              <div className="mt-auto flex items-center justify-between pt-2 text-xs text-slate-500">
                <span>{ws.agents.length} agent{ws.agents.length === 1 ? '' : 's'}</span>
                <span
                  role="button"
                  tabIndex={0}
                  onClick={(e) => remove(ws, e)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') remove(ws, e as unknown as React.MouseEvent);
                  }}
                  className="rounded p-1 opacity-0 group-hover:opacity-100 hover:bg-red-900/40 hover:text-red-300"
                  aria-label="Delete workspace"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </span>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
