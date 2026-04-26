import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, GitBranch } from 'lucide-react';
import { useWorkspaces } from '../context/useWorkspaces';
import { StatusBadge } from '../components/StatusBadge';

export function WorkspaceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { workspaces, repos, loading } = useWorkspaces();

  if (loading) {
    return <div className="p-6 text-sm text-slate-500">Loading…</div>;
  }

  const ws = workspaces.find((w) => w.id === id);
  if (!ws) {
    return (
      <div className="p-6 text-sm text-slate-400">
        Workspace not found.{' '}
        <Link to="/" className="text-emerald-400 hover:underline">
          Back
        </Link>
      </div>
    );
  }
  const repo = repos.find((r) => r.id === ws.repoId);

  return (
    <div className="mx-auto max-w-5xl space-y-4 p-6">
      <Link to="/" className="inline-flex items-center gap-1 text-sm text-slate-400 hover:text-slate-200">
        <ArrowLeft className="h-4 w-4" /> Workspaces
      </Link>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{ws.name}</h1>
          <div className="mt-1 text-xs text-slate-500">{repo?.displayName ?? '(missing repo)'}</div>
          <div className="mt-2 flex items-center gap-1.5 text-xs text-slate-400">
            <GitBranch className="h-3.5 w-3.5" />
            <span className="font-mono">{ws.branchName}</span>
            <span className="text-slate-600">←</span>
            <span className="font-mono">{ws.baseBranch}</span>
          </div>
        </div>
        <StatusBadge status={ws.status} />
      </div>

      <div className="rounded-md border border-slate-800 bg-slate-900/40 p-4 text-sm text-slate-400">
        <div className="mb-2 font-mono text-xs text-slate-500">{ws.worktreePath}</div>
        <p>
          Agents will run here in Phase 3. Output and diff streaming wire up in
          Phases 3–4. For now you can verify the worktree and branch on disk.
        </p>
      </div>
    </div>
  );
}
