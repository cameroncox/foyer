import { Alert, AppShell, Button, Center, Loader } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { useMemo, useState } from 'react'

import type { Bookmark } from './api/client.ts'
import { useReorderBookmarks, useReorderCategories } from './api/mutations.ts'
import { useDashboard } from './api/queries.ts'
import classes from './App.module.css'
import { Board } from './features/board/Board.tsx'
import { FormPanel, type Panel } from './features/bookmark-form/FormPanel.tsx'
import { CategoryDrawer } from './features/edit-mode/CategoryDrawer.tsx'
import { EditBanner } from './features/edit-mode/EditBanner.tsx'
import { EditBoard } from './features/edit-mode/EditBoard.tsx'
import { EmptyState } from './features/empty-state/EmptyState.tsx'
import { ImportModal } from './features/import/ImportModal.tsx'
import { searchBookmarks } from './features/search/search.ts'
import { SearchResults } from './features/search/SearchResults.tsx'
import { TopBar } from './features/top-bar/TopBar.tsx'
import { useLiveUpdates } from './hooks/useLiveUpdates.ts'
import { navigation } from './navigation.ts'

const DRAWER_WIDTH = 360

export default function App() {
  useLiveUpdates()
  const dashboard = useDashboard()
  const reorderBookmarks = useReorderBookmarks()
  const reorderCategories = useReorderCategories()
  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState(false)
  const [panel, setPanel] = useState<Panel>(null)
  const [importing, setImporting] = useState(false)

  const categories = useMemo(() => dashboard.data?.categories ?? [], [dashboard.data])
  const hits = useMemo(() => searchBookmarks(categories, query), [categories, query])
  const searching = !editing && query.trim() !== ''
  const empty = categories.every((c) => c.bookmarks.length === 0)

  // Edit forms follow live data: a bookmark that disappears closes its form.
  const editingBookmark = useMemo(() => {
    if (panel?.kind !== 'edit') {
      return undefined
    }

    return categories.flatMap((c) => c.bookmarks).find((b) => b.id === panel.bookmark.id)
  }, [categories, panel])
  const livePanel: Panel =
    panel?.kind === 'edit'
      ? editingBookmark
        ? { kind: 'edit', bookmark: editingBookmark }
        : null
      : panel

  const failed = (title: string) => (error: Error) =>
    notifications.show({ color: 'red', title, message: error.message })

  const setEditMode = (on: boolean) => {
    setEditing(on)
    setPanel(null)
    if (on) {
      setQuery('')
    }
  }

  return (
    <AppShell
      header={{ height: 80 }}
      aside={{
        width: DRAWER_WIDTH,
        breakpoint: 'sm',
        collapsed: { desktop: !editing, mobile: !editing },
      }}
    >
      <AppShell.Header withBorder={false}>
        <TopBar
          query={query}
          onQueryChange={setQuery}
          onSubmit={() => hits[0] && navigation.open(hits[0].bookmark.url)}
          editing={editing}
          onEditingChange={setEditMode}
        />
      </AppShell.Header>

      {editing && dashboard.data && (
        <AppShell.Aside withBorder>
          <CategoryDrawer
            dashboard={dashboard.data}
            onImport={() => setImporting(true)}
            onReorder={({ dashboard: optimistic, categoryIds }) =>
              reorderCategories.mutate(
                { categoryIds, optimistic },
                { onError: failed('Couldn’t reorder categories') },
              )
            }
          />
        </AppShell.Aside>
      )}

      <AppShell.Main>
        {editing && <EditBanner />}
        <div className={classes.main}>
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
          ) : editing ? (
            <EditBoard
              dashboard={dashboard.data}
              editingId={editingBookmark?.id}
              onEdit={(bookmark: Bookmark) => setPanel({ kind: 'edit', bookmark })}
              onMove={({ dashboard: optimistic, request }) =>
                reorderBookmarks.mutate(
                  { body: request, optimistic },
                  { onError: failed('Couldn’t move the bookmark') },
                )
              }
            />
          ) : searching ? (
            <SearchResults
              query={query}
              hits={hits}
              onAdd={(name) => setPanel({ kind: 'add', name })}
            />
          ) : empty ? (
            <EmptyState onAdd={() => setPanel({ kind: 'add' })} />
          ) : (
            <Board categories={categories} />
          )}
        </div>
      </AppShell.Main>

      <FormPanel panel={livePanel} onPanelChange={setPanel} categories={categories} />
      <ImportModal opened={importing} onClose={() => setImporting(false)} />
    </AppShell>
  )
}
