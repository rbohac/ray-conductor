import { NavLink, useNavigate } from 'react-router-dom';
import { Music4, FolderGit2, LayoutGrid, Plus, Square } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../api/client';

interface HeaderProps {
  onNewWorkspace: () => void;
}

export function Header({ onNewWorkspace }: HeaderProps) {
  const navigate = useNavigate();

  async function stopAll() {
    if (!window.confirm('Stop ALL running agents across all workspaces?')) return;
    try {
      await api.stopAllAgents();
      toast.success('Stopped all agents');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : String(e));
    }
  }

  return (
    <header className="flex items-center gap-4 border-b border-slate-800 bg-slate-950 px-4 py-2.5">
      <button
        type="button"
        className="flex items-center gap-2 text-sm font-semibold tracking-tight"
        onClick={() => navigate('/')}
      >
        <Music4 className="h-5 w-5 text-emerald-400" />
        <span>Maestro</span>
      </button>

      <nav className="flex items-center gap-1 text-sm">
        <NavLink
          to="/"
          end
          className={({ isActive }) =>
            `flex items-center gap-1.5 rounded px-2.5 py-1.5 ${
              isActive
                ? 'bg-slate-800 text-slate-100'
                : 'text-slate-400 hover:bg-slate-800/60 hover:text-slate-200'
            }`
          }
        >
          <LayoutGrid className="h-4 w-4" /> Workspaces
        </NavLink>
        <NavLink
          to="/repos"
          className={({ isActive }) =>
            `flex items-center gap-1.5 rounded px-2.5 py-1.5 ${
              isActive
                ? 'bg-slate-800 text-slate-100'
                : 'text-slate-400 hover:bg-slate-800/60 hover:text-slate-200'
            }`
          }
        >
          <FolderGit2 className="h-4 w-4" /> Repos
        </NavLink>
      </nav>

      <div className="ml-auto flex items-center gap-2">
        <button
          type="button"
          onClick={onNewWorkspace}
          className="flex items-center gap-1.5 rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500"
        >
          <Plus className="h-4 w-4" /> New workspace
        </button>
        <button
          type="button"
          onClick={stopAll}
          className="flex items-center gap-1.5 rounded border border-red-800 bg-red-950/60 px-3 py-1.5 text-sm font-medium text-red-200 hover:bg-red-900/60"
          title="Kill all running agents"
        >
          <Square className="h-4 w-4 fill-red-300" /> Stop all
        </button>
      </div>
    </header>
  );
}
