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
