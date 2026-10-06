import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { Me, Profile } from '../../api/client.ts'
import { type ApiCall, problem, stubApi } from '../../test/fakeApi.ts'
import { bookmark, category, defaultProfile, FakeEventSource, profile } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'
import { bookmarkletHref } from '../quick-add/bookmarklet.ts'

const home = defaultProfile({ id: 1, canEdit: false })
const personal = profile('cameron_example.com', {
  id: 2,
  slug: 'cameron-example-com',
  kind: 'personal',
  canDelete: false,
})
const work = profile('work', { id: 3 })
const vendor = profile('vendor', { id: 4, kind: 'ownerless' })
const all = [home, personal, work, vendor]

/** /api/me for cameron, resolving X-Foyer-Profile like the server; unknown slugs are 404. */
function meFor(profiles: Profile[] = all, handover = { count: 0 }) {
  const asked: (string | null)[] = []
  const handler = (call: ApiCall) => {
    asked.push(call.profile)
    const current = call.profile
      ? profiles.find((p) => p.slug === call.profile)
      : profiles.find((p) => p.kind === 'personal')
    if (!current) {
      return problem(404, `There's no profile '${call.profile}'.`)
    }

    const me: Me = {
      profilesEnabled: true,
      user: 'cameron_example.com',
      current,
      canEditDefault: false,
      profiles,
      handoverCount: handover.count,
    }
    return me
  }

  return { asked, handler }
}

const page = () => ({
  categories: [category('Uncategorized', [bookmark('Wiki')], true)],
})

function at(path: string) {
  window.history.replaceState(null, '', path)
}

