import type { Dashboard } from '../../api/client.ts'

/** Bookmark ids per category id, the working copy a drag rearranges before it's saved. */
export type Containers = Record<number, number[]>

export function toContainers(dashboard: Dashboard): Containers {
  return Object.fromEntries(dashboard.categories.map((c) => [c.id, c.bookmarks.map((b) => b.id)]))
}

export function findContainer(containers: Containers, bookmarkId: number): number | undefined {
  const entry = Object.entries(containers).find(([, ids]) => ids.includes(bookmarkId))
  return entry ? Number(entry[0]) : undefined
}

/**
 * Moves a bookmark into another category's list while dragging over it: before the card it's
 * over, or at the end when over the category itself (overIndex undefined). Same category: unchanged.
 */
export function moveAcross(
  containers: Containers,
  bookmarkId: number,
  toCategory: number,
  overIndex: number | undefined,
): Containers {
  const fromCategory = findContainer(containers, bookmarkId)
  if (fromCategory === undefined || fromCategory === toCategory || !(toCategory in containers)) {
    return containers
  }

  const target = [...containers[toCategory]]
  target.splice(overIndex ?? target.length, 0, bookmarkId)
  return {
    ...containers,
    [fromCategory]: containers[fromCategory].filter((id) => id !== bookmarkId),
    [toCategory]: target,
  }
}
