import { useEffect, useRef, useState } from 'react';
import { ArrowDownToLine, Send, Square } from 'lucide-react';
import { toast } from 'sonner';
import { api, ApiError } from '../api/client';
import { StatusBadge } from './StatusBadge';
import type { Agent } from '../api/types';

export interface OutputLine {
  agentId: string;
  stream: 'stdout' | 'stderr';
  line: string;
  ts: string;
}

interface AgentOutputPanelProps {
  workspaceId: string;
  agent: Agent;
  lines: OutputLine[];
}

const isTerminal = (s: Agent['status']) =>
  s === 'completed' || s === 'errored' || s === 'stopped' || s === 'orphaned';

export function AgentOutputPanel({ workspaceId, agent, lines }: AgentOutputPanelProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [autoscroll, setAutoscroll] = useState(true);
  const [draft, setDraft] = useState('');

  useEffect(() => {
    if (!autoscroll) return;
    const el = containerRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [lines, autoscroll]);

  function onScroll() {
    const el = containerRef.current;
    if (!el) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 20;
    setAutoscroll(atBottom);
  }

  async function stop() {
    try {
      await api.stopAgent(workspaceId, agent.id);
      toast.success('Agent stopped');
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : e instanceof Error ? e.message : String(e));
    }
  }

  async function send(e: React.FormEvent) {
    e.preventDefault();
    if (!draft.trim()) return;
    try {
      await api.sendAgentMessage(workspaceId, agent.id, draft);
      setDraft('');
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : e instanceof Error ? e.message : String(e));
    }
  }

  const filtered = lines.filter((l) => l.agentId === agent.id);
  const running = !isTerminal(agent.status);

  return (
    <div className="flex h-full min-h-0 flex-col rounded-md border border-slate-800 bg-slate-950">
      <div className="flex items-center gap-2 border-b border-slate-800 px-3 py-2">
        <span className="text-xs font-mono text-slate-500">{agent.id.slice(0, 8)}</span>
        <span className="rounded bg-slate-800 px-1.5 py-0.5 text-[11px] uppercase text-slate-300">
          {agent.provider}
        </span>
        {agent.model ? (
          <span className="rounded bg-slate-800 px-1.5 py-0.5 text-[11px] text-slate-400 font-mono">
            {agent.model}
          </span>
        ) : null}
        <StatusBadge status={agent.status} />
        <div className="ml-auto flex items-center gap-1">
          {!autoscroll && running ? (
            <button
              type="button"
              onClick={() => {
                setAutoscroll(true);
                const el = containerRef.current;
                if (el) el.scrollTop = el.scrollHeight;
              }}
              className="flex items-center gap-1 rounded border border-slate-700 px-2 py-0.5 text-[11px] text-slate-300 hover:bg-slate-800"
            >
              <ArrowDownToLine className="h-3 w-3" /> resume autoscroll
            </button>
          ) : null}
          {running ? (
            <button
              type="button"
              onClick={stop}
              className="flex items-center gap-1 rounded border border-red-800 bg-red-950/50 px-2 py-0.5 text-[11px] text-red-200 hover:bg-red-900/60"
            >
              <Square className="h-3 w-3 fill-red-300" /> stop
            </button>
          ) : null}
        </div>
      </div>
      <div
        ref={containerRef}
        onScroll={onScroll}
        className="min-h-0 flex-1 overflow-auto bg-slate-950 p-3 font-mono text-[12.5px] leading-snug"
      >
        <div className="text-slate-500">$ {agent.provider} {agent.initialPrompt}</div>
        {filtered.length === 0 && running ? (
          <div className="mt-1 text-slate-500">waiting for output…</div>
        ) : null}
        {filtered.map((l, i) => (
          <div
            key={i}
            className={l.stream === 'stderr' ? 'text-red-300 whitespace-pre-wrap' : 'text-slate-200 whitespace-pre-wrap'}
          >
            {l.line}
          </div>
        ))}
        {!running ? (
          <div className="mt-2 text-slate-600">[exit {agent.exitCode ?? '?'}]</div>
        ) : null}
      </div>
      {running ? (
        <form onSubmit={send} className="flex items-center gap-2 border-t border-slate-800 px-2 py-2">
          <input
            type="text"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="send to stdin"
            className="flex-1 rounded border border-slate-700 bg-slate-900 px-2 py-1 font-mono text-sm text-slate-100 outline-none focus:border-emerald-500"
          />
          <button
            type="submit"
            disabled={!draft.trim()}
            className="flex items-center gap-1 rounded bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <Send className="h-3 w-3" /> send
          </button>
        </form>
      ) : null}
    </div>
  );
}
