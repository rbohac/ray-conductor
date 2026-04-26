import { Routes, Route } from 'react-router-dom';
import { Music4 } from 'lucide-react';

function HelloPage() {
  return (
    <div className="flex flex-col items-center justify-center h-full gap-4">
      <Music4 className="w-12 h-12 text-emerald-400" />
      <h1 className="text-4xl font-semibold tracking-tight">Hello Maestro</h1>
      <p className="text-slate-400 text-sm">
        Skeleton wired. Phase 2 will introduce repos &amp; workspaces.
      </p>
    </div>
  );
}

export default function App() {
  return (
    <div className="h-full flex flex-col">
      <header className="border-b border-slate-800 px-4 py-3 flex items-center gap-2">
        <Music4 className="w-5 h-5 text-emerald-400" />
        <span className="font-semibold tracking-tight">Maestro</span>
      </header>
      <main className="flex-1 overflow-auto">
        <Routes>
          <Route path="/" element={<HelloPage />} />
          <Route path="*" element={<HelloPage />} />
        </Routes>
      </main>
    </div>
  );
}
