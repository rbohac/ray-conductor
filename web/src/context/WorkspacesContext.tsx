import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api } from '../api/client';
import type { Repo, Workspace } from '../api/types';
import { WorkspacesContext, type WorkspacesContextValue } from './workspacesContextValue';

export function WorkspacesProvider({ children }: { children: ReactNode }) {
  const [repos, setRepos] = useState<Repo[]>([]);
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setError(null);
      const [r, w] = await Promise.all([api.listRepos(), api.listWorkspaces()]);
      setRepos(r);
      setWorkspaces(w);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, []);

  const refreshWorkspace = useCallback(async (id: string) => {
    try {
      const ws = await api.getWorkspace(id);
      setWorkspaces((prev) => prev.map((w) => (w.id === id ? ws : w)));
      return ws;
    } catch {
      return null;
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const value = useMemo<WorkspacesContextValue>(
    () => ({ repos, workspaces, loading, error, refresh, refreshWorkspace }),
    [repos, workspaces, loading, error, refresh, refreshWorkspace],
  );

  return <WorkspacesContext.Provider value={value}>{children}</WorkspacesContext.Provider>;
}
