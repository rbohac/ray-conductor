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
