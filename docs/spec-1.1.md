# Foyer 1.1

Oct 5, 2026 · @Cameron Cox

## Overview

What 1.1 adds on top of the [Foyer Spec](spec.md).

Goals:

- **Profiles:** named views with their own bookmarks and layout. Without auth, pick one by URL or from the menu; behind an auth proxy, each user gets their own and can make more. Foyer still has no in-app auth.
- **Shared bookmarks:** mark a bookmark shared and every profile sees it, read-only, in a category of the same name.

## Profiles

A profile is a named view of Foyer: its own manual bookmarks, categories, order and tags. **Default** is the home profile and the only one that holds Docker bookmarks. A bookmark marked shared, in Default or any other profile, shows in every profile. Profiles work with or without an auth proxy; the user header only decides whose profiles you can see.

|  | No header (no auth) | `Remote-User` header |
| --- | --- | --- |
| At `/` | Default, or this device's last pick | The user's personal profile (created on first sight), or this device's last pick |
| Can open | Default and every ownerless profile | Default, their own profiles, and every ownerless profile |
| New profiles | Ownerless | Owned by that user |
| Edits Default | Only when no Default editors are listed | Only if listed as a Default editor |

- **Default** is a system profile at `/default`, and at `/` for header-less requests. It can't be renamed or deleted. Its bookmarks stay in Default unless marked shared, like any profile's, and bookmarks shared from other profiles show in it too.
- **Docker bookmarks live only in Default.** Other profiles see one only when it's marked shared there, so a vendor profile stays free of containers. A shared Docker card keeps its live status dot, dimming and host tag everywhere it shows.
- **Ownerless profiles** are made without a header. Everyone sees and edits them, header or not. Example: a "vendor" profile for one product's links, set as a Chrome profile's homepage at `/vendor`.
- **User profiles** belong to one `Remote-User`. Nobody else sees them; anyone else gets a 404. The first one, made on first sight, is that user's **personal profile**. Its URL is the header value slugified (`cameron@casadecox.org` → `/cameron-casadecox-org`); the picker shows the header value as sent.
- **Bad headers:** a `Remote-User` from an untrusted address, or one that's present but empty, gets a 403. The untrusted case is logged.
- **Picking:** the URL wins. Every profile lives at `/{name}`. Picking one in the menu goes to its URL and is remembered per device, so `/` reopens it. If the remembered profile isn't visible to this request any more, `/` silently opens the personal profile (with a header) or Default (without), and the pick is forgotten.
- **Names:** letters, digits and hyphens, up to 50. The URL is the name in lower case, matched case-insensitively. A name can't clash with any profile that someone could see alongside it: an ownerless name must be unused everywhere, and a user's names must not match an ownerless one. The error is just "That name isn't available", so it never reveals someone's private profile. A reserved name says Foyer uses it instead. Reserved (plus add and openapi, which the app already serves): `api`, `assets`, `default`, `healthz`.
- **Managing:** the menu has New profile, plus rename and delete for profiles you can edit. Deleting asks first and removes its bookmarks and categories; if it shares any, the confirm says so: "vendor shares 3 bookmarks. They disappear for everyone." Default and a user's personal profile can't be renamed or deleted.
- **Import** lands in the current profile, unshared, Default included. Quick add uses the profile baked into its bookmarklet: the bookmarklet offered on a profile's page opens /add?profile=\<name>, so each Chrome profile drags its own. Adding to a profile you can't edit says so, and with a header offers to add to your personal profile instead.
- **Upgrade from 1.0:** all existing bookmarks and categories move into Default, unshared. A no-auth install looks exactly like 1.0; behind auth, Default editors mark what everyone should see as shared.

**Default editors** are the users or groups allowed to change Default, e.g. the one team that runs the Foyer install.

| Setup | Who edits Default |
| --- | --- |
| `FOYER_PROFILES=false` | Everyone; there are no profiles, as in 1.0 |
| Profiles on, no editors listed | Header-less requests; users with a header can't |
| Profiles on, editors listed | Users in `FOYER_DEFAULT_REMOTE_USERS` or with a group in `FOYER_DEFAULT_REMOTE_GROUPS`; header-less requests can't |

