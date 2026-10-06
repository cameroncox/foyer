import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { Bookmark, Dashboard, Me, Profile } from '../../api/client.ts'
import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category, defaultProfile, dockerBookmark, profile } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'
import { facets, spotlightHits } from '../spotlight/scope.ts'
import { blockedDelete, sharedLabel } from './shared.ts'

const personal = profile('cameron', { id: 2, kind: 'personal', canDelete: false })

function me(current: Profile, extra: Partial<Me> = {}): Me {
  const home = defaultProfile({ id: 1, canEdit: current.kind === 'default' && current.canEdit })
  return {
    profilesEnabled: true,
    user: 'cameron',
    current,
    canEditDefault: home.canEdit,
    profiles: [home, personal],
    handoverCount: 0,
    ...extra,
  }
}

const fromDefault = (name: string, extra: Partial<Bookmark> = {}) =>
  bookmark(name, { isShared: true, canEdit: false, sharedFrom: 'Default', ...extra })

/** cameron's personal profile: their own Wiki, Grocery list (shared), and Jellyfin from Default. */
function personalPage(): Dashboard {
  return placed({
    categories: [
      category('Media', [
        dockerBookmark('Jellyfin', 'running', {
          isShared: true,
          canEdit: false,
          sharedFrom: 'Default',
        }),
      ]),
      category('Household', [
        bookmark('Grocery list', { isShared: true }),
        fromDefault('Family photos', { sharedBy: 'alex', sharedFrom: 'alex' }),
      ]),
      category('Uncategorized', [bookmark('Wiki')], true),
    ],
  })
}

/** Gives each bookmark its category's id, as the API does. */
function placed(dashboard: Dashboard): Dashboard {
  for (const c of dashboard.categories) {
    c.bookmarks.forEach((b) => (b.categoryId = c.id))
  }

  return dashboard
}

