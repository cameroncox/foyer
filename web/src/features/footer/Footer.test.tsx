import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { stubApi } from '../../test/fakeApi.ts'
import { bookmark, category } from '../../test/fakes.ts'
import { renderApp } from '../../test/render.tsx'
import { REPOSITORY_URL } from './Footer.tsx'

const page = () => ({ categories: [category('Uncategorized', [bookmark('Gitea')], true)] })

describe('Footer', () => {
  it('shows the running version and links to the repository', async () => {
    stubApi(page)
    renderApp()

    expect(await screen.findByText('Foyer v1.0.3')).toBeInTheDocument()
    const link = screen.getByRole('link', { name: 'GitHub' })
    expect(link).toHaveAttribute('href', REPOSITORY_URL)
    expect(link).toHaveAttribute('target', '_blank')
  })

  it('is left out in edit mode', async () => {
    stubApi(page)
    renderApp()
    await screen.findByText('Foyer v1.0.3')

    await userEvent.click(screen.getByRole('button', { name: 'Edit page' }))

    expect(screen.queryByRole('link', { name: 'GitHub' })).toBeNull()
  })
})
