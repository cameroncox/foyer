import type { Bookmark } from '../../api/client.ts'

/**
 * Who a shared card is from, for its icon's tooltip: "Shared by cameron" or "Shared from vendor"
 * for another profile's, "Shared with every profile" for the page's own; null when not shared.
 */
export function sharedLabel(bookmark: Bookmark): string | null {
  if (!bookmark.isShared) {
    return null
  }

  if (bookmark.sharedBy) {
    return `Shared by ${bookmark.sharedBy}`
  }

  if (bookmark.sharedFrom) {
    return `Shared from ${bookmark.sharedFrom}`
  }

  return 'Shared with every profile'
}

/** A Docker bookmark this profile shows from Default (Show Docker bookmarks) without it being shared. */
export const isShownDocker = (bookmark: Bookmark) =>
  !bookmark.canEdit && !bookmark.isShared && bookmark.docker != null

/** Where a read-only card comes from: its share, or Default's Docker hosts. */
export function sourceLabel(bookmark: Bookmark): string | null {
  return isShownDocker(bookmark) ? 'From Default’s Docker hosts' : sharedLabel(bookmark)
}

/** Who can change another profile's shared bookmark, for read-only notes. */
export function ownerName(bookmark: Bookmark): string {
  return bookmark.sharedBy ?? bookmark.sharedFrom ?? 'its owner'
}

/** Another profile's shared bookmark: shown here, changed only by its owner. */
export const isReadOnly = (bookmark: Bookmark) => !bookmark.canEdit

/**
 * Why a category can't be deleted, worded as the server's refusal: it holds bookmarks other
 * profiles shared, which only their owners can move. Shared Docker bookmarks don't count; they
 * move to Uncategorized. Null when it can go.
 */
export function blockedDelete(category: { name: string; bookmarks: readonly Bookmark[] }) {
  const blocking = category.bookmarks.filter((b) => !b.canEdit && !b.docker)
  if (blocking.length === 0) {
    return null
  }

  const owners = [...new Set(blocking.map(ownerName))].sort((a, b) =>
    a.localeCompare(b, undefined, { sensitivity: 'base' }),
  )
  const who =
    owners.length === 1
      ? owners[0]
      : `${owners.slice(0, -1).join(', ')} and ${owners[owners.length - 1]}`
  const count = blocking.length === 1 ? '1 bookmark' : `${blocking.length} bookmarks`
  return (
    `${category.name} holds ${count} shared by ${who}, so it can't be deleted. ` +
    `Move your own bookmarks out, or ask ${who} to unshare.`
  )
}
