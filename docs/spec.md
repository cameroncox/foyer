# Foyer Spec

Oct 3, 2026 · @Cameron Cox

## Overview

Foyer is a self-hosted bookmark dashboard that replaces gethomepage.dev. It shows bookmarks in ordered categories, built from two sources: Docker containers labeled for it across several hosts, and bookmarks added by hand.

Goals:

- A simple front end: a search bar, categories, and cards. No widgets.
- Zero-touch discovery of labeled containers on all four Docker hosts, with live status.
- Full control of order, categories, and tags from the UI, including for Docker bookmarks.
- A drop-in switch from homepage: existing `homepage.*` labels work without changes.

Wireframes: [Foyer Wireframes](https://claude.ai/artifact/KrNkFk2svbJzGv383p8wQT) (12 artboards, desktop and phone).

## Architecture and deployment

Foyer ships as one container: an ASP.NET Core (.NET 10) minimal API that also serves the built React app as static files.

| Layer | Choice | Notes |
| --- | --- | --- |
| Backend | ASP.NET Core minimal API, .NET 10 | Docker sync runs as hosted background services |
| Docker client | Docker.DotNet.Enhanced | One client per configured host |
| Storage | SQLite via EF Core | File on a `/data` volume, migrations applied at startup |
| Icon cache | Files under `/data/icons` | Served by the API |
| Frontend | React + Vite, Mantine | Built into `wwwroot` at image build time |
| Drag and drop | dnd-kit | Handle-only drag on touch |
| Auth | None in-app | Runs behind Traefik with Tinyauth forwardAuth |

Deployment target: docker-1, routed by the central Traefik. The image is a multi-stage build (Node for the frontend, .NET SDK for the API, ASP.NET runtime for the final stage).

&#91;embedded content: Foyer architecture · 4 hosts, 1 container\]

The sync service is the only part that talks to Docker; the page only ever talks to Foyer's API, through Traefik.

## Configuration

Foyer is configured through environment variables only; bookmarks, categories and tags are the only things managed in the UI.

| Variable | Default | Purpose |
| --- | --- | --- |
| `FOYER_DOCKERHOSTS_{KEY}_URI` | none | Docker endpoint for the host named by `KEY`: `unix://`, `tcp://` or `http://` |
| `FOYER_DOCKERHOSTS_{KEY}_NAME` | `KEY` in lowercase | Optional display name and host tag, for names with hyphens like `docker-1` |
| `FOYER_HOMEPAGE_LABELS` | `true` | Read `homepage.*` labels as a fallback |
| `FOYER_RESYNC_INTERVAL` | `300` | Seconds between full resyncs |
| `FOYER_POLL_INTERVAL` | `30` | Seconds between polls for a host whose event stream is down |
| `FOYER_DATA_DIR` | `/data` | Location of the SQLite file and icon cache |
| `FOYER_TITLE` | `Foyer` | Name in the top bar and the browser tab, up to 60 characters |
| `FOYER_SEARCH_URL` | `https://duckduckgo.com/?q=` | The spotlight's web search. The query replaces `%s`, or is appended when there's none; anything but an http(s) URL stops startup |

- `KEY` is any run of letters and digits (`DOCKER1`, `NAS`, `PVE2`). Env var names can't hold hyphens, which is why `_NAME` exists.
- Each host needs a `_URI`. A `_NAME` with no `_URI` is a startup error that names the key ("DOCKER3 has no URI").
- Removing a host means deleting its lines; nothing else renumbers.
- No hosts configured is valid: Foyer runs with manual bookmarks only.
- These are parsed by Foyer itself, since ASP.NET's built-in environment provider expects `__` separators. The pattern matches Traefik's named env config (`TRAEFIK_ENTRYPOINTS_WEB_ADDRESS`).

## Docker discovery

Foyer connects to each Docker host directly, so it sees every labeled container no matter which host runs it.

Hosts come from the `FOYER_DOCKERHOSTS_{KEY}_*` variables (see Configuration). The name is what Foyer shows and tags with; the Docker API does not report which host a container is on.

```bash
FOYER_DOCKERHOSTS_DOCKER1_NAME=docker-1
FOYER_DOCKERHOSTS_DOCKER1_URI=unix:///var/run/docker.sock
FOYER_DOCKERHOSTS_DOCKER2_NAME=docker-2
FOYER_DOCKERHOSTS_DOCKER2_URI=http://192.0.2.22:2375
FOYER_DOCKERHOSTS_DOCKER3_NAME=docker-3
FOYER_DOCKERHOSTS_DOCKER3_URI=http://192.0.2.23:2375
FOYER_DOCKERHOSTS_DOCKER4_NAME=docker-4
FOYER_DOCKERHOSTS_DOCKER4_URI=http://192.0.2.37:2375
```

- **Socket proxies:** docker-2, 3 and 4 run docker-socket-proxy with `CONTAINERS=1`; `EVENTS` is allowed (verified on docker-2). `POST` stays off; Foyer never writes. docker-1 should use a proxy too rather than the raw socket.
- **Startup:** list containers on every host and reconcile with the database.
- **Live updates:** one event stream per host, filtered at the API to `type=container` and `event=start,stop,die,destroy,rename,pause,unpause,health_status`. Healthcheck `exec_*` events are dropped.
- **Safety net:** a full resync every few minutes.
- **Fallback:** if a host's event stream fails, poll `/containers/json` every 30 seconds for that host until events recover.
- **Host outages:** reconnect with backoff. A host that is unreachable keeps its bookmarks on the page until it returns and resyncs.

## Labels

Foyer reads its own `coxdev.bookmark.*` labels and falls back to `homepage.*` labels field by field, so existing containers work unchanged.

| Field | Foyer label | homepage fallback |
| --- | --- | --- |
| Enabled | `coxdev.bookmark.enabled=true` | `homepage.href` present |
| Name | `coxdev.bookmark.name` | `homepage.name` |
| Category | `coxdev.bookmark.category` | `homepage.group` |
| URL | `coxdev.bookmark.url` | `homepage.href` |
| Icon | `coxdev.bookmark.icon` | `homepage.icon` |
| Tags | `coxdev.bookmark.tags` (comma-separated) | none |

- `coxdev.*` wins over `homepage.*` when both set a field.
- `coxdev.bookmark.enabled=false` keeps a container off the page even when it has `homepage.*` labels.
- No name label → the container name. No category label → Uncategorized.
- Other `homepage.*` labels (`homepage.widget.*`, `homepage.description`) are ignored.
- The homepage fallback is on by default; `FOYER_HOMEPAGE_LABELS=false` turns it off.

## Data model

Three tables hold everything the server stores; display settings never reach the server.

**Category**

| Column | Type | Notes |
| --- | --- | --- |
| Id | int |  |
| Name | string, unique |  |
| SortOrder | int | Drawer order |
| IsSystem | bool | True only for Uncategorized |

**Bookmark**

| Column | Type | Notes |
| --- | --- | --- |
| Id | int |  |
| Source | Manual \| Docker |  |
| Name, Url, Icon | string | For Docker: the latest values from labels |
| CategoryId | int |  |
| SortOrder | int | Position within its category |
| DockerHost | string? | Config `Name` of the host |
| ContainerName | string? | Identity for Docker bookmarks, with DockerHost |
| ContainerState | string? | running, exited, paused, restarting, … |
| Health | string? | healthy, unhealthy, starting, or none |
| IsPresent | bool | False once the container is removed or unlabeled |
| LabelTags | string\[\] | From `coxdev.bookmark.tags` |
| CategoryOverridden, TagsOverridden | bool | Set when edited in the UI |
| CreatedAt | timestamp |  |

**BookmarkTag**: user-added tags per bookmark (BookmarkId, Tag).

- Docker bookmarks are keyed by **host + container name**, never container ID, because IDs change on every recreate.
- A new bookmark gets `SortOrder = max + 1` in its category, so the default order is the order it was entered or first seen.
- Uncategorized is seeded by the first migration, can't be renamed or deleted, and always sorts last.
- The host tag (`#docker-4`) is computed from DockerHost, not stored as a tag.
- Deleting a category moves its bookmarks to Uncategorized, appended in their existing order.

## Ownership and lifecycle

Labels own a Docker bookmark's name, URL and icon; its category and tags start from labels but belong to the UI once you change them.

| Field | Docker bookmark | Manual bookmark |
| --- | --- | --- |
| Name, URL, icon | Labels only, read-only in the UI | Editable |
| Category | Label until edited, then the UI | Editable |
| Tags | Label tags until edited, then the UI | Editable |
| Host tag | Automatic, can't be removed | None |
| Position | UI | UI |
| Delete | Not possible | Yes |

- **Until edited**, a label change updates the card. **After an edit**, later label changes to that field are ignored.
- **Reset to labels** in the Docker edit popover clears both overrides and re-applies the current labels.
- When tags are overridden, the saved set is the full list, so removing a label tag sticks.

Container lifecycle:

| Container | On the page | Stored record |
| --- | --- | --- |
| Running | Card with a green dot | Kept |
| Unhealthy, starting, restarting or paused | Card with a yellow dot | Kept |
| Stopped or exited | Dimmed card with a red dot | Kept |
| Removed (`compose down`, `docker rm`) | Gone | Kept, `IsPresent = false` |
| Label removed or `enabled=false` | Gone | Kept, `IsPresent = false` |
| Comes back | Returns in its old spot | Category, tags and position intact |

Docker bookmarks can't be deleted or hidden from the UI. Only the container or its labels take one off the page.

## Icons

The backend resolves every icon, fetches it once and serves it from its own cache, so the browser never loads icons from other hosts.

| Icon value | Resolved from |
| --- | --- |
| Bare name: `jellyfin.svg`, `filebrowser.png` | dashboard-icons CDN |
| `mdi-…` | Material Design Icons |
| `si-…` | Simple Icons |
| `http(s)://…` | That URL |
| `data:image/…;base64,…` | Decoded directly, no fetch |
| Empty | The bookmark URL's favicon |

- Cached files live under `/data/icons`, keyed by a hash of the icon value.
- Anything that fails to resolve falls back to a letter tile with the bookmark's first letter. The icon endpoint answers it with an empty 204, cached for the hour before a retry, so browsers don't log a failed load.
- The Add and Edit forms show a live preview of the resolved icon.

## Main page

The main page is a top bar, then categories in drawer order, each a grid of bookmark cards.

**Top bar:** the instance name (FOYER\_TITLE, default Foyer, also the browser tab's title), a centered search box (`/` focuses it), the Light / Dark / Auto toggle with the accent swatches, and **Edit**. A floating **+** in the bottom-right adds a bookmark from any mode.

**Cards:**

- Icon, then the name top-aligned with it, then a row of tags under the name.
- No URL on the card; hovering shows the URL and status as a tooltip.
- Docker cards show a status dot on the icon's corner: green running, yellow unhealthy/starting/restarting/paused, red stopped. Stopped cards are also dimmed. Manual cards have no dot.
- Tags: the automatic host tag is filled with a Docker icon; label and user tags are outlined.
- Clicking a card opens its URL.

**Categories:** shown in drawer order. A category with no visible bookmarks is hidden, Uncategorized included.

**Search:**

- Category headers disappear; matches fill one flat grid with a count line ("4 bookmarks match 'arr'").
- Matches name, tags (including the host tag), category and URL, case-insensitive.
- Name matches come first, then other matches; each group keeps page order.
- The tag that matched is highlighted on the card.
- **Enter** opens the first result, which has a focus ring. **Esc** clears the search.
- No matches: "Nothing matches 'xyz'" with an **Add bookmark** button that opens Add with the name pre-filled.

**Spotlight** (Space or ⌘K, outside edit mode and forms):

- Empty, it offers Docker hosts, tags and categories to narrow by, each with its bookmark count. Picking one shows that filter as a chip; Backspace in the empty box clears it.
- Typing searches bookmarks (and matching filters). Enter opens the highlighted bookmark in a new tab.
- A web search row comes last: "Search DuckDuckGo for '…'" (the engine from `FOYER_SEARCH_URL`, named when known, else by its host). With no other match it is the only row, so Enter searches the web.
- On DuckDuckGo, a query starting with a bang (`!g current c# standard`) is only that search, and DuckDuckGo resolves the bang. Other engines search the text as typed.
- No web search inside a filter: that searches bookmarks only.

**Empty state:** a dashed container with "No bookmarks yet", one line about the `coxdev.bookmark.enabled=true` label, and an **Add bookmark** button that opens the Add popover.

## Editing on desktop

Adding works from any mode; everything else that changes the page needs **Edit** first.

**Add bookmark** (the floating **+**, any mode): a popover above the button, which turns into an ×.

- Fields: Name, Category, URL, Tags, Icon with live preview.
- Category defaults to Uncategorized. Choosing "New category…" shows a name field; the category is created on save, at the end of the list.

**Edit mode** (**Edit** → **Done**):

- A blue banner explains the mode. Every card gets a drag handle.
- Drag reorders within a category, or drops a card into another category. This works for Docker cards too.
- Manual cards: pencil and delete buttons. Docker cards: the host tag and a pencil button.
- **Select** (above the cards) deletes several bookmarks at once. Manual cards become checkboxes; Docker cards can't be picked, since they follow their container. Each category heading selects all its manual bookmarks. A bar under the top bar shows the count with **Cancel** and **Delete**, confirmed first. Nothing drags while selecting.
- Changes save as they happen; **Done** just leaves edit mode.

**Edit popovers** (open above the **+** button; the clicked card gets a highlight ring):

- Manual: the same fields as Add, plus **Delete**, **Cancel**, **Save**.
- Docker: a locked panel showing name, URL and icon from labels, and which container and host it comes from. Only Category and Tags are editable. Buttons: **Reset to labels**, **Cancel**, **Save**. No Delete.

**Category drawer** (right side, open for the whole of edit mode):

- Rows in order, each with a drag handle, inline rename, bookmark count and delete.
- Delete confirms inline: "Delete 'Downloads'? Its 2 bookmarks move to Uncategorized."
- Uncategorized is pinned last, with no handle, rename or delete.
- A "New category" field and **Add** button at the bottom.

## Phone

On a phone the top bar collapses to a menu, and editing becomes tap-to-edit with handles for reordering.

**Browsing:**

- Top bar: the instance name and a menu icon. Cards run in a single column with the same dots and tags as desktop.
- The floating **+** stays for adding; Add is in the menu too.
- The menu drops down from the top bar over a dimmed page: search, **Add bookmark** and **Edit page** side by side, and the Light / Dark / Auto control with the accent swatches.
- Cards filter live under the menu as you type; Enter or × closes it. An active search stays in the top bar with a clear button, so it's plain the page is filtered.

**Edit mode:**

- The top bar becomes "Editing" with **Categories** and **Done** buttons, under a one-line hint.
- Each card has a large grip handle on its right edge. Only the handle starts a drag, so the page still scrolls.
- Drag works as on desktop, into other categories too; changing Category in the edit sheet also moves a card.
- Tapping a card opens its edit sheet.
- **Select** works as on desktop; the hint then reads "Tap cards to pick them."

**Edit sheet:** a bottom sheet with the same fields and rules as the desktop popovers (manual and Docker). Inputs use 16px text so iOS doesn't zoom. **Delete** and **Save** are full-width at the bottom.

**Categories sheet:** a full-screen page with a back arrow. Same rows as the desktop drawer, Uncategorized pinned last, and the "New category" field fixed to the bottom so it stays above the keyboard.

## Importing browser bookmarks

Foyer imports the Netscape bookmark HTML file that Chrome, Firefox, Edge and Safari all export, after a preview where you pick what comes in.

| In the export | Becomes |
| --- | --- |
| Folder | Category, named after the bookmark's closest folder |
| Bookmark | Manual bookmark |
| `ICON` attribute | Icon, kept as a `data:` URI |
| `TAGS` attribute (Firefox) | Tags |
| Order within a folder | Order within the category |

- **Entry point:** an **Import** button at the bottom of the category drawer (desktop) or the Categories sheet (phone), in edit mode.
- **Preview:** the folder tree with a checkbox and bookmark count per folder, plus how many duplicates will be skipped. Nothing is saved until you confirm.
- **Nested folders:** a bookmark goes to its closest folder. `Homelab/Network/OPNsense` lands in "Network".
- **Name matches:** a folder whose name matches an existing category (case-insensitive) appends to it. Two folders with the same name merge into one category.
- **New categories** go at the end of the list, in the order they appear in the file.
- **Duplicates:** a bookmark whose URL already exists in Foyer is skipped.
- **No folder:** the browser's own root folders (Bookmarks bar, Other bookmarks, Bookmarks Menu) show as section headers, never categories. Bookmarks sitting directly in them appear as "Not in a folder" and go to Uncategorized.
- **API:** `POST /api/import/preview` and `POST /api/import` (see API sketch).

## Theme

The theme is Mantine's: a color scheme plus one accent picked from the 26 presets on the [Mantine colors generator](https://mantine.dev/colors-generator/), default Deep blue.

- **Accent:** the 26 named presets (Blue gray through Red) ship as hard-coded 10-shade tuples and become `primaryColor` in `createTheme()`. No custom color picker.
- **Color scheme:** Light / Dark / Auto through Mantine's `useMantineColorScheme`.
- **Picker:** the theme button opens a popover with the scheme control on top and a 7-per-row grid of swatches below, each named on hover. On a phone, the swatches sit under the scheme control in the menu.
- **Storage:** both choices are per device, in localStorage. The server stores nothing about display. A new device starts at Deep blue and Auto.
- **No flash:** the saved accent is read before the first render, the same way Mantine handles the color scheme.
- **Status dots don't follow the accent.** Red, yellow and green are fixed in both schemes.

## API sketch

A small JSON API under `/api`, plus one server-sent event stream that pushes changes to open pages.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/dashboard` | Categories in order, each with its present bookmarks in order |
| GET | `/api/events` | Server-sent events: `bookmarks-changed` when Docker or another client changes data |
| POST | `/api/bookmarks` | Add a manual bookmark (may name a new category) |
| PUT | `/api/bookmarks/{id}` | Edit: all fields for manual; category and tags only for Docker |
| DELETE | `/api/bookmarks/{id}` | Delete a manual bookmark (409 for Docker) |
| POST | `/api/bookmarks/delete` | Delete several manual bookmarks, all or nothing (409 if any is Docker); ids already gone are skipped; returns how many were deleted |
| POST | `/api/bookmarks/{id}/reset` | Docker only: clear overrides, re-apply labels |
| PUT | `/api/bookmarks/order` | Save a drag: target category plus the full ordered id list for it |
| POST | `/api/categories` | Add a category |
| PUT | `/api/categories/{id}` | Rename |
| DELETE | `/api/categories/{id}` | Delete, moving bookmarks to Uncategorized (409 for Uncategorized) |
| PUT | `/api/categories/order` | Save drawer order |
| POST | `/api/import/preview` | Parse an uploaded bookmark HTML file; return the folder tree with counts, target categories, new-category flags and duplicate count. Saves nothing |
| POST | `/api/import` | The same file plus the selected folders; creates categories and bookmarks, returns how many were added and skipped |
| GET | `/api/icons/{key}` | Cached icon file; 204 when it has none, so the page shows a letter tile |
| GET | `/api/icons/preview` | Resolve an unsaved icon value for the form's live preview; 204 means a letter tile |
| GET | `/api/settings` | The title and web search URL from `FOYER_TITLE` and `FOYER_SEARCH_URL` |
| GET | `/healthz` | Liveness for Docker and Traefik |

## Out of scope

These stay out of Foyer for good, not just for version 1:

- **Service widgets.** Homepage-style API stats aren't worth the upkeep.
- **Auth.** Foyer always sits behind Tinyauth or whatever fronts it.
- **Discovery agents.** docker-socket-proxy already solves per-host access; Foyer won't ship its own agent.
- **Status for manual bookmarks.** No URL pinging; manual cards never get a dot.
- **Cleanup of removed containers.** A downed or removed container just vanishes. Its hidden record is what lets a container that returns under the same host and name come back as the same bookmark; a different name arrives as a new one.
- **Server-side display settings.** Theme and color scheme are per device, permanently; only the title and web search come from FOYER\_\* variables.
