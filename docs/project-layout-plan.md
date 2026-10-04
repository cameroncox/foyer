# Foyer — Project Layout Plan

Oct 3, 2026 · @Cameron Cox

## Decisions

One repo, one container: a .NET 10 API that serves the built React app, split into two .NET projects plus tests.

| Area | Choice |
| --- | --- |
| Deploy shape | Single image; API serves Vite build from `wwwroot` |
| Backend | .NET 10 minimal API, EF Core + SQLite on `/data` |
| .NET split | `Foyer.Api` + `Foyer.Core` + test projects |
| Docker | Docker.DotNet.Enhanced, multi-endpoint, events + polling fallback |
| Live UI | Server-Sent Events |
| Frontend | React + Vite + TypeScript, npm |
| UI kit | Mantine |
| Data fetching | TanStack Query, invalidated by SSE |
| Drag and drop | dnd-kit (`@dnd-kit/core` + `@dnd-kit/sortable`) |
| API types | Generated from .NET OpenAPI doc with openapi-typescript |
| Repo + image | Gitea + Gitea container registry |
| CI | Gitea Actions on existing runners |

## Repo tree

Backend under `src/`, frontend under `web/`, tests under `tests/`; the Dockerfile builds both into one image.

```
foyer/
├── .gitea/workflows/
│   └── ci.yml              # checks on PR/push, then the release job (v* tags + develop)
├── src/
│   ├── Foyer.Api/          # host, endpoints, SSE, static files
│   └── Foyer.Core/         # domain, sync, data, docker
├── tests/
│   ├── Foyer.Core.Tests/
│   └── Foyer.Api.Tests/
├── web/                    # React + Vite + Mantine app
├── deploy/
│   ├── compose.example.yml
│   └── socket-proxy.example.yml
├── dev/
│   └── compose.dev.yml     # local socket proxy + test containers
├── docs/
├── Dockerfile
├── Foyer.slnx
├── Directory.Build.props   # shared TFM, nullable, analyzers
├── Directory.Packages.props # central package versions
├── global.json             # pin .NET 10 SDK
├── .editorconfig
├── .dockerignore
└── README.md
```

## .NET projects

`Foyer.Core` holds every rule and has no ASP.NET dependency; `Foyer.Api` is a thin host. The Sync folder stays pure (no EF, no Docker client) so the override and reset rules are unit-tested in isolation.

```
src/Foyer.Core/
├── Entities/        # Bookmark, Category, ContainerHealth, input + sync records
├── Sync/            # pure: labels -> desired state, merge w/ overrides
│   ├── LabelParser.cs        # coxdev.bookmark.* + homepage.* compat
│   ├── BookmarkReconciler.cs # create/update/dim/remove decisions
│   └── OverrideRules.cs      # category/tag lock, Reset to labels
├── Docker/          # Docker.DotNet.Enhanced wrappers
│   ├── DockerHostOptions.cs      # parsed from FOYER_DOCKERHOSTS_{KEY}_*
│   ├── ContainerSource.cs    # list + MonitorEvents, polling fallback
│   └── DockerSyncService.cs  # BackgroundService per endpoint
├── Data/
│   ├── FoyerDbContext.cs
│   ├── Configurations/       # EF fluent config
│   ├── Migrations/
│   └── Seed.cs               # Uncategorized
├── Services/        # BookmarkService, CategoryService, OrderingService
├── Events/          # IChangeNotifier (in-proc pub/sub for SSE)
├── Icons/           # icon fetch/cache (url or data: URI)
└── Import/          # Netscape bookmark HTML parser, folder -> category mapping, URL dedupe

src/Foyer.Api/
├── Program.cs
├── Endpoints/       # MapBookmarks, MapCategories, MapEvents (SSE), MapIcons, MapImport
├── Contracts/       # request/response DTOs (OpenAPI source)
├── Configuration/   # options binding, env var mapping
└── wwwroot/         # Vite build output (copied at image build)
```

Rules enforced in Core services, not endpoints:

- Docker bookmarks can't be deleted; endpoints return 409
- Uncategorized can't be renamed or deleted; deleting another category moves its bookmarks there
- Host tag (`#docker-4`) is computed, never stored as an editable tag
- Docker bookmark identity = host name (\_NAME, or key lowercased) + container name

## Frontend

Feature folders mirror the wireframes; one SSE hook invalidates TanStack Query caches so cards update without polling.

```
web/
├── src/
│   ├── main.tsx              # MantineProvider, QueryClientProvider
│   ├── App.tsx
│   ├── theme.ts              # Mantine theme, light/dark
│   ├── api/
│   │   ├── schema.d.ts       # generated, do not edit
│   │   ├── client.ts         # typed fetch wrapper
│   │   └── queries.ts        # useBookmarks, useCategories, mutations
│   ├── hooks/
│   │   ├── useLiveUpdates.ts # EventSource -> queryClient.invalidate
│   │   └── useEditMode.ts
│   ├── features/
│   │   ├── board/            # main page: CategorySection, BookmarkCard, StatusDot
│   │   ├── search/           # search bar + filtering
│   │   ├── edit-mode/        # DragDropContext, CategoryDrawer
│   │   ├── bookmark-form/    # Add popover, Edit manual, Edit Docker
│   │   ├── empty-state/
│   │   └── import/           # file pick, preview tree w/ checkboxes, confirm
│   └── components/           # shared bits (TagChip, IconImage)
├── index.html
├── vite.config.ts            # /api proxy to localhost API in dev
├── tsconfig.json
├── eslint.config.js
└── package.json
```

