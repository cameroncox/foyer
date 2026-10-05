import { describe, expect, it } from 'vitest'

import type { Bookmark, DashboardCategory } from '../../api/client.ts'
import { facets, matchingFacets, spotlightHits } from './scope.ts'

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
    bookmark('Plex', { hostTag: 'docker-2' }),
  ]),
  category('Empty', []),
  category('Network', [
    bookmark('OPNsense', { tags: ['router'] }),
    bookmark('Radarr', { tags: ['arr'] }),
  ]),
]

const names = (hits: ReturnType<typeof spotlightHits>) => hits.map((h) => h.bookmark.name)

describe('facets', () => {
  it('counts hosts and tags alphabetically and non-empty categories in page order', () => {
    const all = facets(page)

    expect(all.hosts.map((f) => [f.value, f.count])).toEqual([
      ['docker-2', 1],
      ['docker-4', 2],
    ])
    expect(all.tags.map((f) => [f.value, f.count])).toEqual([
      ['arr', 2],
      ['router', 1],
    ])
    expect(all.categories.map((f) => [f.value, f.count])).toEqual([
      ['Media', 3],
      ['Network', 2],
    ])
  })
})

describe('matchingFacets', () => {
  it('matches facet names, ignoring a leading #', () => {
    expect(matchingFacets(facets(page), '#dock').map((f) => f.value)).toEqual([
      'docker-2',
      'docker-4',
    ])
    expect(matchingFacets(facets(page), 'net').map((f) => [f.kind, f.value])).toEqual([
      ['category', 'Network'],
    ])
    expect(matchingFacets(facets(page), ' ')).toEqual([])
  })
})

describe('spotlightHits', () => {
  it('is empty with no scope and no query', () => {
    expect(spotlightHits(page, null, '')).toEqual([])
  })

  it('lists the whole scope when nothing is typed', () => {
    expect(names(spotlightHits(page, { kind: 'host', value: 'docker-4' }, ''))).toEqual([
      'Jellyfin',
      'Sonarr',
    ])
    expect(names(spotlightHits(page, { kind: 'tag', value: 'arr' }, ''))).toEqual([
      'Sonarr',
      'Radarr',
    ])
    expect(names(spotlightHits(page, { kind: 'category', value: 'Network' }, ''))).toEqual([
      'OPNsense',
      'Radarr',
    ])
  })

  it('searches within the scope', () => {
    expect(names(spotlightHits(page, { kind: 'tag', value: 'arr' }, 'rad'))).toEqual(['Radarr'])
    expect(names(spotlightHits(page, null, 'arr'))).toEqual(['Sonarr', 'Radarr'])
  })
})
