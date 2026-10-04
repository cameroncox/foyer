import { describe, expect, it } from 'vitest'

import { bookmark, dockerBookmark } from '../../test/fakes.ts'
import { allState, stillPresent, toggleAll, toggleOne } from './selection.ts'

describe('selection', () => {
  const router = bookmark('Router', { id: 1 })
  const nas = bookmark('NAS', { id: 2 })
  const sonarr = dockerBookmark('Sonarr', 'running', { id: 3 })

  it('toggles one id', () => {
    expect([...toggleOne(new Set([1]), 2)]).toEqual([1, 2])
    expect([...toggleOne(new Set([1, 2]), 1)]).toEqual([2])
  })

  it('selects a category’s manual bookmarks, then clears them once all are picked', () => {
    const once = toggleAll(new Set(), [router, nas, sonarr])
    expect([...once]).toEqual([1, 2])
    expect([...toggleAll(once, [router, nas, sonarr])]).toEqual([])
  })

  it('reports the heading checkbox state, ignoring Docker bookmarks', () => {
    expect(allState(new Set([1]), [router, nas, sonarr])).toEqual({
      selectable: true,
      checked: false,
      indeterminate: true,
    })
    expect(allState(new Set([1, 2]), [router, nas, sonarr]).checked).toBe(true)
    expect(allState(new Set(), [sonarr]).selectable).toBe(false)
  })

  it('forgets ids that are gone', () => {
    expect([...stillPresent(new Set([1, 9]), [1, 2])]).toEqual([1])
  })
})
