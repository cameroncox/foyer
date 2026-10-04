import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { Dashboard } from '../../api/client.ts'
import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'

const page = (): Dashboard => ({
  categories: [
    category('Web Comics', [bookmark('XKCD', { url: 'https://xkcd.com/' })]),
    category('Uncategorized', [], true),
  ],
})

function openAt(search: string) {
  window.history.pushState({}, '', `/add${search}`)
}

describe('Quick add', () => {
  beforeEach(() => {
    vi.spyOn(window, 'close').mockImplementation(() => {})
  })

  afterEach(() => {
    window.history.pushState({}, '', '/')
  })

  it('fills in the page and closes the popup once saved', async () => {
    const api = stubApi(page)
    openAt('?url=https%3A%2F%2Fqwantz.com%2F&name=Dinosaur+Comics')
    renderApp()

    const name = await screen.findByLabelText('Name')
    expect(name).toHaveValue('Dinosaur Comics')
    expect(screen.getByLabelText('URL')).toHaveValue('https://qwantz.com/')
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: 'Add bookmark' }))

    await waitFor(() => expect(api.called('POST /api/bookmarks')).toHaveLength(1))
    expect(api.called('POST /api/bookmarks')[0].body).toMatchObject({
      name: 'Dinosaur Comics',
      url: 'https://qwantz.com/',
    })
    await waitFor(() => expect(window.close).toHaveBeenCalled())
    expect(screen.getByText('Bookmark added.')).toBeInTheDocument()
  })

  it('says when the page is already saved', async () => {
    stubApi(page)
    openAt('?url=https%3A%2F%2Fxkcd.com%2F&name=xkcd')
    renderApp()

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText(/Already saved as “XKCD” in Web Comics/)).toBeInTheDocument()
  })

  it('closes without saving on Cancel', async () => {
    const api = stubApi(page)
    openAt('?url=https%3A%2F%2Fqwantz.com%2F&name=Dinosaur+Comics')
    renderApp()

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }))

    expect(window.close).toHaveBeenCalled()
    expect(api.called('POST /api/bookmarks')).toHaveLength(0)
    expect(screen.getByText('Nothing was added.')).toBeInTheDocument()
  })
})