describe('Sharing', () => {
  it('marks shared cards with who they’re from', async () => {
    stubApi(personalPage, { 'GET /api/me': () => me(personal) })
    renderApp()

    const jellyfin = (await screen.findByText('Jellyfin')).closest('a')!
    expect(within(jellyfin).getByRole('img', { name: 'Shared from Default' })).toBeInTheDocument()
    const photos = screen.getByText('Family photos').closest('a')!
    expect(within(photos).getByRole('img', { name: 'Shared by alex' })).toBeInTheDocument()
    const grocery = screen.getByText('Grocery list').closest('a')!
    expect(
      within(grocery).getByRole('img', { name: 'Shared with every profile' }),
    ).toBeInTheDocument()
    expect(
      within(screen.getByText('Wiki').closest('a')!).queryByRole('img', { name: /Shared/ }),
    ).toBeNull()
  })

  it('makes another profile’s shared cards read-only in edit mode', async () => {
    stubApi(personalPage, { 'GET /api/me': () => me(personal) })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))
    expect(screen.queryByRole('button', { name: 'Delete Family photos' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Delete Grocery list' })).toBeInTheDocument()
    expect(screen.getByText('Shared by alex')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'View Family photos' }))
    const panel = await screen.findByRole('dialog')
    expect(within(panel).getByText(/Only alex can change this/)).toBeInTheDocument()
    expect(within(panel).getByText('Household')).toBeInTheDocument()
    expect(within(panel).getByRole('link', { name: 'Open' })).toHaveAttribute(
      'href',
      'https://familyphotos.lan',
    )
  })

  it('leaves another profile’s shared cards out of a selection', async () => {
    stubApi(personalPage, { 'GET /api/me': () => me(personal) })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))
    await userEvent.click(screen.getByRole('button', { name: /Select/ }))
    expect(screen.getByRole('checkbox', { name: 'Select Family photos' })).toBeDisabled()
    expect(screen.getByRole('checkbox', { name: 'Select Grocery list' })).toBeEnabled()
  })

  it('shares a new bookmark from the Add form', async () => {
    const api = stubApi(personalPage, { 'GET /api/me': () => me(personal) })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Add bookmark' }))
    await userEvent.type(screen.getByLabelText('Name'), 'Recipes')
    await userEvent.type(screen.getByLabelText('URL'), 'https://recipes.example.com')
    expect(screen.getByText('Only this profile shows it.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('switch', { name: /Shared/ }))
    expect(
      screen.getByText(/Every profile sees it, read-only, in a category named Uncategorized/),
    ).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Add bookmark' }))

    await waitFor(() => expect(api.called('POST /api/bookmarks')).toHaveLength(1))
    expect(api.called('POST /api/bookmarks')[0].body).toMatchObject({
      name: 'Recipes',
      isShared: true,
    })
  })

  it('asks before it stops sharing', async () => {
    const api = stubApi(personalPage, { 'GET /api/me': () => me(personal) })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))
    await userEvent.click(screen.getByRole('button', { name: 'Edit Grocery list' }))
    await userEvent.click(screen.getByRole('switch', { name: /Shared/ }))
    expect(screen.getByText(/Stop sharing “Grocery list”\?/)).toBeInTheDocument()
    expect(screen.getByRole('switch', { name: /Shared/ })).toBeChecked()

    await userEvent.click(screen.getByRole('button', { name: 'Keep sharing' }))
    expect(screen.getByRole('switch', { name: /Shared/ })).toBeChecked()

    await userEvent.click(screen.getByRole('switch', { name: /Shared/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Stop sharing' }))
    expect(screen.getByRole('switch', { name: /Shared/ })).not.toBeChecked()
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(api.calls.find((c) => c.method === 'PUT')!.body).toMatchObject({ isShared: false })
  })

  it('shows no Shared switch with profiles off', async () => {
    stubApi(personalPage)
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Add bookmark' }))
    expect(screen.queryByRole('switch', { name: /Shared/ })).toBeNull()
  })

  it('lets Default’s editors share Docker bookmarks, under a banner', async () => {
    window.history.replaceState(null, '', '/default')
    const home = defaultProfile({ id: 1, canEdit: true })
    const api = stubApi(
      () => ({ categories: [category('Media', [dockerBookmark('Sonarr', 'running')])] }),
      { 'GET /api/me': () => me(home) },
    )
    renderApp()

    const banner = await screen.findByRole('note')
    expect(banner).toHaveTextContent('You’re editing Default.')
    await userEvent.click(screen.getByRole('button', { name: 'Edit page' }))
    await userEvent.click(screen.getByRole('button', { name: 'Edit category and tags of Sonarr' }))
    await userEvent.click(screen.getByRole('switch', { name: /Shared/ }))
    expect(screen.getByText(/with its status dot/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(api.calls.find((c) => c.method === 'PUT')!.body).toMatchObject({
      isShared: true,
      name: null,
    })

    await userEvent.click(within(banner).getByRole('button', { name: 'Back to cameron' }))
    expect(window.location.pathname).toBe('/cameron')
  })

  it('keeps Default’s banner closed on this device once dismissed', async () => {
    window.history.replaceState(null, '', '/default')
    const home = defaultProfile({ id: 1, canEdit: true })
    stubApi(personalPage, { 'GET /api/me': () => me(home) })
    const { unmount } = renderApp()

    const banner = await screen.findByRole('note')
    await userEvent.click(within(banner).getByRole('button', { name: 'Dismiss' }))
    expect(screen.queryByRole('note')).toBeNull()
    unmount()

    renderApp()
    await screen.findByRole('button', { name: 'Profile: Default' })
    expect(screen.queryByText('You’re editing Default.')).toBeNull()
  })

  it('offers no adding or editing on a read-only profile', async () => {
    window.history.replaceState(null, '', '/default')
    stubApi(
      () => ({
        categories: [
          category('Media', [fromDefault('Jellyfin', { sharedFrom: null, isShared: false })]),
        ],
      }),
      {
        'GET /api/me': () => me(defaultProfile({ id: 1, canEdit: false })),
      },
    )
    renderApp()

    await screen.findByText('Jellyfin')
    expect(screen.queryByRole('button', { name: 'Add bookmark' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Edit page' })).toBeNull()
    expect(screen.queryByRole('note')).toBeNull()
  })

  it('explains why a category holding someone else’s shares can’t be deleted', async () => {
    stubApi(personalPage, { 'GET /api/me': () => me(personal) })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))
    const drawer = await screen.findByRole('complementary')
    await userEvent.click(within(drawer).getByRole('button', { name: 'Delete Household' }))
    expect(
      within(drawer).getByText(
        'Household holds 1 bookmark shared by alex, so it can’t be deleted. Move your own bookmarks out, or ask alex to unshare.'.replace(
          '’',
          "'",
        ),
      ),
    ).toBeInTheDocument()

    // Shared Docker bookmarks don't block it; they move to Uncategorized.
    await userEvent.click(within(drawer).getByRole('button', { name: 'OK' }))
    await userEvent.click(within(drawer).getByRole('button', { name: 'Delete Media' }))
    expect(within(drawer).getByText(/moves to Uncategorized/)).toBeInTheDocument()
  })
})

describe('Handover', () => {
  it('offers Default’s bookmarks on the personal profile, and moves them', async () => {
    let offered = 3
    const api = stubApi(personalPage, {
      'GET /api/me': () => me(personal, { handoverCount: offered }),
      'POST /api/me/handover': () => {
        offered = 0
        return { moved: 3 }
      },
    })
    renderApp()

    expect(
      await screen.findByText('Default has 3 bookmarks from before profiles.'),
    ).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Move them here' }))

    expect(await screen.findByText('Moved 3 bookmarks to cameron')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText(/from before profiles/)).toBeNull())
    expect(api.called('POST /api/me/handover')).toHaveLength(1)
  })

  it('leaves them in Default when asked', async () => {
    let offered = 1
    const api = stubApi(personalPage, {
      'GET /api/me': () => me(personal, { handoverCount: offered }),
      'DELETE /api/me/handover': () => {
        offered = 0
        return new Response(null, { status: 204 })
      },
    })
    renderApp()

    expect(
      await screen.findByText('Default has 1 bookmark from before profiles.'),
    ).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Leave them in Default' }))

    await waitFor(() => expect(screen.queryByText(/from before profiles/)).toBeNull())
    expect(api.called('DELETE /api/me/handover')).toHaveLength(1)
    expect(api.called('POST /api/me/handover')).toHaveLength(0)
  })

  it('makes no offer away from the personal profile', async () => {
    window.history.replaceState(null, '', '/default')
    const home = defaultProfile({ id: 1, canEdit: true })
    stubApi(personalPage, { 'GET /api/me': () => me(home, { handoverCount: 3 }) })
    renderApp()

    await screen.findByText('You’re editing Default.')
    expect(screen.queryByText(/from before profiles/)).toBeNull()
  })
})

describe('Docker bookmarks shown from Default', () => {
  it('labels them as from Default’s Docker hosts, with no shared badge', async () => {
    const shown = dockerBookmark('Sonarr', 'running', { canEdit: false, sharedFrom: 'Default' })
    stubApi(() => ({ categories: [category('Media', [shown])] }), {
      'GET /api/me': () => me(personal),
    })
    renderApp()

    await screen.findByText('Sonarr')
    expect(screen.queryByLabelText(/Shared/)).toBeNull()
    await userEvent.click(screen.getByRole('button', { name: 'Edit page' }))
    expect(await screen.findByText('From Default’s Docker hosts')).toBeInTheDocument()
  })
})

describe('sharing helpers', () => {
  it('labels shared bookmarks by who they’re from', () => {
    expect(
      sharedLabel(bookmark('a', { isShared: true, sharedBy: 'alex', sharedFrom: 'alex' })),
    ).toBe('Shared by alex')
    expect(sharedLabel(bookmark('a', { isShared: true, sharedFrom: 'vendor' }))).toBe(
      'Shared from vendor',
    )
    expect(sharedLabel(bookmark('a', { isShared: true }))).toBe('Shared with every profile')
    expect(sharedLabel(bookmark('a'))).toBeNull()
  })

  it('names every owner blocking a delete', () => {
    const message = blockedDelete({
      name: 'Read Later',
      bookmarks: [
        fromDefault('a', { sharedBy: 'alex' }),
        fromDefault('b', { sharedFrom: 'vendor' }),
        fromDefault('c', { sharedBy: 'alex' }),
        bookmark('mine'),
      ],
    })
    expect(message).toBe(
      "Read Later holds 3 bookmarks shared by alex and vendor, so it can't be deleted. Move your own bookmarks out, or ask alex and vendor to unshare.",
    )
    expect(blockedDelete({ name: 'Mine', bookmarks: [bookmark('mine')] })).toBeNull()
  })

  it('offers a Shared filter in search', () => {
    const page = personalPage().categories
    expect(facets(page).shared).toEqual([{ kind: 'shared', value: 'Shared', count: 3 }])
    expect(
      spotlightHits(page, { kind: 'shared', value: 'Shared' }, '').map((h) => h.bookmark.name),
    ).toEqual(['Jellyfin', 'Grocery list', 'Family photos'])
  })
})
