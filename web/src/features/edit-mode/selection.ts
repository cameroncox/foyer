import type { Bookmark } from '../../api/client.ts'

/** Docker bookmarks follow their container, so only manual ones can be picked for deletion. */
export const isSelectable = (bookmark: Bookmark) => !bookmark.docker

export function toggleOne(selected: ReadonlySet<number>, id: number): Set<number> {
  const next = new Set(selected)
  if (!next.delete(id)) {
    next.add(id)
  }

  return next
}

/** A category's select-all: adds its selectable bookmarks, or removes them if all are picked. */
export function toggleAll(
  selected: ReadonlySet<number>,
  bookmarks: readonly Bookmark[],
): Set<number> {
  const ids = bookmarks.filter(isSelectable).map((b) => b.id)
  const next = new Set(selected)
  const all = ids.every((id) => selected.has(id))
  for (const id of ids) {
    if (all) {
      next.delete(id)
    } else {
      next.add(id)
    }
  }

  return next
}

/** Checkbox state for a category heading. */
export function allState(selected: ReadonlySet<number>, bookmarks: readonly Bookmark[]) {
  const ids = bookmarks.filter(isSelectable).map((b) => b.id)
  const picked = ids.filter((id) => selected.has(id)).length
  return {
    selectable: ids.length > 0,
    checked: ids.length > 0 && picked === ids.length,
    indeterminate: picked > 0 && picked < ids.length,
  }
}

/** Drops ids whose bookmarks are gone, e.g. deleted in another tab. */
export function stillPresent(
  selected: ReadonlySet<number>,
  present: Iterable<number>,
): Set<number> {
  const ids = new Set(present)
  return new Set([...selected].filter((id) => ids.has(id)))
}
