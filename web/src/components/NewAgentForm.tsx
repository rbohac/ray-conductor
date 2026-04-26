import { useState } from 'react';
import { Play } from 'lucide-react';
import { toast } from 'sonner';
import { api, ApiError } from '../api/client';
import type { AgentProvider } from '../api/types';

interface NewAgentFormProps {
  workspaceId: string;
  onStarted: () => void;
}

const CLAUDE_MODELS = [
  '',
  'claude-opus-4-7',
  'claude-sonnet-4-6',
  'claude-haiku-4-5',
];

const CODEX_MODELS = [
  '',
  'gpt-5-codex',
  'gpt-5',
];

const KEY_PROVIDER = 'maestro:lastProvider';
const keyModel = (p: AgentProvider) => `maestro:lastModel:${p}`;

function readLastProvider(): AgentProvider {
  const raw = localStorage.getItem(KEY_PROVIDER);
  return raw === 'codex' ? 'codex' : 'claude';
}

export function NewAgentForm({ workspaceId, onStarted }: NewAgentFormProps) {
  const [provider, setProviderState] = useState<AgentProvider>(readLastProvider);
  const [model, setModelState] = useState<string>(() => localStorage.getItem(keyModel(readLastProvider())) ?? '');
  const [prompt, setPrompt] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const models = provider === 'claude' ? CLAUDE_MODELS : CODEX_MODELS;

  function setProvider(p: AgentProvider) {
    setProviderState(p);
    localStorage.setItem(KEY_PROVIDER, p);
    const remembered = localStorage.getItem(keyModel(p)) ?? '';
    setModelState(remembered);
  }

  function setModel(m: string) {
    setModelState(m);
    localStorage.setItem(keyModel(provider), m);
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!prompt.trim()) return;
    setSubmitting(true);
    try {
      await api.startAgent(workspaceId, {
        provider,
        model: model || undefined,
        initialPrompt: prompt,
      });
      setPrompt('');
      onStarted();
      toast.success(`${provider} agent started`);
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : e instanceof Error ? e.message : String(e));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-2 rounded-md border border-slate-800 bg-slate-900/40 p-3">
      <div className="flex items-center gap-2">
        <select
          value={provider}
          onChange={(e) => setProvider(e.target.value as AgentProvider)}
          className="rounded border border-slate-700 bg-slate-950 px-2 py-1 text-sm text-slate-100 outline-none focus:border-emerald-500"
        >
          <option value="claude">Claude</option>
          <option value="codex">Codex</option>
        </select>
        <select
          value={model}
          onChange={(e) => setModel(e.target.value)}
          className="rounded border border-slate-700 bg-slate-950 px-2 py-1 font-mono text-xs text-slate-300 outline-none focus:border-emerald-500"
        >
          {models.map((m) => (
            <option key={m} value={m}>
              {m || '(default model)'}
            </option>
          ))}
        </select>
        <button
          type="submit"
          disabled={submitting || !prompt.trim()}
          className="ml-auto flex items-center gap-1.5 rounded bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Play className="h-3.5 w-3.5" /> {submitting ? 'starting…' : 'Start agent'}
        </button>
      </div>
      <textarea
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
        placeholder="Describe what the agent should do, e.g. 'add a search bar to the user list page'"
        rows={3}
        className="w-full resize-y rounded border border-slate-700 bg-slate-950 px-2.5 py-1.5 text-sm text-slate-100 outline-none focus:border-emerald-500"
      />
    </form>
  );
}
