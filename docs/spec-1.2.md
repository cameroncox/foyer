# Foyer 1.2

Oct 5, 2026 · @Cameron Cox

## Overview

What 1.2 changes on top of [Foyer 1.1](spec-1.1.md).

In 1.1, sharing a bookmark sends it to every profile. The case that prompted 1.2: a Default editor wants the Docker containers on their own profile, but sharing them from Default puts them in everyone's profile, a spouse's included. Beyond Docker, a bookmark from your own profile usually belongs with one or two people, not everyone.

Goals:

- **Docker bookmarks on your own profile:** a Default editor can show every Docker bookmark on a profile of theirs, including containers that appear later, without sharing them with anyone else.
- **Share with chosen profiles:** a shared bookmark goes either to everyone, as in 1.1, or to profiles you pick.
- **Same model with or without auth:** you share with profiles, not users. Behind an auth proxy, a person's personal profile stands for that person. Without one there are no users, but there are still profiles to pick.
- **Nothing moves on upgrade:** everything shared in 1.1 stays shared with everyone. New shares with everyone are for Default editors only, unless `FOYER_ENABLE_SHARE_WITH_EVERYONE=true`.

**Build order:** Docker bookmarks on your own profile first, since it covers the case that prompted 1.2 on its own. Sharing with chosen profiles follows.

## Since 1.1.0

1.1.1 already changed Default: a user with a `Remote-User` header who can't edit Default no longer sees it. It's left out of the picker, and `/default` is a 404 for them, while its shared bookmarks still reach their own profiles. Header-less requests still see Default, read-only when editors are listed. 1.2 keeps this. The 1.1 spec's "Anyone can open Default from the picker" no longer holds.

## Docker bookmarks on your own profile

A profile setting, **Show Docker bookmarks**, puts every Docker bookmark from Default on that profile. Containers that appear later show up there too, with nothing shared.

- **Who can turn it on:** someone who can edit Default, on a profile they can edit. Behind a proxy, that's a Default editor's own profiles and ownerless ones. Without a proxy, it's any profile, when header-less requests can edit Default. Everyone else doesn't see the setting. So listing yourself in `FOYER_DEFAULT_REMOTE_USERS` and not your wife means only your profiles can show the containers.
- **Ownerless profiles:** turning it on for an ownerless profile shows the containers to everyone who can open that profile. The setting says so: "Everyone who opens vendor will see them."
- **Where they land:** by the 1.1 rule for shared bookmarks, in the category with the same name as in Default, created if needed. After that you can reorder them in your own view. Their status dots, dimming and host tags work as in Default.
- **Read-only, like a share:** category, tags and Reset to labels stay in Default. The read-only panel says "From Default's Docker hosts".
- **Hidden containers:** a container that's gone stays hidden on the profile as it does in Default, and comes back in place.
- **Losing editor rights:** if you're removed from the Default editors, your profiles stop showing Docker bookmarks on the next request, but the setting is kept. It applies again if you're added back.
- **Turning it off** removes them from the profile, except Docker bookmarks that are also shared with that profile.
- **Shared and shown:** a Docker bookmark that's shared with a profile and also shown there by this setting appears once.
- **Spotlight** offers the Docker host filters on that profile, as in Default.

## Audience

A bookmark is **not shared**, **shared with everyone**, or **shared with chosen profiles**.

- **Everyone** works as in 1.1: every other profile shows it, including profiles made later. Only Default editors can choose it, unless `FOYER_ENABLE_SHARE_WITH_EVERYONE=true` opens it to anyone (see Configuration).
- **Chosen profiles** show it, and no others. A profile made later doesn't get it.
- The owner's profile is never part of the audience. It already holds the bookmark.
- The placement rules from 1.1 apply unchanged, but only within the audience. That covers the category by name, merging, Uncategorized, re-placing when the owner moves the bookmark or renames its category, and reordering in your own view.

**Who you can share with:**

|  | No header (no auth) | `Remote-User` header |
| --- | --- | --- |
| Ownerless profiles | Yes | Yes |
| Default | If you can edit it | If you can edit it |
| Your own other profiles | — | Yes |
| Other users' personal profiles | — | Yes, listed by the profile's current name |
| Other users' other profiles | — | No; they stay private, as in the picker |

- Sharing with your own other profiles is useful: share a bookmark from your personal profile into your `work` profile without copying it.
- Sharing with a person means sharing into their personal profile. If they rename it, the share follows, since targets are kept by profile, not by name.
- Listing other users' personal profiles shows every user who has visited Foyer to every other user. Up to 1.1, nobody could see anyone else's profiles; that's accepted for a tool shared by people who know each other. Only the profile's current name shows, never the `Remote-User` value.
- Default is a target only for its editors, so a non-editor can't push bookmarks onto the page every Default viewer sees.

**Lifecycle:**

- **Widening** (adding profiles, or switching to Everyone) places the bookmark at the end of its category in each new profile, as sharing does in 1.1.
- **Narrowing** (removing profiles, or switching from Everyone to chosen) takes it out of the profiles that left the audience, and asks first: "Stop sharing 'Jellyfin' with kitchen? It disappears there."
- **Unsharing or deleting** removes it everywhere, as in 1.1.
- **Deleted targets:** when a target profile is deleted, it drops out of the audience. If no targets remain, the bookmark is no longer shared.
- **A target you can no longer pick** stays in the audience. For example, you lose Default editor rights. You can still remove it, but you can't add it back. Everyone works the same way for someone who can't choose it: an Everyone share they made in 1.1 stays until they narrow it.

## Configuration

New variable, added to the 1.1 table:

