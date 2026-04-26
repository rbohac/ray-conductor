import { CheckCircle2, Circle, Loader2, XCircle } from 'lucide-react';
import type { AgentStatus, WorkspaceStatus } from '../api/types';

interface StatusBadgeProps {
  status: WorkspaceStatus | AgentStatus;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  switch (status) {
    case 'running':
    case 'starting':
      return (
        <span className="flex items-center gap-1 rounded bg-emerald-900/50 px-1.5 py-0.5 text-[11px] font-medium text-emerald-300">
          <Loader2 className="h-3 w-3 animate-spin" /> {status}
        </span>
      );
    case 'completed':
      return (
        <span className="flex items-center gap-1 rounded bg-emerald-900/30 px-1.5 py-0.5 text-[11px] font-medium text-emerald-200">
          <CheckCircle2 className="h-3 w-3" /> completed
        </span>
      );
    case 'errored':
    case 'orphaned':
      return (
        <span className="flex items-center gap-1 rounded bg-red-900/40 px-1.5 py-0.5 text-[11px] font-medium text-red-300">
          <XCircle className="h-3 w-3" /> {status}
        </span>
      );
    case 'stopped':
      return (
        <span className="flex items-center gap-1 rounded bg-slate-800 px-1.5 py-0.5 text-[11px] font-medium text-slate-300">
          stopped
        </span>
      );
    case 'idle':
    default:
      return (
        <span className="flex items-center gap-1 rounded bg-slate-800 px-1.5 py-0.5 text-[11px] font-medium text-slate-400">
          <Circle className="h-3 w-3" /> idle
        </span>
      );
  }
}
