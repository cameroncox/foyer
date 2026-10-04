import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it } from 'vitest'

import type { Dashboard, ImportPreview } from '../../api/client.ts'
import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'

const page = (): Dashboard => ({
  categories: [category('Uncategorized', [bookmark('Router')], true)],
})

const preview: ImportPreview = {
  duplicateCount: 3,
  sections: [
    {
      name: 'Bookmarks bar',
      loose: null,
      folders: [
        {
          id: 'f2',
          name: 'Homelab',
          bookmarkCount: 2,
          duplicateCount: 0,
          targetCategory: 'Homelab',
          isNewCategory: true,
          children: [],
        },
        {
          id: 'f3',
          name: 'media',
          bookmarkCount: 4,
          duplicateCount: 3,
          targetCategory: 'Media',
          isNewCategory: false,
          children: [],
        },
      ],
    },
  ],
}

it('previews a file, imports the picked folders, and reports the result', async () => {
  const api = stubApi(page, {
    'POST /api/import/preview': () => preview,
    'POST /api/import': () => ({ added: 2, skipped: 0 }),
  })
  renderApp()
  await screen.findByText('Router')
  await userEvent.click(screen.getByRole('button', { name: 'Edit' }))
  await userEvent.click(screen.getByRole('button', { name: 'Import browser bookmarks' }))

  const modal = within(screen.getByRole('dialog', { name: 'Import bookmarks' }))
  const file = new File(['<DL><p></DL>'], 'bookmarks.html', { type: 'text/html' })
  await userEvent.upload(document.querySelector('input[type=file]') as HTMLInputElement, file)

  expect(await modal.findByRole('checkbox', { name: 'Homelab' })).toBeChecked()
  expect(api.called('POST /api/import/preview')[0].body).toEqual({ html: '<DL><p></DL>' })
  expect(modal.getByText('new')).toBeInTheDocument()
  expect(modal.getByText('3 bookmarks already in Foyer will be skipped.')).toBeInTheDocument()
  expect(modal.getByRole('button', { name: 'Import 6 bookmarks' })).toBeInTheDocument()

  await userEvent.click(modal.getByRole('checkbox', { name: 'media' }))
  await userEvent.click(modal.getByRole('button', { name: 'Import 2 bookmarks' }))

  await waitFor(() => expect(api.called('POST /api/import')).toHaveLength(1))
  expect(api.called('POST /api/import')[0].body).toEqual({
    html: '<DL><p></DL>',
    folderIds: ['f2'],
  })
  expect(await screen.findByText('Imported 2 bookmarks')).toBeInTheDocument()
})
