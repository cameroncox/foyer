import { MantineProvider } from '@mantine/core'
import { QueryClient } from '@tanstack/react-query'
import { render as rtlRender } from '@testing-library/react'
import type { ReactElement } from 'react'

import { Root } from '../Root.tsx'
import { createFoyerTheme } from '../theme/theme.ts'

/** Renders a component inside Mantine, for component tests. */
export function render(ui: ReactElement) {
  return rtlRender(
    <MantineProvider theme={createFoyerTheme('deepBlue')} env="test">
      {ui}
    </MantineProvider>,
  )
}

/** Renders the whole app with a fresh query cache and no retries. */
export function renderApp() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return rtlRender(<Root queryClient={queryClient} mantineEnv="test" />)
}
