import { describe, expect, it } from 'vitest'

import type { Dashboard } from '../../api/client.ts'
import { bookmark, category, dockerBookmark } from '../../test/fakes.ts'
import { moveBookmark, moveCategory } from './reorder.ts'

function page(): Dashboard {
  return {
    categories: [
      category('Media', [bookmark('A'), bookmark('B'), dockerBookmark('C', 'running')]),
      category('Network', [bookmark('D')]),
      category('Uncategorized', [bookmark('E')], true),
    ],
  }
}

const names = (d: Dashboard, i: number) => d.categories[i].bookmarks.map((b) => b.name)

describe('moveBookmark', () => {
  it('reorders within a category and sends its full order', () => {
    const before = page()
    const [media] = before.categories

    const result = moveBookmark(
      before,
      { categoryId: media.id, index: 0 },
      { categoryId: media.id, index: 2 },
    )!

    expect(names(result.dashboard, 0)).toEqual(['B', 'C', 'A'])
    expect(result.request).toEqual({
      categoryId: media.id,
      bookmarkIds: [media.bookmarks[1].id, media.bookmarks[2].id, media.bookmarks[0].id],
    })
    expect(names(before, 0)).toEqual(['A', 'B', 'C'])
  })

  it('moves to another category, locking a Docker card there', () => {
    const before = page()
    const [media, network] = before.categories

    const result = moveBookmark(
      before,
      { categoryId: media.id, index: 2 },
      { categoryId: network.id, index: 0 },
    )!

    expect(names(result.dashboard, 0)).toEqual(['A', 'B'])
    expect(names(result.dashboard, 1)).toEqual(['C', 'D'])
    const moved = result.dashboard.categories[1].bookmarks[0]
    expect(moved.categoryId).toBe(network.id)
    expect(moved.docker!.categoryOverridden).toBe(true)
    expect(result.request.categoryId).toBe(network.id)
  })

  it('is null when dropped where it started', () => {
    const before = page()
    const id = before.categories[0].id
    expect(
      moveBookmark(before, { categoryId: id, index: 1 }, { categoryId: id, index: 1 }),
    ).toBeNull()
  })
})

describe('moveCategory', () => {
  it('reorders categories, keeping Uncategorized last', () => {
    const before = page()

    const result = moveCategory(before, 1, 0)!

    expect(result.dashboard.categories.map((c) => c.name)).toEqual([
      'Network',
      'Media',
      'Uncategorized',
    ])
    expect(result.categoryIds).toEqual([before.categories[1].id, before.categories[0].id])
  })
})
