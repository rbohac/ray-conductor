import { useEffect, useState } from 'react';
import { AlertTriangle, X } from 'lucide-react';
import { api } from '../api/client';
import type { SystemHealth } from '../api/types';

const INSTALL_HINTS: Record<string, { name: string; hint: string; href: string }> = {
  git: {
    name: 'git',
    hint: 'Install git for Windows',
    href: 'https://git-scm.com/download/win',
  },
  claude: {
    name: 'Claude Code CLI',
    hint: 'npm install -g @anthropic-ai/claude-code',
    href: 'https://docs.claude.com/en/docs/claude-code/overview',
  },
  codex: {
    name: 'Codex CLI',
    hint: 'npm install -g @openai/codex',
    href: 'https://github.com/openai/codex',
  },
};

export function HealthBanner() {
  const [health, setHealth] = useState<SystemHealth | null>(null);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    void api.health().then(setHealth).catch(() => {
      // health endpoint failure is itself a kind of "not healthy" signal,
      // but the rest of the app still works — stay quiet rather than scaring
      // the user with our own infra noise.
    });
  }, []);

  if (!health || dismissed) return null;

  const missing: ('git' | 'claude' | 'codex')[] = [];
  if (!health.git) missing.push('git');
  if (!health.claude) missing.push('claude');
  if (!health.codex) missing.push('codex');
  if (missing.length === 0) return null;

  return (
    <div className="border-b border-amber-900 bg-amber-950/40 px-4 py-2 text-sm text-amber-200">
      <div className="flex items-start gap-3">
        <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
        <div className="flex-1">
          <div className="font-medium">
            {missing.length === 1
              ? `${INSTALL_HINTS[missing[0]!]!.name} not detected on PATH.`
              : `${missing.length} CLIs not detected on PATH.`}
          </div>
          <ul className="mt-1 space-y-0.5 text-xs text-amber-300/90">
            {missing.map((m) => {
              const h = INSTALL_HINTS[m]!;
              return (
                <li key={m} className="font-mono">
                  <span className="text-amber-200">{h.name}:</span>{' '}
                  <code className="rounded bg-amber-950 px-1 py-0.5">{h.hint}</code>{' '}
                  <a className="underline hover:text-amber-100" href={h.href} target="_blank" rel="noreferrer">
                    docs
                  </a>
                </li>
              );
            })}
          </ul>
          <div className="mt-1 text-xs text-amber-300/70">
            Repo and workspace management still work; only agent spawning needs these.
          </div>
        </div>
        <button
          type="button"
          onClick={() => setDismissed(true)}
          className="rounded p-1 text-amber-300 hover:bg-amber-900/40"
          aria-label="Dismiss"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
