import { useCallback, useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, GitBranch } from 'lucide-react';
import { useWorkspaces } from '../context/useWorkspaces';
import { StatusBadge } from '../components/StatusBadge';
import { useSse } from '../hooks/useSse';
import { AgentOutputPanel } from '../components/AgentOutputPanel';
import type { OutputLine } from '../components/AgentOutputPanel';
import { NewAgentForm } from '../components/NewAgentForm';
import type { AgentStatus } from '../api/types';

interface AgentStatusEvent {
  agentId: string;
  status: AgentStatus;
  exitCode?: number | null;
  ts: string;
}

export function WorkspaceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { workspaces, repos, loading, refreshWorkspace } = useWorkspaces();
  const [lines, setLines] = useState<OutputLine[]>([]);

  const ws = workspaces.find((w) => w.id === id) ?? null;
  const repo = ws ? repos.find((r) => r.id === ws.repoId) : null;

  const onEvent = useCallback(
    (eventName: string, data: unknown) => {
      if (eventName === 'agent-output') {
        const ev = data as OutputLine;
        setLines((prev) => {
          const next = [...prev, ev];
          // Cap at 5000 lines per workspace to avoid pathological growth
          // when an agent produces enormous output.
          return next.length > 5000 ? next.slice(-5000) : next;
        });
      } else if (eventName === 'agent-status') {
        const ev = data as AgentStatusEvent;
        if (ws) {
          // Refetch the workspace so the agent list reflects the new state.
          void refreshWorkspace(ws.id);
        }
        // Surface terminal events as a synthetic output line so the user sees
        // something in the panel even if the CLI printed nothing.
        if (ev.status !== 'running' && ev.status !== 'starting') {
          setLines((prev) => [
            ...prev,
            {
              agentId: ev.agentId,
              stream: 'stdout',
              line: `[agent ${ev.status}${ev.exitCode != null ? ` (exit ${ev.exitCode})` : ''}]`,
              ts: ev.ts,
            },
          ]);
        }
      }
    },
    [ws, refreshWorkspace],
  );

  // Subscribe whenever we have a valid workspace id.
  const { connected } = useSse(ws ? `/api/events/workspace/${ws.id}` : null, {
    onEvent,
  });

  // When navigating between workspaces, drop any cached output from the prior one.
  useEffect(() => {
    setLines([]);
  }, [id]);

  if (loading) {
    return <div className="p-6 text-sm text-slate-500">Loading…</div>;
  }
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

  const sortedAgents = [...ws.agents].sort(
    (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime(),
  );

  return (
    <div className="mx-auto flex h-full max-w-6xl flex-col gap-3 p-4">
      <div>
        <Link to="/" className="inline-flex items-center gap-1 text-xs text-slate-400 hover:text-slate-200">
          <ArrowLeft className="h-3.5 w-3.5" /> Workspaces
        </Link>
      </div>
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-lg font-semibold tracking-tight">{ws.name}</h1>
            <StatusBadge status={ws.status} />
            <span
              className={`text-[11px] ${connected ? 'text-emerald-400' : 'text-slate-500'}`}
              title={connected ? 'SSE connected' : 'SSE disconnected'}
            >
              {connected ? '● live' : '○ offline'}
            </span>
          </div>
          <div className="mt-1 text-xs text-slate-500">{repo?.displayName ?? '(missing repo)'}</div>
          <div className="mt-1.5 flex items-center gap-1.5 text-xs text-slate-400">
            <GitBranch className="h-3.5 w-3.5" />
            <span className="font-mono">{ws.branchName}</span>
            <span className="text-slate-600">←</span>
            <span className="font-mono">{ws.baseBranch}</span>
          </div>
          <div className="mt-1 truncate font-mono text-[11px] text-slate-600">{ws.worktreePath}</div>
        </div>
      </div>

      <NewAgentForm workspaceId={ws.id} onStarted={() => void refreshWorkspace(ws.id)} />

      <div className="flex-1 min-h-0 space-y-3">
        {sortedAgents.length === 0 ? (
          <div className="rounded-md border border-dashed border-slate-700 bg-slate-900/40 p-6 text-center text-sm text-slate-500">
            No agents yet. Start one above.
          </div>
        ) : (
          sortedAgents.map((agent) => (
            <div key={agent.id} className="h-72">
              <AgentOutputPanel workspaceId={ws.id} agent={agent} lines={lines} />
            </div>
          ))
        )}
      </div>
    </div>
  );
}
