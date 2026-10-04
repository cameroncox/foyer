import { describe, expect, it } from 'vitest'

import { bookmark, category } from '../../test/fakes.ts'
import { findContainer, moveAcross, toContainers } from './containers.ts'

const media = category('Media', [bookmark('A'), bookmark('B')])
const tools = category('Tools', [bookmark('C')])
const empty = category('Empty', [])
const ids = (c: typeof media) => c.bookmarks.map((b) => b.id)
const containers = toContainers({ categories: [media, tools, empty] })

describe('containers', () => {
  it('maps categories to their bookmark ids', () => {
    expect(containers).toEqual({ [media.id]: ids(media), [tools.id]: ids(tools), [empty.id]: [] })
    expect(findContainer(containers, tools.bookmarks[0].id)).toBe(tools.id)
  })

  it('moves a card before the one it is over', () => {
    const a = media.bookmarks[0].id
    const moved = moveAcross(containers, a, tools.id, 0)
    expect(moved[media.id]).toEqual([media.bookmarks[1].id])
    expect(moved[tools.id]).toEqual([a, tools.bookmarks[0].id])
  })

  it('appends when over an empty category', () => {
    const b = media.bookmarks[1].id
    expect(moveAcross(containers, b, empty.id, undefined)[empty.id]).toEqual([b])
  })

  it('leaves a move within the same category alone', () => {
    expect(moveAcross(containers, media.bookmarks[0].id, media.id, 1)).toBe(containers)
  })
})
