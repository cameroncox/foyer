# Foyer 1.1 — Project Layout Plan

Oct 5, 2026 · @Cameron Cox

## Overview

1.1 adds profiles, the Default profile and shared bookmarks, as the [Foyer 1.1 spec](spec-1.1.md) describes. This plan works from the code as it stands in `~/Projects/foyer`, not the [1.0 layout plan](project-layout-plan.md), which has drifted from it (CI lives in `.github/workflows`, there's a quick-add page and pruning). The container image, EF Core and SQLite stay; the standalone-binary work is separate.

| Area | Choice |
| --- | --- |
| Owner's placement | Stays on `Bookmark` (CategoryId, SortOrder, overrides) |
| Other profiles' placement | New `SharedPlacement` table, for shared bookmarks only |
| Current profile | Scoped `ProfileContext`, set once per request by middleware; Core services inject it |
| Profile on API calls | `X-Foyer-Profile` header from the page's route; `?profile=` for the SSE stream |
| Frontend routing | React Router, declarative mode: `/add` and `/:profile?` |

Found in the repo, to fold back into the spec:

- `/add` (quick-add page) and `/openapi` are taken, so both join the reserved profile names.
- Category names are unique across the whole database (a `NOCASE` index), and Uncategorized is seeded with a fixed id (`Category.UncategorizedId = 1`). Both become per profile.
- `EventSource` can't send custom headers, which is why the SSE stream takes the profile as a query parameter.

## Backend changes

Two new Core folders hold the rules: `Profiles/` decides who's asking and what they can see, and `Sharing/` decides where a shared bookmark lands. Both keep their rules pure, like `Sync/`, so they're unit-tested without EF. Existing services change to work within the current profile. `+` is new, `~` changed.

```
src/Foyer.Core/
├── Entities/
│   ├── + Profile.cs
│   ├── + SharedPlacement.cs
│   ├── ~ Bookmark.cs             # ProfileId (owner), IsShared
│   └── ~ Category.cs             # ProfileId; UncategorizedId constant goes
├── Profiles/                     # new
│   ├── ProfileOptions.cs         # FOYER_PROFILES, headers, trusted proxies, Default editors
│   ├── ProfileResolver.cs        # pure: request facts -> profile, or 403/404
│   ├── ProfileContext.cs         # scoped: profile, user, groups, CanEdit
│   ├── ProfileNames.cs           # slugify, reserved names, availability rule
│   └── ProfileService.cs         # create, rename, delete, personal profile on first sight
├── Sharing/                      # new
│   ├── SharedPlacementRules.cs   # pure: name match, create at end, re-place on owner change
│   └── SharingService.cs         # keeps SharedPlacement rows in step
├── Services/
│   ├── ~ DashboardService.cs     # own + shared bookmarks; Docker only reaches Default unless shared
│   ├── ~ BookmarkService.cs      # owner-only edits, IsShared, share/unshare fan-out
│   ├── ~ CategoryService.cs      # per profile; delete blocked by others' shared bookmarks
│   ├── ~ CategoryLookup.cs       # name lookup within one profile
│   ├── ~ OrderingService.cs      # order own and others' shared bookmarks within a category
│   └── ~ DockerBookmarkStore.cs  # new Docker bookmarks belong to Default
├── Import/ ~ ImportService.cs    # into the current profile
├── Events/ ~ ChangeBroadcaster.cs # each change names the profiles it affects
└── Data/
    ├── Configurations/           # + Profile, + SharedPlacement, ~ Category, ~ Bookmark
    └── Migrations/               # + Profiles (see Data and migration)

src/Foyer.Api/
├── + Profiles/ProfileMiddleware.cs  # resolve once per /api request into ProfileContext
├── Endpoints/
│   ├── + ProfileEndpoints.cs     # /api/me, /api/profiles CRUD
│   └── ~ Dashboard, Bookmark, Category, Import, Event endpoints
├── Contracts/                    # + MeResponse, ProfileResponse, Create/RenameProfileRequest;
│                                 # ~ BookmarkResponse (isShared, sharedFrom, canEdit), bookmark requests (isShared)
├── Configuration/ ~ FoyerSettings.cs # parses the new FOYER_* vars into ProfileOptions
└── ~ Program.cs                  # middleware on /api; startup log for profile mode
```

Rules enforced in Core services, not endpoints:

- Writes go through `ProfileContext.EnsureCanEdit()`: always for your own profiles and ownerless ones; for Default only per the editor rules.
- Only the owner edits or deletes a bookmark; others' shared bookmarks only reorder within their current category (403 otherwise).
- Docker bookmarks are only ever created in Default; `isShared` on a Docker bookmark is accepted only from Default.
- A category holding someone else's shared bookmarks can't be deleted (409 with the explanation).
- `ChangeBroadcaster.BookmarksChanged` takes the affected profile ids (or "all" for a shared bookmark), and the SSE stream only wakes subscribers whose profile is in that set.

## Data and migration

A bookmark's own category, order and Docker overrides stay where they are, so 1.0 rows need no reshaping. Only other profiles' views of a shared bookmark get a new table.

| Table | Change |
| --- | --- |
| `Profile` (new) | Id, Name (shown; the header value for a personal profile), Slug (URL, `NOCASE`), OwnerUser?, IsSystem (Default), IsPersonal, CreatedAt |
| `Category` | + ProfileId (cascade on profile delete). Unique index moves from Name to (ProfileId, Name). Each profile gets its own Uncategorized (IsSystem) |
| `Bookmark` | + ProfileId (owner; Default for Docker), + IsShared. CategoryId and SortOrder are the owner's placement |
| `SharedPlacement` (new) | ProfileId + BookmarkId (key), CategoryId, SortOrder. One row per other profile per shared bookmark; cascades with either side |
| `BookmarkTag` | Unchanged: tags belong to the bookmark, so to its owner |

Tags stay on the bookmark, as the spec says: with Docker bookmarks only in Default, they don't need a ProfileId.

Two migrations, `DefaultProfile` (step 1) and `Profiles` (steps 2 to 4). They're split so a rollback removes the foreign keys before it drops the `Profile` table; dropped first, the cascade would take Default's categories with it. A rollback keeps only Default's rows.

1. Create `Profile` and insert Default as id 1.
2. Add `Category.ProfileId`, set every row to 1, and swap the Name index for (ProfileId, Name).
3. Add `Bookmark.ProfileId` (all 1) and `IsShared` (all false).
4. Create `SharedPlacement`, empty.

SQLite can't alter these columns in place, so EF rebuilds the tables; the migration test runs it against a copy of a real 1.0 `foyer.db`.

- `Seed` keeps Default's Uncategorized; `ProfileService` creates one for every new profile.
- Code that uses `Category.UncategorizedId` switches to "the profile's system category".
- The slug-availability rule (no clash with anything someone could see alongside it) is checked in `ProfileNames`; no index can express it.
- A personal profile whose slug is taken gets `-2`, `-3`, …, since it's created automatically and can't fail.

## Request pipeline

`ProfileMiddleware` runs on every `/api` request before the endpoints. It gathers the request facts, hands them to the pure `ProfileResolver`, and fills the scoped `ProfileContext` or ends the request.

1. **Profiles off** (`FOYER_PROFILES=false`): Default, editable. Done.
2. **User header** (`FOYER_PROFILE_HEADER`, default `Remote-User`): when present, the connection's address must be in `FOYER_TRUSTED_PROXIES` (empty trusts all) and the value must be non-empty, else 403 and a log line. Groups come from `FOYER_GROUPS_HEADER`.
3. **Requested profile:** `X-Foyer-Profile`, or `?profile=` for `/api/events`. None means the personal profile with a header, or Default without.
4. **Visibility:** the slug must match Default, one of this user's profiles, or an ownerless one. Anything else is 404, the same answer whether it exists or not.
5. **First sight:** a user with no personal profile gets one, created before the request continues.
6. **ProfileContext:** profile, user, groups, and CanEdit (own and ownerless: yes; Default: the editor rules).

Notes:

- Foyer doesn't use `ForwardedHeaders`, so `Connection.RemoteIpAddress` is the proxy's address (Traefik), which is what the trust check wants.
- `/api/settings`, `/api/icons/*` and `/healthz` don't depend on a profile; the middleware still runs on `/api` routes so a bad header is refused consistently.
- Page routes stay as they are: `MapFallbackToFile("index.html")` already serves the app for `/{profile}`, and the app learns about a missing profile from `/api/me`.
- The "nobody can edit Default" warning logs once, the first time a user header arrives while profiles are on and no editors are listed.

## Frontend changes

React Router replaces the pathname check in `Root.tsx`, and the current profile flows from the route into every API call and query key.

```
web/src/
├── ~ Root.tsx                     # <BrowserRouter>, routes: /add, /:profile?
├── ~ App.tsx                      # reads the profile from the route
├── api/
│   ├── ~ client.ts                # openapi-fetch middleware adds X-Foyer-Profile
│   ├── ~ queries.ts               # keys gain the profile: ['dashboard', profile]; + useMe
│   ├── ~ mutations.ts             # + profile create/rename/delete
│   └── ~ schema.d.ts              # regenerated
├── hooks/
│   └── ~ useLiveUpdates.ts        # /api/events?profile=…
└── features/
    ├── + profiles/                # ProfilePicker, PhoneProfileList, NewProfileModal,
    │                              # RenameProfile, DeleteProfileConfirm, rememberedProfile.ts
    ├── ~ top-bar/                 # picker beside the name; amber Default treatment for editors
    ├── ~ board/                   # shared icon and "Shared by/from" tooltip on cards
    ├── ~ bookmark-form/           # Shared switch (Docker form too, in Default); read-only panel
    ├── ~ edit-mode/               # lock instead of edit/delete; selection skips read-only cards;
    │                              # drawer explains a blocked category delete
    ├── ~ spotlight/               # Shared filter
    └── ~ quick-add/               # bookmarklet carries ?profile=; page posts to that profile
```

- **Routing:** `react-router` in declarative mode only: no loaders or framework mode, TanStack Query keeps doing the fetching. Profile slugs never collide with `/add`, since `add` is reserved.
- **`/`:** opens this device's remembered profile when there is one. If `/api/me` says it isn't visible, the page drops the pick and shows the personal profile or Default.
- **Switching profiles** navigates to `/{slug}`; React Router handles back and forward, and the query cache keeps each profile's dashboard separately.
- **Quick add:** `BookmarkletLink` builds `/add?profile=<slug>&…` for the profile you're on. When that profile is read-only to you, the page says so and, with a user header, offers to add it to your personal profile instead.

## Tests

The pure resolver and placement rules take most of the weight, as the sync rules did in 1.0. Existing tests keep passing with everything in Default, which is step 1's exit check.

| Project | New coverage |
| --- | --- |
| Foyer.Core.Tests | `ProfileResolver`: profiles off, trusted and untrusted proxies, empty header, visibility, fallback. `ProfileNames`: slugify, reserved names (including `add` and `openapi`), availability. `SharedPlacementRules`: name match, create at end, Uncategorized, owner move and rename, a viewer's rename sticking. Services: owner-only edits, share and unshare fan-out, per-profile category names, blocked category delete, dashboard merge, Docker only in Default. Migration from a 1.0 `foyer.db` fixture |
| Foyer.Api.Tests | 403 and 404 from the middleware, `X-Foyer-Profile`, `/api/me`, profile CRUD, the Default editors matrix, SSE reaching only affected profiles, `FOYER_PROFILES=false` hiding profiles |
| web | Routes (`/`, `/:profile`, `/add`), the profile header on every call, picker and New profile, the remembered-pick fallback, shared and read-only cards, quick add into a read-only profile |

Support changes:

- `FoyerApiFactory` gains helpers to send a user header and groups, and a startup filter that sets the connection's address, so the trusted-proxy tests don't need a real proxy.
- `TestDb` and `FakeApi` take a current profile; most existing tests keep Default and don't change.
- The 1.0 fixture database is a small `foyer.db` made from the current schema, committed under `tests/Foyer.Core.Tests/Data/`.

## Build order

Each step ends with CI green and the app still working as 1.0 does for anyone who doesn't use the new parts. Step 4 went in before step 3, so a build behind the auth proxy could reach Default sooner; until then FOYER\_PROFILES defaulted to false.

1. **Data:** `Profile` and `SharedPlacement` entities, the `Profiles` migration, per-profile categories, the 1.0 fixture migration test. Everything runs in Default; existing tests pass unchanged.
2. **Profile resolution:** `ProfileOptions` from `FOYER_*`, `ProfileResolver`, `ProfileMiddleware`, `ProfileContext` injected into services, personal profiles on first sight, `/api/me` and `/api/profiles`.
3. **Sharing:** `IsShared`, `SharedPlacementRules` and `SharingService`, dashboard merge, owner-only edits, blocked category delete, profile-scoped SSE.
4. **Web routing:** React Router, profile header and query keys, `useLiveUpdates` with `?profile=`, picker, New profile, rename and delete, remembered pick.
5. **Web sharing:** Shared switches (Docker in Default too), shared and read-only cards, Default banner, Shared filter in spotlight, quick add with `?profile=`.
6. **Docs and release:** copy the 1.1 spec and this plan into `docs/`, add user-header notes to `deploy/compose.example.yml`, regenerate the wireframe PNGs, tag `v1.1.0`.
