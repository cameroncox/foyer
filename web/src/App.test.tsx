import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import type { Dashboard } from './api/client.ts'
import { PHONE_QUERY } from './hooks/usePhone.ts'
import { navigation } from './navigation.ts'
import { stubApi } from './test/fakeApi.ts'
import { bookmark, category, dockerBookmark, FakeEventSource, stubDashboard } from './test/fakes.ts'
import { renderApp } from './test/render.tsx'

const uncategorized = (...bookmarks: ReturnType<typeof bookmark>[]) =>
  category('Uncategorized', bookmarks, true)

function page(): Dashboard {
  return {
    categories: [
      category('Media', [
        dockerBookmark('Jellyfin', 'running'),
        dockerBookmark('Sonarr', 'warning', { tags: ['arr'] }),
        dockerBookmark('Prowlarr', 'stopped'),
      ]),
      category('Empty', []),
      category('Infrastructure', [
        bookmark('OPNsense', { tags: ['router'], url: 'https://opnsense.lan' }),
      ]),
      uncategorized(bookmark('Radarr', { tags: ['arr'], url: 'https://radarr.lan' })),
    ],
  }
}

describe('App', () => {
  it('shows the empty state when there are no bookmarks', async () => {
    stubDashboard(() => ({ categories: [uncategorized()] }))
    renderApp()

    expect(await screen.findByRole('heading', { name: 'No bookmarks yet' })).toBeInTheDocument()
    expect(screen.getByText('coxdev.bookmark.enabled=true')).toBeInTheDocument()
  })

  it('shows categories in order, hiding empty ones', async () => {
    stubDashboard(page)
    renderApp()

    await screen.findByRole('heading', { name: /Media/ })
    const headings = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)
    expect(headings).toEqual(['Media3', 'Infrastructure1', 'Uncategorized1'])
  })

  it('marks Docker cards with a status dot and host tag, and dims stopped ones', async () => {
    stubDashboard(page)
    renderApp()

    const sonarr = (await screen.findByText('Sonarr')).closest('a')!
    expect(within(sonarr).getByRole('img', { name: 'Unhealthy' })).toBeInTheDocument()
    expect(within(sonarr).getByText('#docker-4')).toBeInTheDocument()
    expect(within(sonarr).getByText('#arr')).toBeInTheDocument()
    expect(screen.getByText('Prowlarr').closest('a')).toHaveAttribute('data-stopped')
    expect(screen.getByText('OPNsense').closest('a')).not.toHaveAttribute('data-stopped')
    expect(within(screen.getByText('OPNsense').closest('a')!).queryByRole('img')).toBeNull()
  })

  it('links each card to its URL, in this tab', async () => {
    stubDashboard(page)
    renderApp()

    const card = (await screen.findByText('OPNsense')).closest('a')
    expect(card).toHaveAttribute('href', 'https://opnsense.lan')
    expect(card).not.toHaveAttribute('target')
    expect(card).toHaveAttribute('rel', 'noreferrer')
  })

  it('shows an error with a retry when the dashboard fails', async () => {
    let fail = true
    stubDashboard(() =>
      fail ? new Response(JSON.stringify({ detail: 'boom' }), { status: 500 }) : page(),
    )
    renderApp()

    expect(await screen.findByText("Couldn't load bookmarks")).toBeInTheDocument()
    fail = false
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('OPNsense')).toBeInTheDocument()
  })
})

describe('search', () => {
  const spotlightBox = () => screen.findByRole('textbox', { name: 'Jump to a bookmark' })

  it('opens the spotlight from the search button in the top bar', async () => {
    stubDashboard(page)
    renderApp()
    await screen.findByText('OPNsense')

    await userEvent.click(screen.getByRole('button', { name: 'Search bookmarks' }))

    expect(await spotlightBox()).toHaveFocus()
  })

  it('opens the spotlight on / and ⌘K too', async () => {
    stubDashboard(page)
    renderApp()
    await screen.findByText('OPNsense')

    await userEvent.keyboard('/')
    expect(await spotlightBox()).toHaveValue('')
    await userEvent.keyboard('{Escape}')
    await waitFor(() =>
      expect(screen.queryByRole('textbox', { name: 'Jump to a bookmark' })).toBeNull(),
    )

    await userEvent.keyboard('{Meta>}k{/Meta}')
    expect(await spotlightBox()).toBeInTheDocument()
  })

  it('keeps the board in place, with no filtering box', async () => {
    stubDashboard(page)
    renderApp()
    await screen.findByText('OPNsense')

    expect(screen.queryByRole('searchbox')).toBeNull()
  })
})

