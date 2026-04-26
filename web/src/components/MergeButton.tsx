import { useState } from 'react';
import { GitMerge } from 'lucide-react';
import { toast } from 'sonner';
import { Modal } from './Modal';
import { api, ApiError } from '../api/client';
import type { DiffResult, Workspace } from '../api/types';

interface MergeButtonProps {
  workspace: Workspace;
  diff: DiffResult | null;
  onMerged: () => void;
}

export function MergeButton({ workspace, diff, onMerged }: MergeButtonProps) {
  const [open, setOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const merged = workspace.status === 'completed';
  const additions = diff?.files.reduce((s, f) => s + f.additions, 0) ?? 0;
  const deletions = diff?.files.reduce((s, f) => s + f.deletions, 0) ?? 0;
  const fileCount = diff?.files.length ?? 0;

  async function confirm() {
    setSubmitting(true);
    try {
      await api.mergeWorkspace(workspace.id);
      toast.success(`Merged into ${workspace.baseBranch}`);
      setOpen(false);
      onMerged();
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        const payload = e.payload as { conflicts?: string[] } | null;
        const list = payload?.conflicts?.join(', ') ?? '';
        toast.error(`${e.message}${list ? ` — ${list}` : ''}`);
      } else {
        toast.error(e instanceof Error ? e.message : String(e));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <button
        type="button"
        disabled={merged}
        onClick={() => setOpen(true)}
        className="flex items-center gap-1.5 rounded bg-emerald-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-600 disabled:cursor-not-allowed disabled:opacity-50"
      >
        <GitMerge className="h-4 w-4" /> {merged ? 'Merged' : 'Merge'}
      </button>
      <Modal
        open={open}
        title={`Merge ${workspace.branchName} into ${workspace.baseBranch}`}
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
              type="button"
              onClick={confirm}
              disabled={submitting}
              className="flex items-center gap-1.5 rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <GitMerge className="h-4 w-4" /> {submitting ? 'Merging…' : 'Confirm merge'}
            </button>
          </>
        }
      >
        <div className="space-y-2 text-sm text-slate-300">
          <p>
            This will run{' '}
            <code className="rounded bg-slate-800 px-1.5 py-0.5 font-mono text-xs">
              git checkout {workspace.baseBranch} &amp;&amp; git merge --no-ff {workspace.branchName}
            </code>{' '}
            in the source repo.
          </p>
          <div className="rounded border border-slate-800 bg-slate-950 p-3 text-xs">
            <div className="flex items-center gap-3">
              <span className="text-slate-400">Diff summary:</span>
              <span className="text-emerald-400">+{additions}</span>
              <span className="text-red-400">-{deletions}</span>
              <span className="text-slate-500">
                {fileCount} file{fileCount === 1 ? '' : 's'}
              </span>
            </div>
            {diff && diff.files.length > 0 ? (
              <ul className="mt-2 space-y-0.5">
                {diff.files.slice(0, 8).map((f) => (
                  <li key={f.path} className="font-mono text-slate-400">
                    <span
                      className={
                        f.status === 'added'
                          ? 'text-emerald-400'
                          : f.status === 'deleted'
                            ? 'text-red-400'
                            : f.status === 'renamed'
                              ? 'text-amber-400'
                              : 'text-blue-400'
                      }
                    >
                      [{f.status[0]?.toUpperCase()}]
                    </span>{' '}
                    {f.path}
                  </li>
                ))}
                {diff.files.length > 8 ? (
                  <li className="text-slate-500">…and {diff.files.length - 8} more</li>
                ) : null}
              </ul>
            ) : null}
          </div>
          <p className="text-xs text-slate-500">
            On conflict the merge is aborted and your source repo is left clean.
          </p>
        </div>
      </Modal>
    </>
  );
}
