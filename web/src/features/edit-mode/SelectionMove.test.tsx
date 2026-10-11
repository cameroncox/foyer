import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { Dashboard, Me } from '../../api/client.ts'
import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category, defaultProfile, dockerBookmark, profile } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'

const home = defaultProfile({ id: 1, canEdit: true })
const personal = profile('cameron', { id: 2, kind: 'personal', canDelete: false })
const vendor = profile('vendor', { id: 4, kind: 'ownerless' })

/** cameron on Default, which they can edit. */
const me = (): Me => ({
  profilesEnabled: true,
  user: 'cameron',
  current: home,
  canEditDefault: true,
  profiles: [home, personal, vendor],
  canShareWithEveryone: true,
})

function page(): Dashboard {
  return {
    categories: [
      category('Infrastructure', [
        bookmark('Router', { id: 101 }),
        dockerBookmark('Sonarr', 'running', { id: 103 }),
      ]),
      category('Uncategorized', [bookmark('Printer', { id: 104 })], true),
    ],
  }
}

async function selectRouter() {
  window.history.replaceState(null, '', '/default')
  await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))
  await userEvent.click(screen.getByRole('button', { name: 'Select' }))
  await userEvent.click(screen.getByRole('checkbox', { name: 'Select Router' }))
  await userEvent.click(screen.getByRole('button', { name: 'Move to' }))
}

describe('Moving bookmarks to another profile', () => {
  it('offers the other profiles the caller can edit, and moves there', async () => {
    const api = stubApi(page, {
      'GET /api/me': me,
      'POST /api/bookmarks/move': () => ({ moved: 1 }),
    })
    renderApp()
    await selectRouter()

    const menu = await screen.findByRole('menu')
    expect(within(menu).queryByRole('menuitem', { name: 'Default' })).toBeNull()
    expect(within(menu).getByRole('menuitem', { name: 'vendor' })).toBeInTheDocument()
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'cameron' }))

    await waitFor(() => expect(api.called('POST /api/bookmarks/move')).toHaveLength(1))
    expect(api.called('POST /api/bookmarks/move')[0].body).toEqual({
      bookmarkIds: [101],
      profileId: 2,
    })
    expect(await screen.findByText('Moved 1 bookmark to cameron')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Select' })).toBeInTheDocument()
  })

  it('makes a new profile and moves there', async () => {
    const api = stubApi(page, {
      'GET /api/me': me,
      'POST /api/profiles': () => profile('work', { id: 9 }),
      'POST /api/bookmarks/move': () => ({ moved: 1 }),
    })
    renderApp()
    await selectRouter()

    await userEvent.click(await screen.findByRole('menuitem', { name: 'New profile…' }))
    await userEvent.type(await screen.findByRole('textbox', { name: /Name/ }), 'work')
    await userEvent.click(screen.getByRole('button', { name: 'Create and move 1' }))

    await waitFor(() => expect(api.called('POST /api/bookmarks/move')).toHaveLength(1))
    expect(api.called('POST /api/profiles')[0].body).toEqual({ name: 'work' })
    expect(api.called('POST /api/bookmarks/move')[0].body).toEqual({
      bookmarkIds: [101],
      profileId: 9,
    })
    expect(await screen.findByText('Moved 1 bookmark to work')).toBeInTheDocument()
  })

  it('isn’t offered with profiles off', async () => {
    stubApi(page)
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))
    await userEvent.click(screen.getByRole('button', { name: 'Select' }))
    expect(screen.queryByRole('button', { name: 'Move to' })).toBeNull()
  })
})
