import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { ActionIcon, Button, Group, Popover, Text } from '@mantine/core'
import { IconGripVertical, IconPencil, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'

import type { Bookmark } from '../../api/client.ts'
import { useDeleteBookmark } from '../../api/mutations.ts'
import { BookmarkIcon } from '../../components/BookmarkIcon.tsx'
import { TagChip } from '../../components/TagChip.tsx'
import classes from './EditCard.module.css'

interface Props {
  bookmark: Bookmark
  editing: boolean
  onEdit: () => void
}

/**
 * A card in edit mode: drag handle (the only place a drag starts), then pencil and delete for
 * manual cards, or the host tag and pencil for Docker cards.
 */
export function EditCard({ bookmark, editing, onEdit }: Props) {
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: bookmark.id })
  const remove = useDeleteBookmark()
  const [confirming, setConfirming] = useState(false)

  return (
    <div
      ref={setNodeRef}
      style={{
        transform: CSS.Translate.toString(transform),
        transition,
      }}
      className={classes.card}
      data-editing={editing || undefined}
      data-dragging={isDragging || undefined}
      data-stopped={bookmark.status === 'stopped' || undefined}
    >
      <button
        type="button"
        ref={setActivatorNodeRef}
        {...attributes}
        {...listeners}
        className={classes.handle}
        aria-label={`Drag ${bookmark.name}`}
        style={{ border: 0, background: 'transparent', padding: 0 }}
      >
        <IconGripVertical size={18} />
      </button>
      <BookmarkIcon bookmark={bookmark} />
      <div className={classes.text}>
        <span className={classes.name}>{bookmark.name}</span>
        <span className={classes.url}>{hostOf(bookmark.url)}</span>
      </div>
      {bookmark.hostTag && <TagChip tag={bookmark.hostTag} host />}
      <ActionIcon
        variant="subtle"
        color="gray"
        size={36}
        aria-label={
          bookmark.docker ? `Edit category and tags of ${bookmark.name}` : `Edit ${bookmark.name}`
        }
        onClick={onEdit}
      >
        <IconPencil size={16} />
      </ActionIcon>
      {!bookmark.docker && (
        <Popover
          opened={confirming}
          onChange={setConfirming}
          position="bottom-end"
          withArrow
          shadow="md"
        >
          <Popover.Target>
            <ActionIcon
              variant="subtle"
              color="red"
              size={36}
              aria-label={`Delete ${bookmark.name}`}
              onClick={() => setConfirming((c) => !c)}
            >
              <IconTrash size={16} />
            </ActionIcon>
          </Popover.Target>
          <Popover.Dropdown>
            <Text size="sm" fw={500}>
              Delete “{bookmark.name}”?
            </Text>
            <Group justify="flex-end" gap="xs" mt="xs">
              <Button variant="default" size="xs" onClick={() => setConfirming(false)}>
                Keep
              </Button>
              <Button
                color="red"
                size="xs"
                loading={remove.isPending}
                onClick={() => remove.mutate(bookmark.id)}
              >
                Delete
              </Button>
            </Group>
          </Popover.Dropdown>
        </Popover>
      )}
    </div>
  )
}

function hostOf(url: string) {
  try {
    return new URL(url).host
  } catch {
    return url
  }
}
