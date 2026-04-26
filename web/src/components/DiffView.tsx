import { useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, FileDiff, FilePlus, FileMinus, FilePen, RefreshCcw } from 'lucide-react';
import ReactDiffViewer, { DiffMethod } from 'react-diff-viewer-continued';
import { api } from '../api/client';
import type { DiffFile, DiffResult } from '../api/types';

interface DiffViewProps {
  workspaceId: string;
  /** A monotonic counter the parent bumps on `worktree-changed` SSE events. */
  changeToken: number;
}

function reconstruct(file: DiffFile): { before: string; after: string } {
  // react-diff-viewer-continued is a side-by-side renderer; reconstruct the
  // pre/post text for the file by walking the hunk bodies. Surrounding
  // context is included verbatim, the `+`/`-` prefix decides the side.
  const before: string[] = [];
  const after: string[] = [];
  for (const hunk of file.hunks) {
    for (const line of hunk.body.split('\n')) {
      if (line.startsWith('\\')) continue;
      if (line.startsWith('+')) {
        after.push(line.slice(1));
      } else if (line.startsWith('-')) {
        before.push(line.slice(1));
      } else if (line.startsWith(' ')) {
        before.push(line.slice(1));
        after.push(line.slice(1));
      } else {
        before.push(line);
        after.push(line);
      }
    }
    before.push('');
    after.push('');
  }
  return { before: before.join('\n'), after: after.join('\n') };
}

function FileIcon({ status }: { status: DiffFile['status'] }) {
  switch (status) {
    case 'added':
      return <FilePlus className="h-4 w-4 text-emerald-400" />;
    case 'deleted':
      return <FileMinus className="h-4 w-4 text-red-400" />;
    case 'renamed':
      return <FilePen className="h-4 w-4 text-amber-400" />;
    case 'modified':
    default:
      return <FileDiff className="h-4 w-4 text-blue-400" />;
  }
}

function FileBlock({ file }: { file: DiffFile }) {
  const [expanded, setExpanded] = useState(true);
  const { before, after } = reconstruct(file);
  return (
    <div className="overflow-hidden rounded-md border border-slate-800 bg-slate-900/40">
      <button
        type="button"
        onClick={() => setExpanded((e) => !e)}
        className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-slate-900"
      >
        {expanded ? <ChevronDown className="h-4 w-4 text-slate-500" /> : <ChevronRight className="h-4 w-4 text-slate-500" />}
        <FileIcon status={file.status} />
        <span className="truncate font-mono text-slate-200">{file.path}</span>
        {file.oldPath && file.oldPath !== file.path ? (
          <span className="font-mono text-xs text-slate-500">← {file.oldPath}</span>
        ) : null}
        <span className="ml-auto text-xs">
          <span className="text-emerald-400">+{file.additions}</span>{' '}
          <span className="text-red-400">-{file.deletions}</span>
        </span>
      </button>
      {expanded ? (
        <div className="diff-host border-t border-slate-800 text-xs">
          <ReactDiffViewer
            oldValue={before}
            newValue={after}
            splitView={false}
            useDarkTheme
            compareMethod={DiffMethod.LINES}
            hideLineNumbers={false}
          />
        </div>
      ) : null}
    </div>
  );
}

export function DiffView({ workspaceId, changeToken }: DiffViewProps) {
  const [data, setData] = useState<DiffResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pulse, setPulse] = useState(false);

  async function load() {
    try {
      setError(null);
      const r = await api.getDiff(workspaceId);
      setData(r);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  // Initial fetch and refetch on workspace change.
  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [workspaceId]);

  // Refetch when the watcher reports a worktree change.
  useEffect(() => {
    if (changeToken === 0) return;
    setPulse(true);
    const t = setTimeout(() => setPulse(false), 800);
    void load();
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [changeToken]);

  const totalAdditions = data?.files.reduce((s, f) => s + f.additions, 0) ?? 0;
  const totalDeletions = data?.files.reduce((s, f) => s + f.deletions, 0) ?? 0;

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3 text-sm">
        <div className="text-slate-400">
          {data ? (
            <>
              <span className="font-mono">{data.headBranch}</span>{' '}
              <span className="text-slate-600">vs.</span>{' '}
              <span className="font-mono">{data.baseBranch}</span>
              <span className="ml-3 text-emerald-400">+{totalAdditions}</span>{' '}
              <span className="text-red-400">-{totalDeletions}</span>{' '}
              <span className="text-slate-500">
                in {data.files.length} file{data.files.length === 1 ? '' : 's'}
              </span>
            </>
          ) : (
            'Loading diff…'
          )}
        </div>
        {pulse ? (
          <span className="inline-flex items-center rounded-full bg-emerald-900/50 px-2 py-0.5 text-[11px] text-emerald-300">
            updated
          </span>
        ) : null}
        <button
          type="button"
          onClick={() => void load()}
          className="ml-auto flex items-center gap-1 rounded border border-slate-700 px-2 py-1 text-xs text-slate-300 hover:bg-slate-800"
        >
          <RefreshCcw className="h-3.5 w-3.5" /> refresh
        </button>
      </div>

      {error ? (
        <div className="rounded border border-red-900 bg-red-950/40 p-3 text-sm text-red-300">{error}</div>
      ) : null}

      {data && data.files.length === 0 ? (
        <div className="rounded-md border border-dashed border-slate-700 bg-slate-900/40 p-6 text-center text-sm text-slate-500">
          No changes between <span className="font-mono">{data.headBranch}</span> and{' '}
          <span className="font-mono">{data.baseBranch}</span>.
        </div>
      ) : null}

      {data?.files.map((f) => <FileBlock key={f.path} file={f} />)}
    </div>
  );
}
