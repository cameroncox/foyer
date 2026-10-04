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
| `docs/` | Spec, project layout plan and wireframes |
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
mkdir -p data
docker run -p 8080:8080 --user "$(id -u):$(id -g)" -v "$PWD/data:/data" foyer
```

The runtime image is chiseled and non-root (uid 1654 unless `--user` / compose `user:` overrides
it; `./data` must be writable by that user). Its healthcheck runs
`dotnet Foyer.Api.dll --healthcheck`, which probes `/healthz`.

## Releases

The `release` job in `.gitea/workflows/ci.yml` runs after every check job passes and pushes
images to `<REGISTRY>/<owner>/<repo>`, where `REGISTRY` is a repo variable (Settings › Actions ›
Variables) holding the Gitea registry's host:

| Trigger | Tags |
| --- | --- |
| Tag `vX.Y.Z` | `:X.Y.Z`, `:latest` |
| Push to `develop` | `:dev`, `:<7-char sha>` |

It needs repo secrets `REGISTRY_USERNAME` and `REGISTRY_TOKEN` (a Gitea access token that can
write packages for the image's owner). The image logs its version at startup.

## Deploy

`deploy/compose.example.yml` runs Foyer behind Traefik, reading its own host through a private,
unpublished socket proxy. `deploy/socket-proxy.example.yml` is the read-only proxy for each other
host Foyer reads; firewall its port to Foyer's host (see the file: ufw doesn't filter Docker's
published ports). Fill in the `[BRACKETED]` values in both first.

## Bookmarklet

In edit mode, the Categories drawer has an **Add to Foyer** link. Drag it to the browser's
bookmarks bar; clicking it on any page opens a small popup with the Add form filled in from
that page's URL and title, and closes it once saved. Pick a category above the link to have the
form start there; each category can have its own link. The link points at the address Foyer was
opened on, so drag it from the address you'll use day to day. It isn't offered on phones, where
bookmarklets don't see the page they're run from.

## Configuration

Environment variables only. See the [spec](docs/spec.md) for the full list; the essentials:

Docker hosts are optional: with no `FOYER_DOCKERHOSTS_{KEY}_URI` set, Foyer runs as a plain
bookmark manager with manually added bookmarks only.

| Variable | Default | Purpose |
| --- | --- | --- |
| `FOYER_DOCKERHOSTS_{KEY}_URI` | — | Docker endpoint per host (`unix://`, `tcp://`, `http://`) |
| `FOYER_DOCKERHOSTS_{KEY}_NAME` | `KEY` lowercased | Display name and host tag |
| `FOYER_HOMEPAGE_LABELS` | `true` | Fall back to `homepage.*` labels |
| `FOYER_RESYNC_INTERVAL` | `300` | Seconds between full resyncs |
| `FOYER_POLL_INTERVAL` | `30` | Poll interval when a host's event stream is down |
| `FOYER_DATA_DIR` | `/data` | SQLite file and icon cache |
| `FOYER_TITLE` | `Foyer` | Name in the top bar and the browser tab (up to 60 characters) |
| `FOYER_SEARCH_URL` | `https://duckduckgo.com/?q=` | The spotlight's web search; the query replaces `%s`, or is appended. Bangs (`!g …`) go straight to DuckDuckGo |

## Built with AI

Foyer was made with the help of AI. The spec, project layout plan and wireframes in `docs/` were
drafted with Claude (Anthropic), and much of the code was written with Claude Code, working from
those documents alongside the maintainer.
