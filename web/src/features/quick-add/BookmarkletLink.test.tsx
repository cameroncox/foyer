import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import type { Dashboard } from '../../api/client.ts'
import { PHONE_QUERY } from '../../hooks/usePhone.ts'
import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'

const dashboard: Dashboard = {
  categories: [category('Web Comics', [bookmark('XKCD')]), category('Uncategorized', [], true)],
}

describe('BookmarkletLink', () => {
  it('builds the picked category into the bookmarklet', async () => {
    stubApi(() => dashboard)
    renderApp()
    await userEvent.click(await screen.findByRole('button', { name: 'Edit page' }))

    const link = await screen.findByRole('link', { name: 'Add to Foyer' })
    expect(link.getAttribute('href')).toMatch(/^javascript:/)
    expect(link.getAttribute('href')).not.toContain('category=')

    await userEvent.click(screen.getByRole('combobox', { name: 'Bookmarklet category' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Web Comics' }))

    const picked = screen.getByRole('link', { name: 'Add to Foyer: Web Comics' })
    expect(picked.getAttribute('href')).toContain(`category=${dashboard.categories[0].id}&`)
  })

  it('is hidden on a phone, where there is no bookmarks bar to drag to', async () => {
    const desktop = window.matchMedia
    vi.stubGlobal('matchMedia', (query: string) => ({
      ...desktop(query),
      matches: query === PHONE_QUERY,
    }))
    stubApi(() => dashboard)
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Open menu' }))
    await userEvent.click(await screen.findByText('Edit page'))
    await userEvent.click(await screen.findByRole('button', { name: 'Categories' }))

    expect(await screen.findByRole('list', { name: 'Categories' })).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Bookmarklet category' })).toBeNull()
    expect(screen.queryByRole('link', { name: /^Add to Foyer/ })).toBeNull()
  })
})
