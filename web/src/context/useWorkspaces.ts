import { useContext } from 'react';
import { WorkspacesContext } from './workspacesContextValue';

export function useWorkspaces() {
  const ctx = useContext(WorkspacesContext);
  if (!ctx) {
    throw new Error('useWorkspaces must be used inside <WorkspacesProvider>');
  }
  return ctx;
}