- So a no-auth install edits Default freely, and listing an editor is what locks it down.
- Users and groups match case-insensitively, and in user names @ and \_ match each other, since Tinyauth sends me@example.com as me\_example.com. Groups come from `FOYER_GROUPS_HEADER` (default `Remote-Groups`, comma-separated), with the same trusted-proxy check as the user header.
- Anyone can open Default from the picker; it's read-only for those who can't edit it. If profiles are on, no editors are listed and a Remote-User header has been seen, Foyer logs a warning that nobody behind the proxy can edit Default.

## Architecture and deployment

## Configuration

New variables, added to the 1.0 table:

| Variable | Default | Purpose |
| --- | --- | --- |
| `FOYER_PROFILES` | `true` | `false` turns profiles off; every request uses Default, as in 1.0. Other profiles are hidden, not deleted, and come back when it's turned on again. Bookmarks they share are hidden from Default too, since nobody could edit or unshare them there |
| `FOYER_PROFILE_HEADER` | `Remote-User` | Header that names the user |
| `FOYER_TRUSTED_PROXIES` | empty | Comma-separated IPs or CIDRs allowed to set the header. Empty trusts every address |
| `FOYER_DEFAULT_REMOTE_USERS` | empty | Comma-separated user names that can edit Default |
| `FOYER_DEFAULT_REMOTE_GROUPS` | empty | Comma-separated groups whose members can edit Default |
| `FOYER_GROUPS_HEADER` | `Remote-Groups` | Header listing the user's groups, comma-separated |

- Tinyauth sends `Remote-User`, so the defaults work behind it unchanged.
- With `FOYER_TRUSTED_PROXIES` empty, anyone who reaches Foyer without going through the proxy can act as any user by sending the header. Set it to Traefik's address once Foyer is reachable any other way.
- A malformed entry in `FOYER_TRUSTED_PROXIES` stops startup and names the entry.

## Docker discovery

## Labels

## Data model

A bookmark's own category and order stay on Bookmark, as in 1.0. Only other profiles' views of a shared bookmark need a placement of their own.

**Profile** (new)

| Column | Type | Notes |
| --- | --- | --- |
| Id | int |  |
| Name | string | Shown in the picker; the header value for a personal profile |
| Slug | string, case-insensitive | The URL; unique per owner, plus the availability rule under Profiles |
| OwnerUser | string? | Remote-User value; null for ownerless profiles and Default |
| IsSystem | bool | True only for Default |
| IsPersonal | bool | The profile created for a user on first sight; can't be renamed or deleted |
| CreatedAt | timestamp |  |

**Category** gains ProfileId. Names are unique per profile, and each profile has its own Uncategorized.

**Bookmark** gains:

| Column | Type | Notes |
| --- | --- | --- |
| ProfileId | int | Owner. Always Default for Docker bookmarks |
| IsShared | bool | Any bookmark; Docker bookmarks are shared from Default |

CategoryId, SortOrder, CategoryOverridden and TagsOverridden stay: they're the owner's placement.

**SharedPlacement** (new): where another profile shows a shared bookmark.

| Column | Type | Notes |
| --- | --- | --- |
| ProfileId, BookmarkId | int | Key |
| CategoryId | int | One of that profile's categories |
| SortOrder | int | Position in that category |

**BookmarkTag** is unchanged: tags belong to the bookmark, so to its owner.

- Sharing a bookmark adds a SharedPlacement row in every other profile, placed by category name as below; a new profile gets rows for every shared bookmark when it's created. Unsharing or deleting the bookmark removes them.
- Rows cascade with their profile, bookmark or category.

## Ownership and lifecycle

Sharing is per bookmark. The owner edits it; everyone else can only reorder it in their own view.

|  | Owner | Other profiles |
| --- | --- | --- |
| Own bookmark, not shared | Full control | Not visible |
| Own bookmark, shared | Full control, can unshare | Read-only; reorder within its category |
| Bookmark in Default | Default editors (header-less with none listed) | Not visible unless shared; then read-only, reorder within its category |
| Docker bookmark | Lives in Default: category, tags and order start from labels, Reset to labels, can be shared | Not visible unless shared from Default |

**Which category a shared bookmark lands in** for another profile:

