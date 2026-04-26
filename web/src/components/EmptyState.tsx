import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
  action?: ReactNode;
}

export function EmptyState({ icon: Icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-md border border-dashed border-slate-700 bg-slate-900/40 px-8 py-16 text-center">
      <Icon className="h-10 w-10 text-slate-500" />
      <h3 className="text-base font-semibold text-slate-200">{title}</h3>
      <p className="max-w-md text-sm text-slate-400">{description}</p>
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
