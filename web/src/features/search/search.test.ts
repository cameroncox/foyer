import { describe, expect, it } from 'vitest'

import type { Bookmark, DashboardCategory } from '../../api/client.ts'
import { searchBookmarks } from './search.ts'

let nextId = 1
function bookmark(name: string, extra: Partial<Bookmark> = {}): Bookmark {
  return {
    id: nextId++,
    source: 'manual',
    name,
    url: `https://${name.toLowerCase()}.lan`,
    icon: null,
    iconUrl: null,
    categoryId: 1,
    tags: [],
    hostTag: null,
    status: null,
    docker: null,
    isShared: false,
    canEdit: true,
    sharedBy: null,
    sharedFrom: null,
    ...extra,
  }
}

function category(name: string, bookmarks: Bookmark[]): DashboardCategory {
  return { id: nextId++, name, isSystem: false, bookmarks }
}

const page = [
  category('Media', [
    bookmark('Jellyfin', { hostTag: 'docker-4' }),
    bookmark('Sonarr', { tags: ['arr'], hostTag: 'docker-4' }),
    bookmark('Requests', { tags: ['jellyseerr'] }),
  ]),
  category('Network', [
    bookmark('OPNsense', { url: 'https://router.lan' }),
    bookmark('Radarr', { tags: ['arr'] }),
  ]),
]

const names = (hits: ReturnType<typeof searchBookmarks>) => hits.map((h) => h.bookmark.name)

describe('searchBookmarks', () => {
  it('returns nothing for a blank query', () => {
    expect(searchBookmarks(page, '   ')).toEqual([])
  })

  it('puts name matches first, then other matches, each in page order', () => {
    expect(names(searchBookmarks(page, 'arr'))).toEqual(['Sonarr', 'Radarr'])
    expect(names(searchBookmarks(page, 'jelly'))).toEqual(['Jellyfin', 'Requests'])
  })

  it('matches tags and reports the tag that matched', () => {
    const [hit] = searchBookmarks(page, 'JELLYSEERR')
    expect(hit.bookmark.name).toBe('Requests')
    expect(hit.matchedTag).toBe('jellyseerr')
  })

  it('matches the host tag', () => {
    const hits = searchBookmarks(page, 'docker-4')
    expect(names(hits)).toEqual(['Jellyfin', 'Sonarr'])
    expect(hits[0].matchedTag).toBe('docker-4')
  })

  it('matches the category name and the URL', () => {
    expect(names(searchBookmarks(page, 'network'))).toEqual(['OPNsense', 'Radarr'])
    expect(names(searchBookmarks(page, 'router.lan'))).toEqual(['OPNsense'])
  })
})
