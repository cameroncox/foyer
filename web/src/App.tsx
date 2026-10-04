import { Alert, AppShell, Button, Center, Drawer, Loader } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { type ComponentProps, useMemo, useState } from 'react'

import type { Bookmark } from './api/client.ts'
import { useReorderBookmarks, useReorderCategories } from './api/mutations.ts'
import { useDashboard } from './api/queries.ts'
import classes from './App.module.css'
import { Board } from './features/board/Board.tsx'
import { FormPanel, type Panel } from './features/bookmark-form/FormPanel.tsx'
import { CategoryDrawer } from './features/edit-mode/CategoryDrawer.tsx'
import { EditBoard } from './features/edit-mode/EditBoard.tsx'
import { PhoneEditHint } from './features/edit-mode/EditHint.tsx'
import { EmptyState } from './features/empty-state/EmptyState.tsx'
import { ImportModal } from './features/import/ImportModal.tsx'
import { searchBookmarks } from './features/search/search.ts'
import { SearchResults } from './features/search/SearchResults.tsx'
import { TopBar } from './features/top-bar/TopBar.tsx'
import { useLiveUpdates } from './hooks/useLiveUpdates.ts'
import { usePhone } from './hooks/usePhone.ts'
import { navigation } from './navigation.ts'

const DRAWER_WIDTH = 300

export default function App() {
  useLiveUpdates()
  const dashboard = useDashboard()
  const reorderBookmarks = useReorderBookmarks()
  const reorderCategories = useReorderCategories()
  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState(false)
  const [panel, setPanel] = useState<Panel>(null)
  const [importing, setImporting] = useState(false)
  const [categoriesOpen, setCategoriesOpen] = useState(false)
  const phone = usePhone()

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

  const reorderCategoriesWith: ComponentProps<typeof CategoryDrawer>['onReorder'] = ({
    dashboard: optimistic,
    categoryIds,
  }) =>
    reorderCategories.mutate(
      { categoryIds, optimistic },
      { onError: failed('Couldn’t reorder categories') },
    )

  const setEditMode = (on: boolean) => {
    setEditing(on)
    setPanel(null)
    setCategoriesOpen(false)
    if (on) {
      setQuery('')
    }
  }

  return (
    <AppShell
      // No slide-in: the content's left edge is worked out from the drawer's width, and an
      // animated padding would make it wobble while the drawer opens.
      transitionDuration={0}
      header={{ height: { base: 60, sm: 80 } }}
      // On a phone the drawer would cover the cards, so it opens as its own screen instead.
      aside={{
        width: DRAWER_WIDTH,
        breakpoint: 'sm',
        collapsed: { desktop: !editing, mobile: true },
      }}
    >
      <AppShell.Header withBorder={false}>
        <TopBar
          query={query}
          onQueryChange={setQuery}
          onSubmit={() => hits[0] && navigation.open(hits[0].bookmark.url)}
          editing={editing}
          onEditingChange={setEditMode}
          onAdd={() => setPanel({ kind: 'add' })}
          onOpenCategories={() => setCategoriesOpen(true)}
        />
      </AppShell.Header>

      {editing && dashboard.data && !phone && (
        <AppShell.Aside withBorder>
          <CategoryDrawer
            dashboard={dashboard.data}
            onImport={() => setImporting(true)}
            onReorder={reorderCategoriesWith}
          />
        </AppShell.Aside>
      )}
      {phone && (
        <Drawer
          opened={editing && categoriesOpen && !!dashboard.data}
          onClose={() => setCategoriesOpen(false)}
          position="right"
          size="100%"
          padding={0}
          withCloseButton={false}
          aria-label="Categories"
          // Full height, so Import sits at the bottom as it does in the desktop drawer.
          styles={{ body: { height: '100%' } }}
        >
          {dashboard.data && (
            <CategoryDrawer
              dashboard={dashboard.data}
              onImport={() => setImporting(true)}
              onReorder={reorderCategoriesWith}
              onBack={() => setCategoriesOpen(false)}
            />
          )}
        </Drawer>
      )}

      <AppShell.Main>
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
            <>
              {phone && <PhoneEditHint />}
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
            </>
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
