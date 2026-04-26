import { useCallback, useEffect, useState } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { Header } from './components/Header';
import { WorkspacesPage } from './pages/WorkspacesPage';
import { ReposPage } from './pages/ReposPage';
import { WorkspaceDetailPage } from './pages/WorkspaceDetailPage';
import { NewWorkspaceModal } from './components/NewWorkspaceModal';
import { WorkspacesProvider } from './context/WorkspacesContext';

function Shell() {
  const [newOpen, setNewOpen] = useState(false);

  const openNew = useCallback(() => setNewOpen(true), []);
  const closeNew = useCallback(() => setNewOpen(false), []);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      // Ctrl+N (or Cmd+N) opens the new-workspace modal from anywhere.
      const meta = e.ctrlKey || e.metaKey;
      if (meta && e.key.toLowerCase() === 'n') {
        // Browser default for Ctrl+N is "open new window" — we override.
        e.preventDefault();
        setNewOpen(true);
      }
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <div className="flex h-full flex-col">
      <Header onNewWorkspace={openNew} />
      <main className="flex-1 overflow-auto">
        <Routes>
          <Route path="/" element={<WorkspacesPage onNewWorkspace={openNew} />} />
          <Route path="/repos" element={<ReposPage />} />
          <Route path="/workspaces/:id" element={<WorkspaceDetailPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      <NewWorkspaceModal open={newOpen} onClose={closeNew} />
    </div>
  );
}

export default function App() {
  return (
    <WorkspacesProvider>
      <Shell />
    </WorkspacesProvider>
  );
}
