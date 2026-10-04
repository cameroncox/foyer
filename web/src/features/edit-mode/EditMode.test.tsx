import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { Dashboard } from '../../api/client.ts'
import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category, dockerBookmark } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'

const media = category('Media', [dockerBookmark('Sonarr', 'running'), bookmark('Plex')])
const empty = category('Downloads', [])
const page = (): Dashboard => ({
  categories: [media, empty, category('Uncategorized', [bookmark('Router')], true)],
})

async function enterEditMode() {
  await screen.findByText('Router')
  await userEvent.click(screen.getByRole('button', { name: 'Edit page' }))
}

describe('edit mode', () => {
  it('swaps search for the editing hint, and shows handles, per-card actions and Done', async () => {
    stubApi(page)
    renderApp()
    await enterEditMode()

    expect(screen.getAllByRole('status').some((s) => s.textContent?.startsWith('Editing'))).toBe(
      true,
    )
    expect(screen.getByRole('button', { name: 'Done' })).toBeInTheDocument()
    expect(screen.queryByRole('searchbox')).toBeNull()
    expect(screen.getByRole('button', { name: 'Drag Plex' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete Plex' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Drag Sonarr' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete Sonarr' })).toBeNull()
    expect(screen.getByText('Drop bookmarks here')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Done' }))
    expect(screen.queryByRole('button', { name: 'Drag Plex' })).toBeNull()
  })

  it('deletes a manual card from the board after confirming', async () => {
    const api = stubApi(page)
    renderApp()
    await enterEditMode()

    await userEvent.click(screen.getByRole('button', { name: 'Delete Plex' }))
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(api.calls.some((c) => c.method === 'DELETE')).toBe(true))
    expect(api.calls.find((c) => c.method === 'DELETE')!.path).toBe(
      `/api/bookmarks/${media.bookmarks[1].id}`,
    )
  })
})

describe('category drawer', () => {
  const drawer = () => within(screen.getByRole('list', { name: 'Categories' }))

  it('lists categories with Uncategorized pinned last', async () => {
    stubApi(page)
    renderApp()
    await enterEditMode()

    expect(drawer().getByRole('textbox', { name: 'Rename Media' })).toBeInTheDocument()
    expect(drawer().getByText('always last')).toBeInTheDocument()
    expect(drawer().queryByRole('textbox', { name: 'Rename Uncategorized' })).toBeNull()
  })

  it('renames when the field loses focus', async () => {
    const api = stubApi(page)
    renderApp()
    await enterEditMode()

    const field = drawer().getByRole('textbox', { name: 'Rename Media' })
    await userEvent.clear(field)
    await userEvent.type(field, 'Streaming{Enter}')

    await waitFor(() => expect(api.called(`PUT /api/categories/${media.id}`)).toHaveLength(1))
    expect(api.called(`PUT /api/categories/${media.id}`)[0].body).toEqual({ name: 'Streaming' })
  })

  it('confirms a delete inline, saying where the bookmarks go', async () => {
    const api = stubApi(page)
    renderApp()
    await enterEditMode()

    await userEvent.click(drawer().getByRole('button', { name: 'Delete Media' }))
    expect(drawer().getByText('Its 2 bookmarks move to Uncategorized.')).toBeInTheDocument()
    await userEvent.click(drawer().getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(api.called(`DELETE /api/categories/${media.id}`)).toHaveLength(1))
  })

  it('adds a category', async () => {
    const api = stubApi(page)
    renderApp()
    await enterEditMode()

    await userEvent.type(screen.getByRole('textbox', { name: 'New category' }), 'Tools')
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    await waitFor(() => expect(api.called('POST /api/categories')).toHaveLength(1))
    expect(api.called('POST /api/categories')[0].body).toEqual({ name: 'Tools' })
  })
})
