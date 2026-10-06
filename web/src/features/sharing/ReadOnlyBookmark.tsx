import { Anchor, Button, Group, Stack, Text, Title } from '@mantine/core'
import { IconBrandDocker, IconLock, IconUsers } from '@tabler/icons-react'

import type { Bookmark } from '../../api/client.ts'
import { BookmarkIcon } from '../../components/BookmarkIcon.tsx'
import { TagChip } from '../../components/TagChip.tsx'
import { usePhone } from '../../hooks/usePhone.ts'
import { statusLabel } from '../board/status.ts'
import { isShownDocker, ownerName, sourceLabel } from './shared.ts'
import classes from './Sharing.module.css'

interface Props {
  bookmark: Bookmark
  categoryName: string
  onDone: () => void
}

/**
 * Another profile's shared bookmark, opened from edit mode: what it is and who it's from. Only
 * its owner changes it; here it can just be reordered within its category.
 */
export function ReadOnlyBookmark({ bookmark, categoryName, onDone }: Props) {
  const phone = usePhone()
  const fromDocker = isShownDocker(bookmark)
  const chips = [
    ...(bookmark.hostTag ? [{ tag: bookmark.hostTag, host: true }] : []),
    ...bookmark.tags.map((tag) => ({ tag, host: false })),
  ]

  return (
    <Stack gap="md">
      <Group gap="sm" wrap="nowrap">
        <BookmarkIcon bookmark={bookmark} statusLabel={statusLabel(bookmark)} />
        <Stack gap={2} miw={0}>
          <Title order={2} size="h4" id="bookmark-form-title" tabIndex={-1} data-autofocus>
            {bookmark.name}
          </Title>
          <Text
            size="sm"
            c="var(--mantine-primary-color-filled)"
            style={{ display: 'flex', alignItems: 'center', gap: 5 }}
          >
            {fromDocker ? (
              <IconBrandDocker size={14} aria-hidden="true" />
            ) : (
              <IconUsers size={14} aria-hidden="true" />
            )}
            {sourceLabel(bookmark)}
          </Text>
        </Stack>
      </Group>

      <dl className={classes.details}>
        <dt>URL</dt>
        <dd className={classes.mono}>
          <Anchor href={bookmark.url} inherit>
            {bookmark.url}
          </Anchor>
        </dd>
        <dt>Category</dt>
        <dd>{categoryName}</dd>
        {chips.length > 0 && (
          <>
            <dt>Tags</dt>
            <dd>
              <Group gap={4}>
                {chips.map((c) => (
                  <TagChip key={`${c.host}:${c.tag}`} tag={c.tag} host={c.host} />
                ))}
              </Group>
            </dd>
          </>
        )}
        {bookmark.icon && (
          <>
            <dt>Icon</dt>
            <dd className={classes.mono}>{bookmark.icon}</dd>
          </>
        )}
      </dl>

      <Text size="sm" c="dimmed" style={{ display: 'flex', gap: 8 }}>
        <IconLock size={15} aria-hidden="true" style={{ flex: 'none', marginTop: 2 }} />
        <span>
          {fromDocker ? 'Change it in Default.' : `Only ${ownerName(bookmark)} can change this.`}{' '}
          Drag its {phone ? 'grip' : 'handle'} to reorder it within {categoryName}.
        </span>
      </Text>

      <Group justify="flex-end" gap="xs">
        <Button variant="default" onClick={onDone}>
          Close
        </Button>
        <Button component="a" href={bookmark.url}>
          Open
        </Button>
      </Group>
    </Stack>
  )
}
