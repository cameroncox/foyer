import { expect, it, vi } from 'vitest'

import { navigation } from './navigation.ts'

it('opens bookmarks in a new tab without giving it a handle on Foyer', () => {
  const open = vi.spyOn(window, 'open').mockImplementation(() => null)

  navigation.open('https://opnsense.lan')

  expect(open).toHaveBeenCalledWith('https://opnsense.lan', '_blank', 'noopener,noreferrer')
})