describe('Profiles', () => {
  it('opens the personal profile at /, sending it with every call', async () => {
    const me = meFor()
    const dashboardFor: (string | null)[] = []
    const api = stubApi(page, {
      'GET /api/me': me.handler,
      'GET /api/dashboard': (call) => {
        dashboardFor.push(call.profile)
        return page()
      },
    })
    renderApp()

    expect(
      await screen.findByRole('button', { name: 'Profile: cameron_example.com' }),
    ).toBeInTheDocument()
    await screen.findByText('Wiki')
    expect(me.asked).toEqual([null])
    // StrictMode mounts the board twice in tests, so there can be two reads; both name the profile.
    expect(new Set(dashboardFor)).toEqual(new Set(['cameron-example-com']))
    expect(FakeEventSource.latest.url).toBe('/api/events?profile=cameron-example-com')

    await userEvent.click(screen.getByRole('button', { name: 'Edit page' }))
    const drawer = await screen.findByRole('complementary')
    await userEvent.type(within(drawer).getByPlaceholderText(/New category/), 'Tools{Enter}')
    await waitFor(() => expect(api.called('POST /api/categories')).toHaveLength(1))
    expect(api.called('POST /api/categories')[0].profile).toBe('cameron-example-com')
  })

  it('opens the profile the URL names', async () => {
    at('/work')
    const me = meFor()
    stubApi(page, { 'GET /api/me': me.handler })
    renderApp()

    expect(await screen.findByRole('button', { name: 'Profile: work' })).toBeInTheDocument()
    expect(me.asked).toEqual(['work'])
  })

  it('says so when the URL names a profile the caller can’t see', async () => {
    at('/alex')
    stubApi(page, { 'GET /api/me': meFor().handler })
    renderApp()

    expect(await screen.findByText('There’s no profile “alex”.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go home' })).toHaveAttribute('href', '/')
  })

  it('reopens the profile last picked on this device, and drops it once it’s gone', async () => {
    localStorage.setItem('foyer.profile', 'work')
    const me = meFor()
    stubApi(page, { 'GET /api/me': me.handler })
    const { unmount } = renderApp()
    expect(await screen.findByRole('button', { name: 'Profile: work' })).toBeInTheDocument()
    unmount()

    localStorage.setItem('foyer.profile', 'deleted')
    const later = meFor()
    stubApi(page, { 'GET /api/me': later.handler })
    renderApp()

    expect(
      await screen.findByRole('button', { name: 'Profile: cameron_example.com' }),
    ).toBeInTheDocument()
    expect(later.asked).toEqual(['deleted', null])
    expect(localStorage.getItem('foyer.profile')).toBeNull()
  })

  it('switches profiles from the picker, remembering the pick', async () => {
    const me = meFor()
    stubApi(page, { 'GET /api/me': me.handler })
    renderApp()

    await userEvent.click(
      await screen.findByRole('button', { name: 'Profile: cameron_example.com' }),
    )
    const menu = await screen.findByRole('menu')
    expect(within(menu).getByText('Yours')).toBeInTheDocument()
    expect(within(menu).getByText('Everyone’s')).toBeInTheDocument()
    expect(within(menu).getByText(/read-only for you/)).toBeInTheDocument()
    await userEvent.click(within(menu).getByRole('menuitem', { name: /vendor/ }))

    expect(await screen.findByRole('button', { name: 'Profile: vendor' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/vendor')
    expect(localStorage.getItem('foyer.profile')).toBe('vendor')
  })

  it('offers no editing on a read-only profile', async () => {
    at('/default')
    stubApi(page, { 'GET /api/me': meFor().handler })
    renderApp()

    await screen.findByRole('button', { name: 'Profile: Default' })
    expect(screen.queryByRole('button', { name: 'Edit page' })).toBeNull()
  })

  it('creates a profile and opens it', async () => {
    const profiles = [...all]
    const me = meFor(profiles)
    const api = stubApi(page, {
      'GET /api/me': me.handler,
      'POST /api/profiles': () => {
        const created = profile('kitchen', { id: 9 })
        profiles.push(created)
        return created
      },
    })
    renderApp()

    await userEvent.click(
      await screen.findByRole('button', { name: 'Profile: cameron_example.com' }),
    )
    await userEvent.click(await screen.findByRole('menuitem', { name: 'New profile' }))
    await userEvent.type(screen.getByLabelText('Name'), 'Kitchen')
    expect(screen.getByText(/\/kitchen$/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Create and open' }))

    expect(await screen.findByRole('button', { name: 'Profile: kitchen' })).toBeInTheDocument()
    expect(api.called('POST /api/profiles')[0].body).toEqual({ name: 'Kitchen' })
    expect(window.location.pathname).toBe('/kitchen')
  })

  it('shows why a name was refused', async () => {
    stubApi(page, {
      'GET /api/me': meFor().handler,
      'POST /api/profiles': () => problem(409, 'That name isn’t available.'),
    })
    renderApp()

    await userEvent.click(
      await screen.findByRole('button', { name: 'Profile: cameron_example.com' }),
    )
    await userEvent.click(await screen.findByRole('menuitem', { name: 'New profile' }))
    await userEvent.type(screen.getByLabelText('Name'), 'vendor')
    await userEvent.click(screen.getByRole('button', { name: 'Create and open' }))

    expect(await screen.findByText('That name isn’t available.')).toBeInTheDocument()

    await userEvent.clear(screen.getByLabelText('Name'))
    await userEvent.type(screen.getByLabelText('Name'), 'my kitchen')
    await userEvent.click(screen.getByRole('button', { name: 'Create and open' }))
    expect(await screen.findByText('Use letters, digits and hyphens only.')).toBeInTheDocument()
  })

  it('renames the current profile, following it to its new address', async () => {
    at('/work')
    const profiles = [...all]
    stubApi(page, {
      'GET /api/me': meFor(profiles).handler,
      'PUT /api/profiles/3': (call) => {
        const renamed = { ...work, name: 'office', slug: 'office' }
        profiles.splice(profiles.indexOf(work), 1, renamed)
        expect(call.body).toEqual({ name: 'office' })
        return renamed
      },
    })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Profile: work' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Rename work' }))
    await userEvent.clear(screen.getByLabelText('Name'))
    await userEvent.type(screen.getByLabelText('Name'), 'office')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByRole('button', { name: 'Profile: office' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/office')
  })

  it('deletes the current profile and goes home', async () => {
    at('/work')
    localStorage.setItem('foyer.profile', 'work')
    const profiles = [...all]
    const api = stubApi(page, {
      'GET /api/me': meFor(profiles).handler,
      'DELETE /api/profiles/3': () => {
        profiles.splice(profiles.indexOf(work), 1)
        return new Response(null, { status: 204 })
      },
    })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Profile: work' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Rename work' }))
    await userEvent.click(screen.getByRole('button', { name: 'Delete profile…' }))
    await userEvent.click(screen.getByRole('button', { name: 'Delete profile' }))

    expect(
      await screen.findByRole('button', { name: 'Profile: cameron_example.com' }),
    ).toBeInTheDocument()
    expect(api.called('DELETE /api/profiles/3')).toHaveLength(1)
    expect(window.location.pathname).toBe('/')
    expect(localStorage.getItem('foyer.profile')).toBeNull()
  })

  it('offers a rename on every row but Default, and no delete for a personal profile', async () => {
    stubApi(page, { 'GET /api/me': meFor().handler })
    renderApp()

    await userEvent.click(
      await screen.findByRole('button', { name: 'Profile: cameron_example.com' }),
    )
    await screen.findByRole('menu')
    expect(screen.queryByRole('button', { name: 'Rename Default' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Rename work' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Rename vendor' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Rename cameron_example.com' }))
    expect(await screen.findByLabelText('Name')).toHaveValue('cameron_example.com')
    expect(screen.queryByRole('button', { name: 'Delete profile…' })).toBeNull()
  })

  it('renames the personal profile from its row while on another profile', async () => {
    at('/work')
    const profiles = [...all]
    const api = stubApi(page, {
      'GET /api/me': meFor(profiles).handler,
      'PUT /api/profiles/2': () => {
        const renamed = { ...personal, name: 'cameron', slug: 'cameron' }
        profiles.splice(profiles.indexOf(personal), 1, renamed)
        return renamed
      },
    })
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Profile: work' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Rename cameron_example.com' }))
    await userEvent.clear(screen.getByLabelText('Name'))
    await userEvent.type(screen.getByLabelText('Name'), 'cameron')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.called('PUT /api/profiles/2')).toHaveLength(1))
    expect(api.called('PUT /api/profiles/2')[0].body).toEqual({ name: 'cameron' })
    // It wasn't the page's profile, so the page stays put.
    expect(window.location.pathname).toBe('/work')
  })

  it('shows no picker with profiles off', async () => {
    stubApi(page)
    renderApp()

    await screen.findByText('Wiki')
    expect(screen.queryByRole('button', { name: /^Profile:/ })).toBeNull()
    await waitFor(() => expect(FakeEventSource.latest.url).toBe('/api/events'))
  })
})

describe('bookmarkletHref', () => {
  it('carries the profile it was dragged from', () => {
    expect(bookmarkletHref('https://foyer.lan', 7, 'vendor')).toContain(
      '"https://foyer.lan/add?profile=vendor&category=7&"',
    )
  })
})