describe('spotlight', () => {
  const openSpotlight = async () => {
    stubDashboard(page)
    renderApp()
    await screen.findByText('OPNsense')
    await userEvent.keyboard(' ')
    return screen.findByRole('textbox', { name: 'Jump to a bookmark' })
  }

  it('opens on Space with hosts, tags and categories to pick from', async () => {
    const box = await openSpotlight()

    expect(box).toHaveFocus()
    expect(box).toHaveValue('')
    expect(screen.getByRole('button', { name: /#docker-4/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /#router/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Infrastructure/ })).toBeInTheDocument()
  })

  it('narrows to a picked tag, then opens the highlighted bookmark on Enter', async () => {
    const open = vi.spyOn(navigation, 'open').mockImplementation(() => {})
    const box = await openSpotlight()

    await userEvent.click(screen.getByRole('button', { name: /#arr/ }))
    expect(screen.getByText('Showing')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Sonarr/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Jellyfin/ })).toBeNull()

    await userEvent.type(box, 'rad{Enter}')
    expect(open).toHaveBeenCalledWith('https://radarr.lan')
  })

  it('clears the filter on Backspace in an empty box', async () => {
    const box = await openSpotlight()

    await userEvent.click(screen.getByRole('button', { name: /#docker-4/ }))
    await userEvent.type(box, '{Backspace}')

    expect(screen.queryByText('Showing')).toBeNull()
    expect(screen.getByRole('button', { name: /#router/ })).toBeInTheDocument()
  })

  it('sends a bang straight to DuckDuckGo on Enter', async () => {
    const open = vi.spyOn(navigation, 'open').mockImplementation(() => {})
    const box = await openSpotlight()

    await userEvent.type(box, '!g current c# standard')
    expect(screen.queryByText('Bookmarks')).toBeNull()
    await userEvent.type(box, '{Enter}')

    expect(open).toHaveBeenCalledWith('https://duckduckgo.com/?q=!g%20current%20c%23%20standard')
  })

  it('offers a web search after the bookmarks, and alone when nothing matches', async () => {
    const open = vi.spyOn(navigation, 'open').mockImplementation(() => {})
    const box = await openSpotlight()

    await userEvent.type(box, 'rad')
    expect(screen.getByRole('button', { name: /Search DuckDuckGo for “rad”/ })).toBeInTheDocument()
    await userEvent.type(box, '{Enter}')
    expect(open).toHaveBeenLastCalledWith('https://radarr.lan')

    await userEvent.keyboard(' ')
    const again = await screen.findByRole('textbox', { name: 'Jump to a bookmark' })
    await userEvent.clear(again)
    await userEvent.type(again, 'zzz{Enter}')
    expect(open).toHaveBeenLastCalledWith('https://duckduckgo.com/?q=zzz')
  })

  it('uses FOYER_SEARCH_URL, where a bang is just text', async () => {
    const open = vi.spyOn(navigation, 'open').mockImplementation(() => {})
    stubApi(page, {
      'GET /api/settings': () => ({
        title: 'Foyer',
        searchUrl: 'https://www.google.com/search?q=%s',
      }),
    })
    renderApp()
    await screen.findByText('OPNsense')
    await userEvent.keyboard(' ')
    const box = await screen.findByRole('textbox', { name: 'Jump to a bookmark' })

    await userEvent.type(box, '!g zzz')
    await userEvent.click(screen.getByRole('button', { name: /Search Google for “!g zzz”/ }))

    expect(open).toHaveBeenCalledWith('https://www.google.com/search?q=!g%20zzz')
  })

  it('offers to go to an address, after the web search', async () => {
    const open = vi.spyOn(navigation, 'open').mockImplementation(() => {})
    const box = await openSpotlight()

    await userEvent.type(box, 'example.com')
    await userEvent.click(screen.getByRole('button', { name: /Go to example\.com/ }))
    expect(open).toHaveBeenCalledWith('https://example.com')

    await userEvent.keyboard(' ')
    const again = await screen.findByRole('textbox', { name: 'Jump to a bookmark' })
    await userEvent.clear(again)
    await userEvent.type(again, 'radarr')
    expect(screen.queryByRole('button', { name: /Go to/ })).toBeNull()
  })

  it('opens a row by its number after Tab, and types digits otherwise', async () => {
    const open = vi.spyOn(navigation, 'open').mockImplementation(() => {})
    const box = await openSpotlight()

    await userEvent.type(box, 'rad2')
    expect(box).toHaveValue('rad2')
    expect(open).not.toHaveBeenCalled()

    await userEvent.type(box, '{Backspace}{Tab}a1')
    expect(box).toHaveValue('rada1')
    expect(open).not.toHaveBeenCalled()

    await userEvent.type(box, '{Backspace}{Backspace}{Tab}2')
    expect(open).toHaveBeenCalledWith('https://duckduckgo.com/?q=rad')
  })

  it('numbers the first nine rows across groups', async () => {
    const box = await openSpotlight()

    await userEvent.type(box, 'rad')
    expect(
      within(screen.getByRole('button', { name: /Radarr/ })).getByText('1'),
    ).toBeInTheDocument()
    expect(
      within(screen.getByRole('button', { name: /Search DuckDuckGo/ })).getByText('2'),
    ).toBeInTheDocument()
  })

  it('stays shut while editing', async () => {
    stubDashboard(page)
    renderApp()
    await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))
    ;(document.activeElement as HTMLElement).blur()

    await userEvent.keyboard(' ')

    expect(screen.queryByRole('textbox', { name: 'Jump to a bookmark' })).toBeNull()
  })
})

describe('on a phone', () => {
  const asPhone = () => {
    const desktop = window.matchMedia
    vi.stubGlobal('matchMedia', (query: string) => ({
      ...desktop(query),
      matches: query === PHONE_QUERY,
    }))
  }

  it('searches through a full-screen spotlight from the top bar, closed by its X', async () => {
    asPhone()
    stubDashboard(page)
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Search bookmarks' }))
    const box = await screen.findByRole('textbox', { name: 'Jump to a bookmark' })
    await userEvent.type(box, 'opn')
    expect(await screen.findByRole('button', { name: /OPNsense/ })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Close search' }))
    await waitFor(() =>
      expect(screen.queryByRole('textbox', { name: 'Jump to a bookmark' })).toBeNull(),
    )
  })

  it('leaves search out of the menu', async () => {
    asPhone()
    stubDashboard(page)
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Open menu' }))
    const close = await screen.findByRole('button', { name: 'Close menu' })
    const menu = close.closest<HTMLElement>('[role="dialog"]')!

    expect(within(menu).getByRole('button', { name: 'Add bookmark' })).toBeInTheDocument()
    expect(within(menu).queryByRole('searchbox')).toBeNull()
  })
})

describe('live updates', () => {
  it('refetches when bookmarks change', async () => {
    let current = page()
    stubDashboard(() => current)
    renderApp()
    await screen.findByText('OPNsense')

    current = { categories: [uncategorized(bookmark('Brand new'))] }
    act(() => FakeEventSource.latest.emit('bookmarks-changed'))

    expect(await screen.findByText('Brand new')).toBeInTheDocument()
    expect(FakeEventSource.latest.url).toBe('/api/events')
  })

  it('refetches after a reconnect, but not on the first connect', async () => {
    const fetch = stubDashboard(page)
    renderApp()
    await screen.findByText('OPNsense')
    const before = fetch.mock.calls.length

    act(() => FakeEventSource.latest.emit('connected'))
    expect(fetch.mock.calls.length).toBe(before)

    act(() => FakeEventSource.latest.emit('connected'))
    await waitFor(() => expect(fetch.mock.calls.length).toBe(before + 1))
  })
})

describe('theme', () => {
  it('saves the chosen accent on this device', async () => {
    stubDashboard(page)
    renderApp()
    await screen.findByText('OPNsense')

    await userEvent.click(screen.getByRole('button', { name: 'Theme and accent color' }))
    await userEvent.click(await screen.findByRole('radio', { name: 'Violet' }))

    expect(localStorage.getItem('foyer-accent')).toBe('violet')
    expect(screen.getByRole('radio', { name: 'Violet' })).toHaveAttribute('aria-checked', 'true')
  })
})
