# Maestro

Personal Conductor-style orchestrator for running parallel Claude Code / Codex
agents in isolated git worktrees. Single-port, single-binary Windows app.

## Stack

- Backend: .NET 10 ASP.NET Core (controllers, OpenAPI, ILogger)
- Frontend: Vite + React 19 + TypeScript (strict) + Tailwind 3
- Real-time: Server-Sent Events
- Persistence: JSON file at `%LOCALAPPDATA%\Maestro\state.json`
- Packaging: `dotnet publish` -> single self-contained `Maestro.exe` (~50 MB)

## Daily use (after install)

Double-click `Maestro.exe`. The default browser opens to
`http://localhost:5000`. Register a local git repo, create a workspace, spawn
a Claude Code or Codex agent inside the worktree, watch the output stream, see
the diff update live, merge when ready.

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

Open `http://localhost:5173` for development with HMR.

## Producing a release `.exe`

```sh
cd api
dotnet publish -c Release -o ../publish
```

The Vite frontend is built automatically as part of the publish target. Output
is `publish/Maestro.exe` plus auxiliary `appsettings.json` / `wwwroot/`.

## Updating the underlying CLIs

Maestro shells out to `claude` and `codex` from your PATH. Update them as you
normally would (`npm i -g @anthropic-ai/claude-code` and the equivalent for
Codex). On startup Maestro runs `claude --version` / `codex --version` and
shows a banner if either is missing.

## Known limitations (v1)

- Windows-only release target (`win-x64`).
- Agents run with `--dangerously-skip-permissions` (Claude) / `--yolo` (Codex)
  for fully-autonomous v1; the worktree itself is the isolation boundary.
- No interactive approval forwarding through SSE.
- Local merge only. No GitHub PR integration.
