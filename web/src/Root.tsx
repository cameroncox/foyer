import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode, useMemo, useState } from 'react'

import App from './App.tsx'
import { AccentContext } from './theme/AccentContext.ts'
import { loadAccent, saveAccent } from './theme/accentStore.ts'
import { createFoyerTheme } from './theme/theme.ts'

const defaultQueryClient = new QueryClient({
  defaultOptions: {
    // Live updates refetch on change, so window focus doesn't need to.
    queries: { refetchOnWindowFocus: false, retry: 1 },
  },
})

/** Providers around the app. The saved accent is read before the first render, so it never flashes. */
export function Root({
  queryClient = defaultQueryClient,
  mantineEnv,
}: {
  queryClient?: QueryClient
  /** 'test' turns off Mantine's transitions and portals, for jsdom. */
  mantineEnv?: 'default' | 'test'
}) {
  const [accent, setAccentState] = useState(loadAccent)
  const theme = useMemo(() => createFoyerTheme(accent), [accent])
  const accentState = useMemo(
    () => ({
      accent,
      setAccent: (key: string) => {
        saveAccent(key)
        setAccentState(key)
      },
    }),
    [accent],
  )

  return (
    <StrictMode>
      <AccentContext value={accentState}>
        <MantineProvider theme={theme} defaultColorScheme="auto" env={mantineEnv}>
          <Notifications position="top-right" />
          <QueryClientProvider client={queryClient}>
            <App />
          </QueryClientProvider>
        </MantineProvider>
      </AccentContext>
    </StrictMode>
  )
}
