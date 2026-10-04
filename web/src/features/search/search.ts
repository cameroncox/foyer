import type { Bookmark, DashboardCategory } from '../../api/client.ts'

export interface SearchHit {
  bookmark: Bookmark
  /** The tag (host tag included) that matched, to highlight on the card. */
  matchedTag?: string
}

/**
 * Bookmarks matching `query` (case-insensitive) in name, tags, host tag, category or URL.
 * Name matches come first, then the rest; each group keeps page order.
 */
export function searchBookmarks(
  categories: readonly DashboardCategory[],
  query: string,
): SearchHit[] {
  const needle = query.trim().toLowerCase()
  if (needle === '') {
    return []
  }

  const byName: SearchHit[] = []
  const byOther: SearchHit[] = []
  const has = (text: string | null | undefined) => !!text && text.toLowerCase().includes(needle)

  for (const category of categories) {
    for (const bookmark of category.bookmarks) {
      const tags = [...(bookmark.hostTag ? [bookmark.hostTag] : []), ...bookmark.tags]
      const matchedTag = tags.find(has)
      if (has(bookmark.name)) {
        byName.push({ bookmark, matchedTag })
      } else if (matchedTag || has(category.name) || has(bookmark.url)) {
        byOther.push({ bookmark, matchedTag })
      }
    }
  }

  return [...byName, ...byOther]
}
