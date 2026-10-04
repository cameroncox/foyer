import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { Dashboard } from '../../api/client.ts'
import { problem, stubApi } from '../../test/fakeApi.ts'
import { bookmark, category, dockerBookmark } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'

function page(): Dashboard {
  return {
    categories: [
      category('Media', [
        dockerBookmark('Sonarr', 'running', { tags: ['arr'], icon: 'sonarr.svg' }),
      ]),
      category(
        'Uncategorized',
        [bookmark('Router', { tags: ['network'], url: 'https://router.lan' })],
        true,
      ),
    ],
  }
}

const dialog = () => screen.getByRole('dialog', { name: /bookmark/i })

/** The tag input itself; its suggestion list is labelled "Tags" too. */
const tagsInput = (form: ReturnType<typeof within>) =>
  form.getAllByLabelText('Tags').find((el: HTMLElement) => el.tagName === 'INPUT')!

async function openAdd() {
  await screen.findByText('Router')
  await userEvent.click(screen.getByRole('button', { name: 'Add bookmark' }))
  return within(dialog())
}

describe('Add bookmark', () => {
  it('adds a bookmark in a new category from the floating +', async () => {
    const api = stubApi(page)
    renderApp()
    const form = await openAdd()

    await userEvent.type(form.getByLabelText('Name'), 'Home Assistant')
    await userEvent.click(form.getByRole('combobox', { name: 'Category' }))
    await userEvent.click(await form.findByRole('option', { name: 'New category…' }))
    await userEvent.type(form.getByLabelText('New category name'), 'Home Automation')
    await userEvent.type(form.getByLabelText('URL'), 'https://ha.lan')
    await userEvent.type(tagsInput(form), 'automation,')
    await userEvent.click(form.getByRole('button', { name: 'Add bookmark' }))

    await waitFor(() => expect(api.called('POST /api/bookmarks')).toHaveLength(1))
    expect(api.called('POST /api/bookmarks')[0].body).toEqual({
      name: 'Home Assistant',
      url: 'https://ha.lan',
      icon: null,
      tags: ['automation'],
      categoryId: null,
      newCategoryName: 'Home Automation',
    })
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Add bookmark' })).toBeNull())
  })

  it('defaults to Uncategorized and checks the URL before sending', async () => {
    const api = stubApi(page)
    renderApp()
    const form = await openAdd()

    expect(form.getByRole('combobox', { name: 'Category' })).toHaveValue('Uncategorized')
    await userEvent.type(form.getByLabelText('Name'), 'Thing')
    await userEvent.type(form.getByLabelText('URL'), 'thing.lan')
    await userEvent.click(form.getByRole('button', { name: 'Add bookmark' }))

    expect(form.getByText('Enter a full http:// or https:// address')).toBeInTheDocument()
    expect(api.called('POST /api/bookmarks')).toHaveLength(0)
  })

  it('filters categories by search and clears back to Uncategorized', async () => {
    stubApi(page)
    renderApp()
    const form = await openAdd()
    const select = form.getByRole('combobox', { name: 'Category' })
    expect(form.queryByLabelText('Clear category')).toBeNull()

    await userEvent.clear(select)
    await userEvent.type(select, 'med')
    expect(await form.findByRole('option', { name: 'Media' })).toBeInTheDocument()
    expect(form.queryByRole('option', { name: 'Uncategorized' })).toBeNull()
    await userEvent.click(form.getByRole('option', { name: 'Media' }))
    expect(select).toHaveValue('Media')

    await userEvent.click(form.getByLabelText('Clear category'))
    expect(select).toHaveValue('Uncategorized')
    expect(form.queryByLabelText('Clear category')).toBeNull()
  })

  it('shows what the server refused', async () => {
    stubApi(page, {
      'POST /api/bookmarks': () => problem(409, "A category named 'Media' already exists."),
    })
    renderApp()
    const form = await openAdd()

    await userEvent.type(form.getByLabelText('Name'), 'X')
    await userEvent.type(form.getByLabelText('URL'), 'https://x.lan')
    await userEvent.click(form.getByRole('button', { name: 'Add bookmark' }))

    expect(await form.findByText("A category named 'Media' already exists.")).toBeInTheDocument()
  })

  it('is offered with the search text when nothing matches', async () => {
    stubApi(page)
    renderApp()
    await screen.findByText('Router')

    await userEvent.click(screen.getByRole('button', { name: 'Search bookmarks' }))
    await userEvent.type(
      await screen.findByRole('textbox', { name: 'Jump to a bookmark' }),
      'grafana',
    )
    await userEvent.click(screen.getByRole('button', { name: /Add “grafana” as a bookmark/ }))

    expect(within(dialog()).getByLabelText('Name')).toHaveValue('grafana')
  })

  it('the floating + turns into a close button while open', async () => {
    stubApi(page)
    renderApp()
    await openAdd()

    await userEvent.click(screen.getByRole('button', { name: 'Close' }))

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Add bookmark' })).toBeNull())
  })
})

