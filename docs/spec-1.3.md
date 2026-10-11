# Foyer 1.3

Oct 10, 2026 · @Cameron Cox

## Overview

What 1.3 changes on top of [Foyer 1.2](spec-1.2.md): bookmarks move between profiles, Default stops nagging its editors, and profiles can be switched from the keyboard.

## Changes

### Moving bookmarks between profiles

Default is just the default: nothing on it nags its editors, and bookmarks that belong elsewhere can be moved out.

- **No Default banner.** 1.1's "You're editing Default" banner is gone.
- **No handover offer.** 1.2.0's one-time "Default has N bookmarks from before profiles" offer is gone; moving replaces it. Its `HandoverSettledAt` column stays, unused, to avoid rebuilding `Profiles`.
- **Move to** sits in edit mode's selection bar, beside Delete, on any profile you can edit, with profiles on. It lists the other profiles you can edit, grouped like the picker (Default for its editors, Yours, Everyone's), then **New profile…**, which asks for a name, makes the profile and moves the bookmarks there. You stay on the page you were on; a toast says "Moved 3 bookmarks to work".
- **Manual bookmarks only.** Docker cards can't be selected, as for Delete; a Docker id sent anyway refuses the lot (409).
- **Where they land:** in the category with the same name, made where missing in the source's order, Uncategorized to Uncategorized, after what's already there. Categories the move empties are removed from the source.
- **Sharing:** a shared bookmark keeps its audience, less the profile it moved to. One shared only with that profile is no longer shared. One shared with everyone now shows in the profile it left too, in the slot it had.

### Switching profiles from the keyboard

With profiles on, on desktop.

- **P** (outside edit mode, forms and text boxes) opens the spotlight on the other profiles alone, placeholder "Switch to a profile". Typing narrows them by name or slug; Enter switches to the highlighted one. It starts in picking mode, so 1–9 switches at once. Backspace in the empty box goes back to the full spotlight.
- **In the ordinary spotlight,** the other profiles are a **Profiles** group: last when nothing is typed, and matching ones as "Switch to work" after the web search while typing, so Enter on a miss still searches the web.
- The picker's tooltip says "Switch profile (P)". P does nothing with only one profile to see.

## API

| Method | Path | Change |
| --- | --- | --- |
| POST | `/api/bookmarks/move` | New: `{ bookmarkIds, profileId }` moves manual bookmarks to another profile the caller can edit, answering `{ moved }`. A profile they can't edit is a 400, the same whether or not it exists |
| POST, DELETE | `/api/me/handover` | Removed, with `handoverCount` in `/api/me` |
