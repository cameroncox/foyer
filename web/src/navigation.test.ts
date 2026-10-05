import { expect, it, vi } from 'vitest'

import { navigation } from './navigation.ts'

it('opens bookmarks in this tab', () => {
  const assign = vi.fn()
  vi.stubGlobal('location', { ...window.location, assign })

  navigation.open('https://opnsense.lan')

  expect(assign).toHaveBeenCalledWith('https://opnsense.lan')
  vi.unstubAllGlobals()
})
