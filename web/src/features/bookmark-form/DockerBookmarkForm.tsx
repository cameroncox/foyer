import { Alert, Button, Group, Stack, Text, Title } from '@mantine/core'
import { IconBrandDocker, IconLock } from '@tabler/icons-react'
import { useState } from 'react'

import type { Bookmark, DashboardCategory } from '../../api/client.ts'
import { useResetBookmark, useUpdateBookmark } from '../../api/mutations.ts'
import { useCurrentProfile } from '../profiles/profileContext.ts'
import { SharedField } from '../sharing/SharedField.tsx'
import { initialShare, shareBody, shareError } from '../sharing/shareValue.ts'
import { categoryFields, categoryName, NEW_CATEGORY } from './categoryChoice.ts'
import { CategoryField } from './CategoryField.tsx'
import classes from './DockerBookmarkForm.module.css'
import { TagsField } from './TagsField.tsx'

interface Props {
  categories: readonly DashboardCategory[]
  bookmark: Bookmark
  onDone: () => void
}

/**
 * Edit a Docker bookmark: name, URL and icon are locked to its labels; only Category, Tags and
 * Shared change here. Reset to labels hands category and tags back to the labels. No Delete.
 */
export function DockerBookmarkForm({ categories, bookmark, onDone }: Props) {
  const docker = bookmark.docker!
  const update = useUpdateBookmark()
  const reset = useResetBookmark()
  const [category, setCategory] = useState(String(bookmark.categoryId))
  const [newCategory, setNewCategory] = useState('')
  const [newCategoryError, setNewCategoryError] = useState<string>()
  const [tags, setTags] = useState(bookmark.tags)
  const [share, setShare] = useState(() => initialShare(bookmark))
  const [shareProblem, setShareProblem] = useState<string | null>(null)
  const sharing = useCurrentProfile()?.me.profilesEnabled ?? false
  const error = update.error ?? reset.error

  const save = (event: React.FormEvent) => {
    event.preventDefault()
    if (category === NEW_CATEGORY && !newCategory.trim()) {
      setNewCategoryError('Name the new category')
      return
    }

    const problem = sharing ? shareError(share) : null
    setShareProblem(problem)
    if (problem) {
      return
    }

    // Labels own the name, URL and icon, so those stay null. Failures show above the fields.
    update.mutate(
      {
        id: bookmark.id,
        body: {
          tags,
          name: null,
          url: null,
          icon: null,
          ...categoryFields(category, newCategory),
          ...(sharing ? shareBody(share) : {}),
        },
      },
      { onSuccess: onDone },
    )
  }

  return (
    <form onSubmit={save} noValidate>
      <Stack gap="md">
        <Stack gap={4}>
          {/* Opening focuses the heading; landing on Category would pop its list open. */}
          <Title order={2} size="h4" id="bookmark-form-title" tabIndex={-1} data-autofocus>
            Edit bookmark
          </Title>
          <Text size="sm" c="dimmed" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <IconBrandDocker size={14} aria-hidden="true" />
            Container <span className={classes.mono}>{docker.containerName}</span> on {docker.host}
          </Text>
        </Stack>
        {error && (
          <Alert color="red" variant="light" p="xs">
            {error.message}
          </Alert>
        )}

        <div className={classes.locked}>
          <div className={classes.lockedTitle}>
            <IconLock size={12} aria-hidden="true" />
            Set by container labels
          </div>
          <dl className={classes.fields}>
            <dt>Name</dt>
            <dd>{bookmark.name}</dd>
            <dt>URL</dt>
            <dd className={classes.mono} title={bookmark.url}>
              {bookmark.url}
            </dd>
            <dt>Icon</dt>
            <dd className={classes.mono}>{bookmark.icon || 'favicon'}</dd>
          </dl>
        </div>

        <CategoryField
          categories={categories}
          value={category}
          onChange={setCategory}
          newName={newCategory}
          onNewNameChange={(v) => {
            setNewCategory(v)
            setNewCategoryError(undefined)
          }}
          newNameError={newCategoryError}
        />
        <TagsField value={tags} onChange={setTags} hostTag={bookmark.hostTag} />
        <SharedField
          value={share}
          onChange={(next) => {
            setShare(next)
            setShareProblem(null)
          }}
          bookmark={bookmark}
          error={shareProblem}
          name={bookmark.name}
          categoryName={categoryName(categories, category, newCategory)}
          docker
        />

        <Group gap="xs" justify="flex-end">
          <Button
            variant="subtle"
            mr="auto"
            loading={reset.isPending}
            disabled={!docker.categoryOverridden && !docker.tagsOverridden}
            title="Use the category and tags from the container's labels again"
            onClick={() => reset.mutate(bookmark.id, { onSuccess: onDone })}
          >
            Reset to labels
          </Button>
          <Button variant="default" onClick={onDone} disabled={update.isPending || reset.isPending}>
            Cancel
          </Button>
          <Button type="submit" loading={update.isPending}>
            Save
          </Button>
        </Group>
      </Stack>
    </form>
  )
}
