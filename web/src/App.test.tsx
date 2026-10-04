import { screen } from '@testing-library/react'
import { expect, it } from 'vitest'

import App from './App.tsx'
import { render } from './test/render.tsx'

it('renders the Foyer title', () => {
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Foyer' })).toBeInTheDocument()
})
