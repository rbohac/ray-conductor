import { createContext } from 'react';
import type { Repo, Workspace } from '../api/types';

export interface WorkspacesContextValue {
  repos: Repo[];
  workspaces: Workspace[];
  loading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
  refreshWorkspace: (id: string) => Promise<Workspace | null>;
}

export const WorkspacesContext = createContext<WorkspacesContextValue | undefined>(undefined);
