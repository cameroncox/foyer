import type { Bookmark, DashboardCategory } from '../../api/client.ts'
import { type SearchHit, searchBookmarks } from '../search/search.ts'

export type ScopeKind = 'host' | 'tag' | 'category'

/** A filter picked in the spotlight: one Docker host, tag or category. */
export interface Scope {
  kind: ScopeKind
  value: string
}

export interface Facet extends Scope {
  /** Bookmarks the filter leaves. */
  count: number
}

export interface Facets {
  hosts: Facet[]
  tags: Facet[]
  categories: Facet[]
}

/**
 * The filters the spotlight offers before anything is typed: every Docker host and tag
 * (alphabetical) and every non-empty category (page order), each with its bookmark count.
 */
export function facets(categories: readonly DashboardCategory[]): Facets {
  const hosts = new Map<string, number>()
  const tags = new Map<string, number>()
  const bump = (map: Map<string, number>, key: string) => map.set(key, (map.get(key) ?? 0) + 1)

  for (const category of categories) {
    for (const bookmark of category.bookmarks) {
      if (bookmark.hostTag) {
        bump(hosts, bookmark.hostTag)
      }

      new Set(bookmark.tags).forEach((tag) => bump(tags, tag))
    }
  }

  const sorted = (kind: ScopeKind, map: Map<string, number>): Facet[] =>
    [...map]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([value, count]) => ({ kind, value, count }))

  return {
    hosts: sorted('host', hosts),
    tags: sorted('tag', tags),
    categories: categories
      .filter((c) => c.bookmarks.length > 0)
      .map((c) => ({ kind: 'category', value: c.name, count: c.bookmarks.length })),
  }
}

/** Facets whose name contains `query` (case-insensitive), for offering filters while typing. */
export function matchingFacets(all: Facets, query: string): Facet[] {
  const needle = query.trim().toLowerCase().replace(/^#/, '')
  if (needle === '') {
    return []
  }

  return [...all.hosts, ...all.tags, ...all.categories].filter((f) =>
    f.value.toLowerCase().includes(needle),
  )
}

function inScope(category: DashboardCategory, bookmark: Bookmark, scope: Scope): boolean {
  switch (scope.kind) {
    case 'host':
      return bookmark.hostTag === scope.value
    case 'tag':
      return bookmark.tags.includes(scope.value)
    case 'category':
      return category.name === scope.value
  }
}

/**
 * Spotlight results: bookmarks in `scope` (all of them when there's none) that match `query`.
 * With a scope and no query, the whole scope in page order; with neither, nothing.
 */
export function spotlightHits(
  categories: readonly DashboardCategory[],
  scope: Scope | null,
  query: string,
): SearchHit[] {
  const scoped = scope
    ? categories.map((c) => ({ ...c, bookmarks: c.bookmarks.filter((b) => inScope(c, b, scope)) }))
    : categories

  if (query.trim() === '') {
    return scope ? scoped.flatMap((c) => c.bookmarks.map((bookmark) => ({ bookmark }))) : []
  }

  return searchBookmarks(scoped, query)
}

export function scopeLabel(scope: Scope): string {
  return scope.kind === 'category' ? scope.value : `#${scope.value}`
}