- `npm run gen:api` pulls `/openapi/v1.json` from the running API and regenerates `schema.d.ts`; CI fails if the committed file is stale
- dnd-kit: one `SortableContext` per category for cards (grid strategy, so edit mode keeps the grid; cards can move between categories) and one for the drawer's category list; drags start from the handle only
- Vite builds to `web/dist`; the Dockerfile copies it into `Foyer.Api/wwwroot`

## Tests and tooling

Most test weight goes on `Foyer.Core.Tests`, since the label/override rules are where bugs would hide.

| Project | Framework | Covers |
| --- | --- | --- |
| Foyer.Core.Tests | xUnit + Shouldly | FOYER\_\* env parsing, LabelParser, homepage.\* compat, reconciler, override lock + reset, ordering, Uncategorized rules, import (nesting, name match, dedupe, root folders) against Chrome/Firefox export fixtures |
| Foyer.Api.Tests | xUnit + WebApplicationFactory, in-memory SQLite | endpoint contracts, 409 on Docker delete, SSE emits on change, import preview saves nothing |
| web | Vitest + Testing Library | forms, edit-mode toggles, search filter |

Tooling:

- `.editorconfig` + `dotnet format` check in CI
- `Directory.Build.props`: nullable on, warnings as errors, .NET analyzers
- Central package management via `Directory.Packages.props`
- ESLint + Prettier + `tsc --noEmit` for web
- Optional later: Playwright smoke test against the built image

Docker integration tests use Testcontainers: start a socket proxy (CONTAINERS + EVENTS) and labeled containers, then run `ContainerSource` and `DockerSyncService` against them. Tag them `Category=Integration` so they can be filtered locally; the act runners already have Docker access, so they run in CI as-is.

## Docker and local dev

One multi-stage Dockerfile; local dev runs API and Vite natively, with a compose file supplying a socket proxy and labeled test containers.

Dockerfile stages:

1. `node` — `npm ci`, `npm run build` in `web/`
2. `sdk` — restore, publish `Foyer.Api`, copy `web/dist` into `wwwroot`
3. `aspnet` runtime (chiseled/non-root) — `/data` volume, port 8080, healthcheck on `/healthz`

Config via env vars:

- `FOYER_DOCKERHOSTS_{KEY}_URI` — required per host (`unix://`, `tcp://`, `http://`)
- `FOYER_DOCKERHOSTS_{KEY}_NAME` — optional, defaults to `KEY` lowercased
- `FOYER_HOMEPAGE_LABELS` (`true`), `FOYER_RESYNC_INTERVAL` (`300`), `FOYER_POLL_INTERVAL` (`30`), `FOYER_DATA_DIR` (`/data`, SQLite + icon cache)
- Parsed by Foyer itself, not ASP.NET's `__` env provider; a `_NAME` with no `_URI` fails startup naming the key

Local dev loop:

- `docker compose -f dev/compose.dev.yml up` — socket proxy (CONTAINERS + EVENTS) + a few `whoami` containers with `coxdev.bookmark.*` and `homepage.*` labels
- `dotnet watch --project src/Foyer.Api` on :5080
- `npm run dev` in `web/` on :5173, proxying `/api` to :5080

`deploy/` holds the example compose for docker-1 (local socket via proxy) and the socket-proxy snippet for docker-2/3/4.

## CI (Gitea Actions)

One workflow, `ci.yml`: its check jobs gate every push and PR, and its `release` job runs only after all of them pass, pushing images to the Gitea registry for version tags and the develop branch.

`ci.yml` (push, pull\_request) — two parallel jobs:

- **dotnet**: setup .NET 10, restore (cache NuGet), `dotnet format --verify-no-changes`, build, test
- **web**: setup Node, `npm ci` (cache npm), lint, `tsc --noEmit`, Vitest, `vite build`, check generated `schema.d.ts` is current

The `release` job (tags `v*`, pushes to `develop`; `needs` the dotnet, web and api-contract jobs) builds with `docker buildx` and pushes to `gitea.lan.casadecox.org/foyer/foyer`:

| Trigger | Image tags |
| --- | --- |
| Tag `vX.Y.Z` | `:X.Y.Z`, `:latest` |
| Push to `develop` | `:dev`, `:<7-char git sha>` |
| PRs, other branches | none (`ci.yml` only) |

The act runners have Docker access, so Testcontainers and `docker buildx` run directly on them; no Docker-in-Docker setup is needed.

Notes:

- `:latest` tracks the last version tag pushed, never `develop`
- `:<sha>` lets a dev deployment roll back to any earlier `develop` build
- Registry auth uses a Gitea token stored as a repo secret

## Build order

Build the data model and pure sync rules first; UI comes after the API contract is stable enough to generate types from.

1. **Skeleton** — solution, props files, empty projects, Dockerfile, `ci.yml` green on an empty build
2. **Data** — Domain entities, DbContext, first migration, Uncategorized seed, Category/Bookmark services + tests
3. **Sync core** — LabelParser (incl. `homepage.*` compat), reconciler, override lock + Reset to labels; fully unit-tested, no Docker yet
4. **Docker** — host config from FOYER\_\* env, ContainerSource with events + polling fallback, DockerSyncService; `Testcontainers integration tests`
5. **API** — CRUD + reorder endpoints, OpenAPI, SSE `/api/events`, icon proxy/cache, import preview + commit, `/healthz`
6. **Web: view mode** — generated client, board, cards, status dots, host tags, search, empty state, live updates
7. **Web: edit** — FAB add popover, edit manual/Docker forms, edit mode with dnd + category drawer, import flow from the drawer / Categories sheet
8. **Release** — `release` job in `ci.yml`, deploy example, run on docker-1 at `foyer.lan.casadecox.org`
9. **Later** — dark mode polish, phone layout, Playwright smoke test

Each step ends with CI green and something runnable.