- The profile's category with the same name as the owner's (case-insensitive) — the two merge.
- None with that name → Foyer creates one at the end of the profile's list. After that it's an ordinary category the profile can rename or reorder.
- Owner's Uncategorized → the profile's Uncategorized.
- The owner is the source of truth, whether that's Default or a user. Moving the bookmark to another category, or renaming its category, re-places it for everyone by the same rule, at the end of the category: Default moves SharePoint from "Employee Tools" to "Archives", and Work shows it under Archives. The other profile's own bookmarks stay where they are.

**Lifecycle:**

- Sharing a bookmark appends it to the end of its category in every other profile.
- Unsharing or deleting it removes it, and its placements, everywhere else.
- Shared bookmarks show the owner's tags; other profiles can't add or remove them.

**Categories holding someone else's shared bookmarks:**

- **Delete is blocked.** The drawer's delete explains instead of confirming: "Read Later holds 1 bookmark shared by alex, so it can't be deleted. Move your own bookmarks out, or ask alex to unshare." Docker bookmarks don't block it; in Default they move to Uncategorized, as in 1.0.
- **Rename is fine.** The link to the owner's category sticks, so renaming Work's "Read Later" to "Later" keeps the shared bookmark in "Later". That holds until the owner moves the bookmark or renames its category; then it's placed by name again.
- Owners don't see how many profiles show a shared bookmark.

## Icons

## Main page

- **Profile picker** sits next to the instance name and shows the current profile ("cameron ▾", "Default ▾"). It lists Default, your profiles, ownerless profiles, and New profile.
- **Shared cards** carry a small shared icon beside the tags, for the owner and everyone else. The tooltip says who it's from: "Shared by cameron", or "Shared from vendor" / "Shared from Default" for profiles with no user.
- Search and spotlight cover everything the profile sees, shared bookmarks included. Spotlight gains a **Shared** filter; Docker host filters show only for Docker bookmarks the profile can see.
- **Default banner:** on Default, its editors see "You're editing Default. Docker bookmarks live here. Only bookmarks marked shared show in other profiles.", with a way back to their personal profile. A profile the page can't change has no Edit or + button.

## Editing on desktop

- **Add and Edit** gain a **Shared** switch, off by default. In Default, the Docker edit popover gets it too.
- **Someone else's shared card** in edit mode: a drag handle that only reorders within its category, no pencil or delete. Clicking it opens a read-only panel (name, URL, icon, owner).
- **Select** skips other profiles' shared cards, as it skips Docker cards.
- Turning Shared off asks first: "Stop sharing 'Jellyfin'? It disappears for everyone else."

## Phone

Same rules as desktop. The profile picker sits at the top of the menu; the edit sheet gets the Shared switch, and other profiles' shared cards open a read-only sheet.

## Importing browser bookmarks

## Theme

## API sketch

Same endpoints as 1.0. Middleware resolves the profile once per request, and every endpoint works within it.

| Method | Path | Change |
| --- | --- | --- |
| GET | `/api/me` | New: current profile, the profiles this caller can pick, and whether it can edit Default |
| GET | `/api/dashboard` | The profile's categories, with its own, shared and Docker bookmarks placed in them; each bookmark says if it's shared and who owns it |
| POST, PUT | `/api/bookmarks…` | Accept `isShared` (Docker bookmarks: from Default only) |
| PUT, DELETE | `/api/bookmarks/{id}` | 403 on another profile's bookmark |
| PUT | `/api/bookmarks/order` | May include others' shared bookmarks, within their current category only |
| GET | `/api/events` | `bookmarks-changed` goes to every profile when a shared bookmark changes, and otherwise only to pages showing the owning profile |
| Any | `X-Foyer-Profile: {name}` | Sent by the page from its URL: the profile to act on; ?profile= on /api/events, since EventSource can't send headers. 404 if the caller can't see it; writes to Default need edit rights |
| POST | `/api/profiles` | New: create a profile, owned by the caller's user or ownerless |
| PUT, DELETE | `/api/profiles/{id}` | New: rename or delete; 403 for Default or a personal profile, 404 for someone else's (the same answer as one that doesn't exist) |

Import lands in the current profile, unshared.

## Out of scope

Still out for good: in-app auth. Foyer trusts the proxy's header and nothing else.

Not in 1.1:

- **Admin or permissions.** A user's profiles are theirs alone; nobody manages another user's profiles.
- **Sharing with specific profiles.** Shared means everyone.
- **Sharing a whole category.** Sharing is per bookmark.
- **Hiding shared bookmarks** from your own view.
