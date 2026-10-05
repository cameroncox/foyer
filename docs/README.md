# Docs

Design documents written while planning Foyer, exported from the Claude artifacts they were
drafted in. The 1.0 files describe the plan as of v1.0.0; the 1.1 files describe profiles and
shared bookmarks, planned on top of it. Where the code and these differ, the code wins.

| File | What |
| --- | --- |
| [spec.md](spec.md) | Foyer Spec: behaviour, labels, data model, UI, API sketch, out of scope |
| [project-layout-plan.md](project-layout-plan.md) | Project Layout Plan: repo tree, stack choices, tests, CI, build order |
| [spec-1.1.md](spec-1.1.md) | Foyer 1.1 spec: profiles, Default editors, shared bookmarks, new config, data model and API changes |
| [project-layout-plan-1.1.md](project-layout-plan-1.1.md) | Foyer 1.1 Project Layout Plan: backend and frontend changes, migration, request pipeline, tests, build order |
| [wireframes/](wireframes/) | Foyer Wireframes: 14 desktop and phone artboards for 1.0, and 12 for 1.1 in [wireframes/1.1/](wireframes/1.1/), as PNGs |
| [screenshots/](screenshots/) | The main page in light and dark, from a demo instance, for the README |

The spec's architecture diagram was an embedded widget and didn't survive the Markdown export;
it shows as a placeholder line.

The 1.1 spec only covers what changes. Its sections with a heading and no body (Architecture and
deployment, Docker discovery, Labels, Icons, Importing browser bookmarks, Theme) are unchanged
from 1.0.

The 1.0 wireframes, in the canvas's order:

| # | Artboard |
| --- | --- |
| 1 | [Main page](wireframes/01-main-page.png) |
| 2 | [Edit mode and category drawer](wireframes/02-edit-mode-category-drawer.png) |
| 3 | [Add bookmark (from the + in any mode)](wireframes/03-add-bookmark.png) |
| 4 | [Edit manual bookmark](wireframes/04-edit-manual-bookmark.png) |
| 5 | [Edit Docker bookmark](wireframes/05-edit-docker-bookmark.png) |
| 6 | [Empty state](wireframes/06-empty-state.png) |
| 7 | [Phone: menu closed](wireframes/07-phone-menu-closed.png) |
| 8 | [Phone: menu open](wireframes/08-phone-menu-open.png) |
| 9 | [Phone: edit mode](wireframes/09-phone-edit-mode.png) |
| 10 | [Phone: edit bookmark sheet](wireframes/10-phone-edit-bookmark-sheet.png) |
| 11 | [Phone: categories sheet](wireframes/11-phone-categories-sheet.png) |
| 12 | [Search results](wireframes/12-search-results.png) |
| 13 | [Import preview](wireframes/13-import-preview.png) |
| 14 | [Phone: import preview](wireframes/14-phone-import-preview.png) |

The 1.1 wireframes (the canvas's "1.1 Profiles" page), in its order:

| # | Artboard |
| --- | --- |
| 1 | [Personal profile (cameron)](wireframes/1.1/01-personal-profile.png) |
| 2 | [Default, opened by an editor](wireframes/1.1/02-default-opened-by-editor.png) |
| 3 | [Edit mode with shared cards](wireframes/1.1/03-edit-mode-shared-cards.png) |
| 4 | [Add bookmark, Shared on](wireframes/1.1/04-add-bookmark-shared-on.png) |
| 5 | [Stop sharing confirm](wireframes/1.1/05-stop-sharing-confirm.png) |
| 6 | [Someone else's shared bookmark](wireframes/1.1/06-someone-elses-shared-bookmark.png) |
| 7 | [Phone: menu with profile picker](wireframes/1.1/07-phone-menu-profile-picker.png) |
| 8 | [Phone: edit sheet, Shared on](wireframes/1.1/08-phone-edit-sheet-shared-on.png) |
| 9 | [Phone: read-only shared sheet](wireframes/1.1/09-phone-read-only-shared-sheet.png) |
| 10 | [Profile picker open](wireframes/1.1/10-profile-picker-open.png) |
| 11 | [New profile (no auth, ownerless)](wireframes/1.1/11-new-profile.png) |
| 12 | [Share a Docker bookmark from Default](wireframes/1.1/12-share-docker-bookmark-from-default.png) |
