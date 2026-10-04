import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { stubApi } from '../test/fakeApi.ts'
import { category } from '../test/fakes.ts'
import { renderApp } from '../test/render.tsx'

const page = () => ({ categories: [category('Uncategorized', [], true)] })

describe('title', () => {
  it('shows FOYER_TITLE in the top bar and the browser tab', async () => {
    stubApi(page, { 'GET /api/settings': () => ({ title: 'Casa de Cox' }) })
    renderApp()

    expect(await screen.findByText('Casa de Cox')).toBeInTheDocument()
    await waitFor(() => expect(document.title).toBe('Casa de Cox'))
  })

  it('falls back to Foyer when the settings can’t be read', async () => {
    stubApi(page, { 'GET /api/settings': () => new Response(null, { status: 500 }) })
    renderApp()

    expect(await screen.findByText('Foyer')).toBeInTheDocument()
  })
})
