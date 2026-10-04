# Foyer

A self-hosted bookmark dashboard that replaces gethomepage.dev. Bookmarks live in
ordered categories and come from two sources: Docker containers labeled
`coxdev.bookmark.*` (or the existing `homepage.*` labels) across several hosts, and
bookmarks added by hand.

One container: a .NET 10 minimal API that also serves the React app.

## Layout

| Path | What |
| --- | --- |
| `src/Foyer.Api` | Host: endpoints, SSE, static files |
| `src/Foyer.Core` | Domain, sync rules, data, Docker; no ASP.NET dependency |
| `tests/` | xUnit v3 + Shouldly |
| `web/` | React + Vite + TypeScript + Mantine |
| `.gitea/workflows` | CI |

## Local dev

Requires the .NET 10 SDK and Node 22.

```bash
docker compose -f dev/compose.dev.yml up -d   # socket proxy on :2375 + labeled test containers
```

```bash
dotnet watch --project src/Foyer.Api     # API on :5080, syncing the "dev" host
```

```bash
cd web && npm install && npm run dev     # UI on :5173, proxies /api to :5080
```

Checks, as CI runs them:

```bash
dotnet format Foyer.slnx --verify-no-changes && dotnet build Foyer.slnx && dotnet test
```

```bash
cd web && npm run lint && npm run format:check && npm run typecheck && npm test && npm run build
```

Integration tests (`Category=Integration`) start socket proxies and containers with
Testcontainers, so they need a local Docker socket. They run in CI as-is. To skip them:

```bash
dotnet test -- --filter-not-trait "Category=Integration"
```

With rootless podman instead of Docker, point Testcontainers and compose at the podman socket:

```bash
export DOCKER_HOST=unix:///run/user/$UID/podman/podman.sock DOCKER_SOCKET=/run/user/$UID/podman/podman.sock TESTCONTAINERS_RYUK_DISABLED=true
```

Podman's Docker-compatible API leaves health out of container listings, so health dots only
show against real Docker.

Regenerate API types (API must be running): `cd web && npm run gen:api`.

Migrations live in `src/Foyer.Core/Data/Migrations` and are applied at startup. In dev the
database goes to `./data` (gitignored). To add one:

```bash
dotnet tool restore && dotnet ef migrations add <Name> -p src/Foyer.Core -o Data/Migrations
```

## Image

```bash
docker build -t foyer .
docker run -p 8080:8080 -v foyer-data:/data foyer
```

The runtime image is chiseled and non-root; its healthcheck runs
`dotnet Foyer.Api.dll --healthcheck`, which probes `/healthz`.

## Configuration

Environment variables only. See the spec for the full list; the essentials:

| Variable | Default | Purpose |
| --- | --- | --- |
| `FOYER_DOCKERHOSTS_{KEY}_URI` | — | Docker endpoint per host (`unix://`, `tcp://`, `http://`) |
| `FOYER_DOCKERHOSTS_{KEY}_NAME` | `KEY` lowercased | Display name and host tag |
| `FOYER_HOMEPAGE_LABELS` | `true` | Fall back to `homepage.*` labels |
| `FOYER_RESYNC_INTERVAL` | `300` | Seconds between full resyncs |
| `FOYER_POLL_INTERVAL` | `30` | Poll interval when a host's event stream is down |
| `FOYER_DATA_DIR` | `/data` | SQLite file and icon cache |
