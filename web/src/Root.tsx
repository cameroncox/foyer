import { MantineProvider } from '@mantine/core'
import { Notifications } from '@mantine/notifications'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode, useMemo, useState } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router'

import App from './App.tsx'
import { ProfileGate } from './features/profiles/ProfileGate.tsx'
import { QUICK_ADD_PATH, readQuickAdd } from './features/quick-add/bookmarklet.ts'
import { QuickAddPage } from './features/quick-add/QuickAddPage.tsx'
import { AccentContext } from './theme/AccentContext.ts'
import { loadAccent, saveAccent } from './theme/accentStore.ts'
import { createFoyerTheme } from './theme/theme.ts'

const defaultQueryClient = new QueryClient({
  defaultOptions: {
    // Live updates refetch on change, so window focus doesn't need to.
    queries: { refetchOnWindowFocus: false, retry: 1 },
  },
})

/**
 * Providers and routes: the quick-add popup at /add, and the board at /{profile}, or / for the
 * remembered or default profile. The saved accent is read before the first render, so it never
 * flashes.
 */
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
            <BrowserRouter>
              <Routes>
                <Route
                  path={QUICK_ADD_PATH}
                  element={
                    <ProfileGate profile={readQuickAdd(window.location.search).profile}>
                      <QuickAddPage />
                    </ProfileGate>
                  }
                />
                <Route path="/:profile?" element={<Board />} />
                <Route path="*" element={<Board />} />
              </Routes>
            </BrowserRouter>
          </QueryClientProvider>
        </MantineProvider>
      </AccentContext>
    </StrictMode>
  )
}

function Board() {
  return (
    <ProfileGate>
      <App />
    </ProfileGate>
  )
}
