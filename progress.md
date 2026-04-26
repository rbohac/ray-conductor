# Maestro build progress

## Phase 1 — Skeleton wired together (DONE)

Bootstrapped `.NET 10` Web API at `api/` (controllers, OpenAPI dev-time, no
HTTPS redirect, no auth) bound to `http://localhost:5000` and serving the SPA
out of `wwwroot/` with `MapFallbackToFile("index.html")` for client-side
routing. Bootstrapped `Vite + React 19 + TypeScript (strict)` SPA at `web/`
with Tailwind 3, React Router v6, `lucide-react`, and `sonner`. Vite dev
server proxies `/api` and `/events` to the .NET API. `vite build` outputs
straight into `api/wwwroot/`, and an MSBuild target `BuildWebFrontendOnPublish`
runs `npm install && npm run build` before `dotnet publish`. The csproj is
configured for `PublishSingleFile=true / SelfContained=true / RuntimeIdentifier=win-x64`
producing a ~50 MB `Maestro.exe`. `Program.cs` auto-launches the default
browser at startup in non-Development environments. Verified: `dotnet run`
serves the "Hello Maestro" landing page and `/api/health` returns 200,
`dotnet publish -c Release` cross-compiles to a working `Maestro.exe`,
`tsc -b` and `npm run lint` are both clean.

## Phase 2 — Repos & workspaces with persistence (DONE)

Domain models and DTOs (`Repo`, `Workspace`, `Agent` plus `*Dto`) live under
`api/Models/`. `GitService` shells out to `git` via `Process` and exposes
`IsGitRepoAsync`, `GetDefaultBranchAsync`, `WorktreeAddAsync`,
`WorktreeRemoveAsync`, `BranchDeleteAsync`, `CheckoutAsync`,
`MergeNoFfAsync`, `DiffAsync`, `VersionAsync`. `WorktreeService` wraps
worktree lifecycle (create + remove + branch deletion). `WorkspaceStore` is a
DI singleton that holds in-memory state for repos and workspaces, mirrors to
`%LOCALAPPDATA%\Maestro\state.json` after every mutation (atomic via
write-tmp-then-rename), and on load marks any agents that were running in a
prior process as `orphaned`. `ReposController` and `WorkspacesController`
expose CRUD with sane validation (rejects non-existent paths and
non-git-directories, blocks repo deletion while it still has workspaces).
`Program.cs` registers the services and configures `AddJsonOptions` with
`JsonNamingPolicy.CamelCase` and `JsonStringEnumConverter`. Frontend gained
`api/client.ts` (typed fetch wrapper with `ApiError`),
`api/types.ts` (mirrors DTOs), `WorkspacesProvider` context +
`useWorkspaces` hook, a global `Header` with the kill-switch "Stop all"
button, a workspaces grid (`WorkspacesPage`) with a workspace-card layout,
status badges, and skeleton loaders, a `ReposPage` with add/delete + empty
state, a `NewWorkspaceModal` mounted on demand with derived branch/base
name, and a stub `WorkspaceDetailPage` that Phases 3–4 fill in. Routes:
`/`, `/repos`, `/workspaces/:id`, fallback to `/`. `Ctrl+N` opens the
new-workspace modal; `Esc` closes any modal. Verified end-to-end: register
real local repo, create a workspace (worktree appears at
`<repo>/.maestro-worktrees/<id>`, branch exists), restart API and confirm
state persists, delete workspace removes worktree and branch, delete repo
returns 204 once empty.

## Phase 3 — Agent process spawning + SSE streaming (DONE)

`EventBus` is an in-process pub/sub keyed by workspace id; subscribers get a
bounded channel (4096 capacity, `DropOldest`) so a slow client never stalls
the producers. `AgentProcessService` is registered both as a singleton DI
service and as `IHostedService` so `StopAsync` runs at app shutdown and kills
every child process tree (`taskkill /T /F /PID` on Windows,
`Process.Kill(entireProcessTree:true)` elsewhere). Each agent spawns
`claude --dangerously-skip-permissions` or `codex --yolo` (overridable via
`Maestro:ClaudeCommand` / `Maestro:CodexCommand` config keys for testing) in
the workspace's worktree, with `--model X` if requested. stdout/stderr lines
are pushed to the bus as `agent-output` events; the `Process.Exited` handler
publishes a final `agent-status` event and decides between `completed`,
`errored`, or `stopped` by checking whether the agent was already removed
from `_running` (intentional kill ⇒ `stopped`). SSE infrastructure lives in
`api/Sse/SseWriter.cs` (header setup, event/comment writers).
`EventsController.GetWorkspaceStream` opens an SSE connection on
`/api/events/workspace/{id}` with named events, a 15-second `: heartbeat`
keep-alive, and proper `RequestAborted` cancellation. `AgentsController`
exposes `POST /api/workspaces/{id}/agents`, follow-up
`POST .../messages` (writes to stdin), `DELETE` per-agent stop, and the
global `POST /api/agents/stop-all` kill switch. `WorkspacesController.Delete`
stops any still-running agents before removing the worktree. Frontend gained
a `useSse` hook (browser `EventSource`, named-event subscription, ref-based
handlers), an `AgentOutputPanel` (monospace, dark, autoscroll with
pause-on-scroll-up + resume button, in-pane stop button, follow-up stdin
input), and a `NewAgentForm` (Claude/Codex provider toggle, model dropdown,
remembered per-provider in `localStorage`). `WorkspaceDetailPage` now
subscribes to its workspace's SSE stream, accumulates output lines (capped
at 5000), surfaces terminal status events as synthetic `[agent <state>]`
lines, and shows a live/offline indicator. Verified: clean spawn streams
output and ends `completed`/exit 0; mid-flight DELETE marks `stopped`
and process is gone from `ps`; `POST /api/agents/stop-all` kills every
running agent across all workspaces; clean API shutdown via SIGTERM kills
children with no zombies; SIGKILL of the API followed by restart marks the
prior agent `orphaned` and the workspace `idle` (not stuck "running").
