import type { Dashboard, ReorderBookmarksRequest } from '../../api/client.ts'

export interface Slot {
  categoryId: number
  index: number
}

/**
 * The dashboard after dragging a card from `from` to `to`, and the request that saves it: the
 * target category's full order. A Docker card dropped in another category gets its category
 * locked, as the server will do. Null when nothing moved.
 */
export function moveBookmark(
  dashboard: Dashboard,
  from: Slot,
  to: Slot,
): { dashboard: Dashboard; request: ReorderBookmarksRequest } | null {
  if (from.categoryId === to.categoryId && from.index === to.index) {
    return null
  }

  const categories = dashboard.categories.map((c) => ({ ...c, bookmarks: [...c.bookmarks] }))
  const source = categories.find((c) => c.id === from.categoryId)
  const target = categories.find((c) => c.id === to.categoryId)
  if (!source || !target || !source.bookmarks[from.index]) {
    return null
  }

  let [moved] = source.bookmarks.splice(from.index, 1)
  if (source !== target) {
    moved = {
      ...moved,
      categoryId: target.id,
      docker: moved.docker && { ...moved.docker, categoryOverridden: true },
    }
  }

  target.bookmarks.splice(to.index, 0, moved)
  return {
    dashboard: { categories },
    request: { categoryId: target.id, bookmarkIds: target.bookmarks.map((b) => b.id) },
  }
}

/**
 * The dashboard after dragging a drawer row, and the new order of every category except
 * Uncategorized (which stays last). Indexes count only the movable categories.
 */
export function moveCategory(
  dashboard: Dashboard,
  fromIndex: number,
  toIndex: number,
): { dashboard: Dashboard; categoryIds: number[] } | null {
  if (fromIndex === toIndex) {
    return null
  }

  const movable = dashboard.categories.filter((c) => !c.isSystem)
  const pinned = dashboard.categories.filter((c) => c.isSystem)
  if (!movable[fromIndex]) {
    return null
  }

  const [moved] = movable.splice(fromIndex, 1)
  movable.splice(toIndex, 0, moved)
  return {
    dashboard: { categories: [...movable, ...pinned] },
    categoryIds: movable.map((c) => c.id),
  }
}
