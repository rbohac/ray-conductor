# Maestro

Personal Conductor-style orchestrator for running parallel Claude Code / Codex
agents in isolated git worktrees. Single-port, single-binary Windows app.

## Stack

- Backend: .NET 10 ASP.NET Core (controllers, OpenAPI, `ILogger`)
- Frontend: Vite + React 19 + TypeScript (strict) + Tailwind 3
- Real-time: Server-Sent Events (no SignalR)
- Persistence: JSON file at `%LOCALAPPDATA%\Maestro\state.json`
- Packaging: `dotnet publish` → single self-contained `Maestro.exe` (~50 MB)

## Setup

1. Install [git for Windows](https://git-scm.com/download/win) and Node.js
   (for the Claude / Codex CLIs).
2. Install the agent CLIs you want to use:
   - `npm i -g @anthropic-ai/claude-code`
   - `npm i -g @openai/codex`
3. Build Maestro yourself (see "Producing a release .exe" below) or grab the
   prebuilt `Maestro.exe`.

## Daily use

Double-click `Maestro.exe`. Maestro starts on `http://localhost:5000` and
your default browser opens automatically.

1. **Repos page** — register the local clone of any repo you want to work in.
   Maestro never modifies your main checkout until you explicitly merge.
2. **New workspace** — pick a repo, give the workspace a name, choose a base
   branch. Maestro creates a fresh worktree at
   `<repo>\.maestro-worktrees\<id>` on a brand-new branch.
3. **Spawn an agent** — pick Claude or Codex, drop in a prompt, hit "Start
   agent". Output streams live; the diff tab updates as files change. Send
   follow-up messages to the agent's stdin from the input below the output
   pane.
4. **Merge** — when you're happy with the changes, click "Merge". Maestro
   runs `git checkout <base> && git merge --no-ff <branch>` in the source
   repo. On conflict the merge is aborted automatically.
5. **Delete** — removes the worktree and the workspace branch. Merged
   commits stay on the base branch.

The big red "Stop all" button in the header kills every running agent
across every workspace at once.

## Development

Run two terminals:

```sh
# Terminal 1 - .NET API on :5000
cd api
dotnet run

# Terminal 2 - Vite dev server on :5173 (proxies /api and /events to :5000)
cd web
npm install
npm run dev
```

Open `http://localhost:5173` for development with HMR. The dev server proxies
`/api` and `/events` so the SPA hits the same code paths as in production.

To override the agent CLI paths during development (or to point at a stub):

```sh
Maestro__ClaudeCommand=/path/to/claude Maestro__CodexCommand=/path/to/codex \
  dotnet run
```

## Producing a release `.exe`

```sh
cd api
dotnet publish -c Release -o ../publish
```

The Vite frontend is built automatically by the `BuildWebFrontendOnPublish`
MSBuild target. Output is `publish\Maestro.exe` plus a small `wwwroot\` and
`appsettings.json`. The exe is self-contained — no .NET runtime needed on
the target machine.

## Updating the underlying CLIs

Maestro shells out to `claude` and `codex` from your `PATH`. Update them as
you normally would (`npm i -g @anthropic-ai/claude-code` and the equivalent
for Codex). On startup Maestro runs `claude --version` / `codex --version`
and shows a banner if either is missing.

## Known limitations (v1)

- Windows-only release target (`win-x64`). The codebase is cross-platform but
  publish profile and `taskkill` usage are Windows-shaped.
- Agents run with `--dangerously-skip-permissions` (Claude) / `--yolo` (Codex)
  for fully-autonomous v1; the worktree itself is the isolation boundary.
- No interactive approval forwarding through SSE — agents that try to prompt
  for approval will hang. Use a different prompt or stop them.
- Local merge only. No GitHub PR integration.
- Single-user, single-machine, no auth. `localhost`-only by design.
- SQLite was deliberately skipped — the JSON file at
  `%LOCALAPPDATA%\Maestro\state.json` is plenty for personal use.

## Troubleshooting

- **"port 5000 already in use"** — another process holds it. Kill the
  conflicting process or override with `--urls http://localhost:5050`.
- **Agent output never streams** — check the health banner for missing CLIs;
  the agent process may have exited immediately.
- **Merge fails with "Could not checkout main"** — your source repo's main
  checkout has uncommitted changes or the base branch is checked out in
  another worktree. Resolve and retry.
- **State got wedged** — delete `%LOCALAPPDATA%\Maestro\state.json` and
  restart. Worktrees on disk under `<repo>\.maestro-worktrees\` can be
  removed with `git worktree remove --force` if needed.
