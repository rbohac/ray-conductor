import { useState } from 'react';
import { Plus, FolderGit2, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Modal } from '../components/Modal';
import { EmptyState } from '../components/EmptyState';
import { useWorkspaces } from '../context/useWorkspaces';
import { api } from '../api/client';
import { ApiError } from '../api/client';

export function ReposPage() {
  const { repos, workspaces, refresh, loading } = useWorkspaces();
  const [open, setOpen] = useState(false);
  const [path, setPath] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [submitting, setSubmitting] = useState(false);

  function workspaceCount(repoId: string) {
    return workspaces.filter((w) => w.repoId === repoId).length;
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!path.trim()) return;
    setSubmitting(true);
    try {
      await api.createRepo({
        path: path.trim(),
        displayName: displayName.trim() || undefined,
      });
      toast.success('Repo registered');
      setPath('');
      setDisplayName('');
      setOpen(false);
      await refresh();
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : e instanceof Error ? e.message : String(e);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  async function remove(id: string, displayName: string) {
    if (!window.confirm(`Unregister ${displayName}? (the repo on disk is not deleted)`)) return;
    try {
      await api.deleteRepo(id);
      toast.success('Repo unregistered');
      await refresh();
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : e instanceof Error ? e.message : String(e);
      toast.error(msg);
    }
  }

  return (
    <div className="mx-auto max-w-4xl space-y-4 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold tracking-tight">Repos</h1>
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="flex items-center gap-1.5 rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500"
        >
          <Plus className="h-4 w-4" /> Add repo
        </button>
      </div>

      {loading ? (
        <div className="text-sm text-slate-500">Loading…</div>
      ) : repos.length === 0 ? (
        <EmptyState
          icon={FolderGit2}
          title="No repos yet"
          description="Add a local git repo path to get started. Worktrees for new workspaces will be created under <repo>/.maestro-worktrees/."
          action={
            <button
              type="button"
              onClick={() => setOpen(true)}
              className="flex items-center gap-1.5 rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500"
            >
              <Plus className="h-4 w-4" /> Add your first repo
            </button>
          }
        />
      ) : (
        <ul className="divide-y divide-slate-800 overflow-hidden rounded-md border border-slate-800 bg-slate-900/40">
          {repos.map((r) => (
            <li key={r.id} className="flex items-center gap-3 px-4 py-3">
              <FolderGit2 className="h-5 w-5 text-slate-500" />
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm font-medium text-slate-100">{r.displayName}</div>
                <div className="truncate font-mono text-xs text-slate-500">{r.path}</div>
              </div>
              <div className="text-xs text-slate-400">
                {r.defaultBranch ? (
                  <span className="rounded bg-slate-800 px-2 py-0.5 font-mono">{r.defaultBranch}</span>
                ) : null}
              </div>
              <div className="text-xs text-slate-500">
                {workspaceCount(r.id)} workspace{workspaceCount(r.id) === 1 ? '' : 's'}
              </div>
              <button
                type="button"
                onClick={() => remove(r.id, r.displayName)}
                className="rounded p-1.5 text-slate-400 hover:bg-red-900/40 hover:text-red-300"
                aria-label="Unregister"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <Modal
        open={open}
        title="Add repo"
        onClose={() => setOpen(false)}
        footer={
          <>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded px-3 py-1.5 text-sm text-slate-300 hover:bg-slate-800"
            >
              Cancel
            </button>
            <button
              type="submit"
              form="add-repo-form"
              disabled={submitting || !path.trim()}
              className="rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {submitting ? 'Adding…' : 'Add'}
            </button>
          </>
        }
      >
        <form id="add-repo-form" onSubmit={submit} className="space-y-3">
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-slate-300">Local path</span>
            <input
              type="text"
              autoFocus
              value={path}
              onChange={(e) => setPath(e.target.value)}
              placeholder="C:\src\my-project"
              className="w-full rounded border border-slate-700 bg-slate-950 px-2.5 py-1.5 font-mono text-sm text-slate-100 outline-none focus:border-emerald-500"
            />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-slate-300">
              Display name <span className="text-slate-500">(optional)</span>
            </span>
            <input
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="my-project"
              className="w-full rounded border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-sm text-slate-100 outline-none focus:border-emerald-500"
            />
          </label>
        </form>
      </Modal>
    </div>
  );
}
