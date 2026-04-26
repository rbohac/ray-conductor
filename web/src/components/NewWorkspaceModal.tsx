import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Modal } from './Modal';
import { useWorkspaces } from '../context/useWorkspaces';
import { api, ApiError } from '../api/client';

interface NewWorkspaceModalProps {
  open: boolean;
  onClose: () => void;
}

function slugify(input: string) {
  return input
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 60);
}

interface FormProps {
  onClose: () => void;
}

function NewWorkspaceForm({ onClose }: FormProps) {
  const { repos, refresh } = useWorkspaces();

  const [repoId, setRepoId] = useState<string>(() => repos[0]?.id ?? '');
  const [name, setName] = useState<string>('');
  const [branchOverride, setBranchOverride] = useState<string | null>(null);
  const [baseOverride, setBaseOverride] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState<boolean>(false);

  const selectedRepo = useMemo(() => repos.find((r) => r.id === repoId), [repos, repoId]);
  const autoBranch = name.trim() ? `maestro/${slugify(name)}` : '';
  const branch = branchOverride ?? autoBranch;
  const autoBase = selectedRepo?.defaultBranch || 'main';
  const baseBranch = baseOverride ?? autoBase;

  if (repos.length === 0) {
    return <div className="text-sm text-slate-400">Add a repo first on the Repos page.</div>;
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!repoId || !name.trim() || !branch.trim() || !baseBranch.trim()) return;
    setSubmitting(true);
    try {
      await api.createWorkspace({
        repoId,
        name: name.trim(),
        branchName: branch.trim(),
        baseBranch: baseBranch.trim(),
      });
      toast.success('Workspace created');
      await refresh();
      onClose();
    } catch (e) {
      const msg = e instanceof ApiError ? e.message : e instanceof Error ? e.message : String(e);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form id="new-workspace-form" onSubmit={submit} className="space-y-3">
      <label className="block">
        <span className="mb-1 block text-xs font-medium text-slate-300">Repo</span>
        <select
          value={repoId}
          onChange={(e) => {
            setRepoId(e.target.value);
            setBaseOverride(null);
          }}
          className="w-full rounded border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-sm text-slate-100 outline-none focus:border-emerald-500"
        >
          {repos.map((r) => (
            <option key={r.id} value={r.id}>
              {r.displayName}
            </option>
          ))}
        </select>
      </label>
      <label className="block">
        <span className="mb-1 block text-xs font-medium text-slate-300">Name</span>
        <input
          autoFocus
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="add user search"
          className="w-full rounded border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-sm text-slate-100 outline-none focus:border-emerald-500"
        />
      </label>
      <div className="grid grid-cols-2 gap-3">
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-slate-300">Branch</span>
          <input
            type="text"
            value={branch}
            onChange={(e) => setBranchOverride(e.target.value)}
            className="w-full rounded border border-slate-700 bg-slate-950 px-2.5 py-1.5 font-mono text-sm text-slate-100 outline-none focus:border-emerald-500"
          />
        </label>
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-slate-300">Base</span>
          <input
            type="text"
            value={baseBranch}
            onChange={(e) => setBaseOverride(e.target.value)}
            className="w-full rounded border border-slate-700 bg-slate-950 px-2.5 py-1.5 font-mono text-sm text-slate-100 outline-none focus:border-emerald-500"
          />
        </label>
      </div>
      <div className="hidden">
        <button
          type="submit"
          disabled={
            submitting
            || !repoId
            || !name.trim()
            || !branch.trim()
            || !baseBranch.trim()
          }
        />
      </div>
    </form>
  );
}

export function NewWorkspaceModal({ open, onClose }: NewWorkspaceModalProps) {
  // Mount the form only when open so its state resets cleanly each time.
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="New workspace"
      footer={
        <>
          <button
            type="button"
            onClick={onClose}
            className="rounded px-3 py-1.5 text-sm text-slate-300 hover:bg-slate-800"
          >
            Cancel
          </button>
          <button
            type="submit"
            form="new-workspace-form"
            className="rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500"
          >
            Create
          </button>
        </>
      }
    >
      {open ? <NewWorkspaceForm onClose={onClose} /> : null}
    </Modal>
  );
}