| Variable | Default | Purpose |
| --- | --- | --- |
| `FOYER_ENABLE_SHARE_WITH_EVERYONE` | `false` | `true` lets anyone share a bookmark with everyone, as in 1.1. Otherwise only those who can edit Default can (`FOYER_DEFAULT_REMOTE_USERS`, `FOYER_DEFAULT_REMOTE_GROUPS`, or header-less requests when none are listed). Sharing with chosen profiles is open to everyone either way |

- Without a proxy and with no editors listed, every request can edit Default, so everyone can share with everyone, as in 1.1.
- A value other than `true` or `false` stops startup.
- With profiles off it has no effect, as there's nobody to share with.


## Data model

**Bookmark:** `IsShared` stays, meaning "shared with anyone". It gains:

| Column | Type | Notes |
| --- | --- | --- |
| ShareWithEveryone | bool | With IsShared: every other profile, as in 1.1. Without it: only the profiles in ShareTarget |

**ShareTarget** (new): who a bookmark is shared with, when it isn't everyone.

| Column | Type | Notes |
| --- | --- | --- |
| BookmarkId, ProfileId | int | Key; cascades with either |

**Profile** gains:

| Column | Type | Notes |
| --- | --- | --- |
| ShowsDockerBookmarks | bool | Show Docker bookmarks. Honoured only while the profile's owner can edit Default (any profile when header-less requests can) |

**SharedPlacement** is unchanged in shape. The rule becomes: a bookmark has a placement in exactly the profiles of its audience, plus, for a Docker bookmark, every profile showing Docker bookmarks. Placements are kept even while the owner's editor rights lapse, so the layout comes back with them; the dashboard just leaves them out.

- **Migration:** every bookmark with `IsShared` gets `ShareWithEveryone = true`, so nothing moves.
- Keeping `IsShared` and adding a column avoids a SQLite rebuild of `Bookmarks`. Rebuilding that table inside the migration's transaction would cascade into tags and placements; the 1.1 `Profiles` migration hit the same issue.
- `IsShared` without `ShareWithEveryone` and with no ShareTarget rows is never stored. Removing the last target unshares the bookmark.

## Main page

- **Owner's badge:** the shared badge in the card's corner gets a tooltip saying who it's shared with: "Shared with everyone", "Shared with alex and kitchen", or "Shared with alex, kitchen and 2 more". Recipients see it as in 1.1: "Shared by cameron", "Shared from vendor".
- Search, spotlight and its **Shared** filter are unchanged. They cover whatever the profile sees.

## Editing on desktop

- **Show Docker bookmarks** is a switch in the profile's dialog, the one the picker's pencil opens, below its name. It's shown only to someone who can turn it on, and Default's dialog doesn't have it.

- **Add and Edit:** the Shared switch becomes **Share with**.
  - Off by default, as in 1.1.
  - Turning it on offers **Everyone** or **Chosen profiles**. Everyone is left out for someone who can't choose it.
  - It starts on Chosen profiles. Everyone is one click away, but a share reaching every profile should be a choice, not a default.
  - Chosen profiles is a multi-select grouped like the picker: **People** (other users' personal profiles), **Yours**, **Everyone's** (ownerless), and **Default** for its editors. The current profile isn't listed.
  - Saving Chosen profiles with nothing picked is refused: "Pick at least one profile, or turn sharing off."
- The Docker edit popover in Default gets the same control.
- Turning sharing off, or narrowing the audience, confirms first, as above.
- **Deleting a profile** that shares bookmarks says who loses them: "vendor shares 3 bookmarks. They disappear from every profile they're shared with."

## Phone

Same rules. The edit sheet's Share with control uses the same choices, with a full-screen list for picking profiles.

## API sketch

| Method | Path | Change |
| --- | --- | --- |
| PUT | `/api/profiles/{id}` | Also takes `showsDockerBookmarks`; 403 for someone who can't edit Default |
| GET | `/api/me` | Each profile says whether it shows Docker bookmarks, and whether the caller can change that |
| GET | `/api/share-targets` | New: the profiles the caller can share with from the current profile, with name and kind (`person`, `yours`, `ownerless`, `default`) |
| POST, PUT | `/api/bookmarks…` | `isShared` is replaced by `share`: null (not shared), or `{ everyone: bool, profileIds: int[] }`. A profile id the caller can't share with is a 400, "You can't share with that profile", the same whether or not it exists. `everyone` from someone who can't choose it is a 403, unless the bookmark is already shared with everyone |
| GET | `/api/me` | Also says whether the caller can share with everyone |
| GET | `/api/dashboard` | The owner's copy of a shared bookmark carries `sharedWith`: `everyone`, or the target profiles' names. Recipients see `sharedBy` and `sharedFrom` as in 1.1 |
| GET | `/api/events` | `bookmarks-changed` for a shared bookmark goes to the owner and its audience, before and after the change, rather than to everyone |

## Out of scope

Out for good:

- **Hiding a share** from your own view. A bookmark shared with you is there for a reason; ask its owner to narrow or unshare it.

Not in 1.2:

- **Sharing with a group** (`Remote-Groups`) as an audience.
- **Recipients editing** a shared bookmark. The owner stays the only editor.
- **Sharing a whole category.** Sharing is still per bookmark.

## Decisions

Settled on Oct 5, 2026:

1. **Build order:** the Docker setting first, then sharing with chosen profiles.
2. **No hiding shares.** If something's shared with you, it's there for a reason.
3. **Sharing with everyone is for Default editors**, the same users and groups that edit Default. `FOYER_ENABLE_SHARE_WITH_EVERYONE=true` opens it to anyone, as in 1.1.
4. **Users see each other's personal profiles** in the target list.
5. **People are listed by their personal profile's current name**, not by `Remote-User`.
