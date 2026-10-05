import { Alert, Anchor, Button, Center, Loader, Stack, Text } from '@mantine/core'
import { useEffect, useState } from 'react'

import { useDashboard } from '../../api/queries.ts'
import { useTitle } from '../../hooks/useTitle.ts'
import { useCurrentProfile } from '../profiles/profileContext.ts'
import { BookmarkForm } from '../bookmark-form/BookmarkForm.tsx'
import { readQuickAdd } from './bookmarklet.ts'
import classes from './QuickAddPage.module.css'

/**
 * The bookmarklet's popup: the Add form, filled in from the page it was clicked on, for the
 * profile the bookmarklet came from. Saving or cancelling closes the popup; opened some other
 * way, it says what happened instead. A profile the caller can't change says so, offering their
 * personal profile instead when they have one.
 */
export function QuickAddPage() {
  const dashboard = useDashboard()
  const title = useTitle()
  const current = useCurrentProfile()
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
            You can close this window, or{' '}
            <Anchor href={current?.slug ? `/${current.slug}` : '/'}>open {title || 'Foyer'}</Anchor>
            .
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
      ) : current && !current.me.current.canEdit ? (
        <ReadOnlyProfile />
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

function ReadOnlyProfile() {
  const me = useCurrentProfile()!.me
  const personal = me.profiles.find((p) => p.kind === 'personal')
  const instead = new URL(window.location.href)
  if (personal) {
    instead.searchParams.set('profile', personal.slug)
  }

  return (
    <Stack gap="sm">
      <Alert color="yellow" variant="light" title={`You can’t add to ${me.current.name}`}>
        Only its editors can add bookmarks there.
      </Alert>
      {me.user && personal && (
        <Button component="a" href={instead.toString()}>
          Add to {personal.name} instead
        </Button>
      )}
    </Stack>
  )
}
