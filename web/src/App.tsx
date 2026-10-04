import { Alert, AppShell, Button, Center, Loader } from '@mantine/core'
import { useMemo, useState } from 'react'

import { useDashboard } from './api/queries.ts'
import classes from './App.module.css'
import { Board } from './features/board/Board.tsx'
import { EmptyState } from './features/empty-state/EmptyState.tsx'
import { searchBookmarks } from './features/search/search.ts'
import { SearchResults } from './features/search/SearchResults.tsx'
import { TopBar } from './features/top-bar/TopBar.tsx'
import { useLiveUpdates } from './hooks/useLiveUpdates.ts'
import { navigation } from './navigation.ts'

export default function App() {
  useLiveUpdates()
  const dashboard = useDashboard()
  const [query, setQuery] = useState('')

  const categories = useMemo(() => dashboard.data?.categories ?? [], [dashboard.data])
  const hits = useMemo(() => searchBookmarks(categories, query), [categories, query])
  const searching = query.trim() !== ''
  const empty = categories.every((c) => c.bookmarks.length === 0)

  return (
    <AppShell header={{ height: 80 }}>
      <AppShell.Header withBorder={false}>
        <TopBar
          query={query}
          onQueryChange={setQuery}
          onSubmit={() => hits[0] && navigation.open(hits[0].bookmark.url)}
        />
      </AppShell.Header>
      <AppShell.Main>
        <main className={classes.main}>
          {dashboard.isPending ? (
            <Center py="xl">
              <Loader aria-label="Loading bookmarks" />
            </Center>
          ) : dashboard.isError ? (
            <Alert color="red" title="Couldn't load bookmarks">
              {dashboard.error.message}
              <Button
                variant="light"
                color="red"
                size="xs"
                mt="sm"
                display="block"
                onClick={() => dashboard.refetch()}
              >
                Try again
              </Button>
            </Alert>
          ) : searching ? (
            <SearchResults query={query} hits={hits} />
          ) : empty ? (
            <EmptyState />
          ) : (
            <Board categories={categories} />
          )}
        </main>
      </AppShell.Main>
    </AppShell>
  )
}