describe('Edit bookmark', () => {
  async function editMode() {
    await screen.findByText('Router')
    await userEvent.click(screen.getByRole('button', { name: 'Edit page' }))
  }

  it('edits every field of a manual bookmark', async () => {
    const api = stubApi(page)
    renderApp()
    await editMode()

    await userEvent.click(screen.getByRole('button', { name: 'Edit Router' }))
    const form = within(dialog())
    expect(form.getByLabelText('URL')).toHaveValue('https://router.lan')
    await userEvent.clear(form.getByLabelText('Name'))
    await userEvent.type(form.getByLabelText('Name'), 'OPNsense')
    await userEvent.click(form.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.calls.some((c) => c.method === 'PUT')).toBe(true))
    const put = api.calls.find((c) => c.method === 'PUT')!
    expect(put.path).toMatch(/^\/api\/bookmarks\/\d+$/)
    expect(put.body).toMatchObject({
      name: 'OPNsense',
      url: 'https://router.lan',
      tags: ['network'],
    })
  })

  it('deletes a manual bookmark after confirming', async () => {
    const api = stubApi(page)
    renderApp()
    await editMode()

    await userEvent.click(screen.getByRole('button', { name: 'Edit Router' }))
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Delete' }))
    expect(within(dialog()).getByText('Delete “Router”?')).toBeInTheDocument()
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(api.calls.some((c) => c.method === 'DELETE')).toBe(true))
  })

  it('locks a Docker bookmark to its labels, editing only category and tags', async () => {
    const api = stubApi(page)
    renderApp()
    await editMode()

    await userEvent.click(screen.getByRole('button', { name: 'Edit category and tags of Sonarr' }))
    const form = within(dialog())
    expect(form.getByText('Set by container labels')).toBeInTheDocument()
    expect(form.getByText('sonarr.svg')).toBeInTheDocument()
    expect(form.queryByLabelText('Name')).toBeNull()
    expect(form.queryByRole('button', { name: 'Delete' })).toBeNull()

    await userEvent.type(tagsInput(form), 'tv,')
    await userEvent.click(form.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(api.calls.find((c) => c.method === 'PUT')!.body).toMatchObject({
      tags: ['arr', 'tv'],
      name: null,
      url: null,
      icon: null,
    })
  })

  it('opens a Docker bookmark on its heading, leaving the category list shut', async () => {
    stubApi(page)
    renderApp()
    await editMode()

    await userEvent.click(screen.getByRole('button', { name: 'Edit category and tags of Sonarr' }))
    const form = within(dialog())

    await waitFor(() => expect(form.getByRole('heading', { name: 'Edit bookmark' })).toHaveFocus())
    expect(form.getByRole('combobox', { name: 'Category' })).not.toHaveFocus()
    expect(screen.queryByRole('listbox')).toBeNull()
  })

  it('resets an overridden Docker bookmark to its labels', async () => {
    const overridden = page()
    overridden.categories[0].bookmarks[0].docker!.tagsOverridden = true
    const api = stubApi(() => overridden)
    renderApp()
    await editMode()

    await userEvent.click(screen.getByRole('button', { name: 'Edit category and tags of Sonarr' }))
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Reset to labels' }))

    await waitFor(() => expect(api.calls.some((c) => c.path.endsWith('/reset'))).toBe(true))
  })
})
