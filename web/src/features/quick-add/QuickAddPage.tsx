import { Alert, Anchor, Center, Loader, Stack, Text } from '@mantine/core'
import { useEffect, useState } from 'react'

import { useDashboard } from '../../api/queries.ts'
import { useTitle } from '../../hooks/useTitle.ts'
import { BookmarkForm } from '../bookmark-form/BookmarkForm.tsx'
import { readQuickAdd } from './bookmarklet.ts'
import classes from './QuickAddPage.module.css'

/**
 * The bookmarklet's popup: the Add form, filled in from the page it was clicked on. Saving or
 * cancelling closes the popup; opened some other way, it says what happened instead.
 */
export function QuickAddPage() {
  const dashboard = useDashboard()
  const title = useTitle()
  const [page] = useState(() => readQuickAdd(window.location.search))
  const [outcome, setOutcome] = useState<'saved' | 'cancelled' | null>(null)

  useEffect(() => {
    if (title) {
      document.title = `Add to ${title}`
    }
  }, [title])

  const finish = (result: 'saved' | 'cancelled') => {
    setOutcome(result)
    window.close()
  }

  const categories = dashboard.data?.categories ?? []
  const existing = page.url
    ? categories.flatMap((c) =>
        c.bookmarks.filter((b) => b.url === page.url).map((b) => ({ bookmark: b, category: c })),
      )[0]
    : undefined

  return (
    <main className={classes.page}>
      {outcome ? (
        <Stack gap="xs">
          <Text fw={500}>{outcome === 'saved' ? 'Bookmark added.' : 'Nothing was added.'}</Text>
          <Text size="sm" c="dimmed">
            You can close this window, or <Anchor href="/">open {title || 'Foyer'}</Anchor>.
          </Text>
        </Stack>
      ) : dashboard.isPending ? (
        <Center py="xl">
          <Loader aria-label="Loading categories" />
        </Center>
      ) : dashboard.isError ? (
        <Alert color="red" title="Couldn't load categories">
          {dashboard.error.message}
        </Alert>
      ) : (
        <Stack gap="md">
          {existing && (
            <Alert variant="light" p="xs">
              Already saved as “{existing.bookmark.name}” in {existing.category.name}.
            </Alert>
          )}
          <BookmarkForm
            categories={categories}
            initialName={page.name}
            initialUrl={page.url}
            initialCategoryId={page.categoryId}
            onDone={() => finish('saved')}
            onCancel={() => finish('cancelled')}
          />
        </Stack>
      )}
    </main>
  )
}
