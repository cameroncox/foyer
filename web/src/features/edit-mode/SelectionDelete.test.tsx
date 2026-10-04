import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { Dashboard } from '../../api/client.ts'
import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category, dockerBookmark } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'

function page(): Dashboard {
  return {
    categories: [
      category('Infrastructure', [
        bookmark('Router', { id: 101 }),
        bookmark('NAS', { id: 102 }),
        dockerBookmark('Sonarr', 'running', { id: 103 }),
      ]),
      category('Uncategorized', [bookmark('Printer', { id: 104 })], true),
    ],
  }
}

async function startSelecting() {
  await screen.findByText('Router')
  await userEvent.click(screen.getByRole('button', { name: 'Edit' }))
  await userEvent.click(screen.getByRole('button', { name: 'Select' }))
}

describe('Deleting several bookmarks', () => {
  it('deletes a category’s manual bookmarks plus one more, after confirming', async () => {
    const api = stubApi(page, { 'POST /api/bookmarks/delete': () => ({ deleted: 3 }) })
    renderApp()
    await startSelecting()

    expect(screen.getByRole('checkbox', { name: 'Select Sonarr' })).toBeDisabled()
    await userEvent.click(screen.getByRole('checkbox', { name: 'Select all in Infrastructure' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Select Printer' }))
    expect(screen.getByText('3 selected')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Delete 3' }))

    await waitFor(() => expect(api.called('POST /api/bookmarks/delete')).toHaveLength(1))
    expect(api.called('POST /api/bookmarks/delete')[0].body).toEqual({
      bookmarkIds: [101, 102, 104],
    })
    expect(await screen.findByText('Deleted 3 bookmarks')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Select' })).toBeInTheDocument()
  })

  it('cancels without deleting', async () => {
    const api = stubApi(page)
    renderApp()
    await startSelecting()

    await userEvent.click(screen.getByRole('checkbox', { name: 'Select Router' }))
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('checkbox', { name: 'Select Router' })).toBeNull()
    expect(api.called('POST /api/bookmarks/delete')).toHaveLength(0)
  })
})
