import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { ActionIcon, Button, Checkbox, Group, Popover, Text } from '@mantine/core'
import { IconGripVertical, IconLock, IconPencil, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'

import type { Bookmark } from '../../api/client.ts'
import { useDeleteBookmark } from '../../api/mutations.ts'
import { BookmarkIcon } from '../../components/BookmarkIcon.tsx'
import { TagChip } from '../../components/TagChip.tsx'
import { usePhone } from '../../hooks/usePhone.ts'
import { SharedMark } from '../sharing/SharedMark.tsx'
import { isReadOnly, sourceLabel } from '../sharing/shared.ts'
import classes from './EditCard.module.css'
import { isSelectable, notSelectableReason } from './selection.ts'

interface Props {
  bookmark: Bookmark
  editing: boolean
  onEdit: () => void
  /** Set while picking bookmarks to delete: the card is a checkbox instead. */
  selection?: { selected: boolean; onToggle: () => void }
}

/**
 * A card in edit mode: drag handle (the only place a drag starts), then pencil and delete for
 * manual cards, or the host tag and pencil for Docker cards. Another profile's shared card says
 * who it's from and has a lock instead, which opens a read-only view; it only reorders within
 * its category. On a phone the whole card opens the form (Delete lives there) and the handle
 * moves to the right, where a thumb finds it. While selecting, the whole card toggles its
 * checkbox; Docker and other profiles' cards can't be picked.
 */
export function EditCard({ bookmark, editing, onEdit, selection }: Props) {
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
  const phone = usePhone()
  const readOnly = isReadOnly(bookmark)
  const editLabel = readOnly
    ? `View ${bookmark.name}`
    : bookmark.docker
      ? `Edit category and tags of ${bookmark.name}`
      : `Edit ${bookmark.name}`

  const handle = (
    <button
      type="button"
      ref={setActivatorNodeRef}
      {...attributes}
      {...listeners}
      className={classes.handle}
      aria-label={`Drag ${bookmark.name}`}
    >
      <IconGripVertical size={phone ? 20 : 16} />
    </button>
  )
  const body = (
    <>
      <BookmarkIcon bookmark={bookmark} compact />
      <div className={classes.text}>
        <span className={classes.name}>{bookmark.name}</span>
        {readOnly ? (
          <span className={classes.url}>{sourceLabel(bookmark)}</span>
        ) : (
          <span className={classes.meta}>
            {bookmark.hostTag ? (
              <TagChip tag={bookmark.hostTag} host />
            ) : (
              <span className={classes.url}>{hostOf(bookmark.url)}</span>
            )}
            <SharedMark bookmark={bookmark} />
          </span>
        )}
      </div>
    </>
  )

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
      data-selected={selection?.selected || undefined}
    >
      {selection ? (
        <label
          className={classes.pick}
          data-disabled={!isSelectable(bookmark) || undefined}
          title={isSelectable(bookmark) ? undefined : notSelectableReason(bookmark)}
        >
          <Checkbox
            aria-label={`Select ${bookmark.name}`}
            checked={selection.selected}
            disabled={!isSelectable(bookmark)}
            onChange={selection.onToggle}
          />
          {body}
        </label>
      ) : phone ? (
        <>
          <button type="button" className={classes.body} aria-label={editLabel} onClick={onEdit}>
            {body}
          </button>
          {handle}
        </>
      ) : (
        <>
          {handle}
          {body}
          <ActionIcon
            variant="subtle"
            color="gray"
            size={30}
            aria-label={editLabel}
            onClick={onEdit}
          >
            {readOnly ? <IconLock size={16} /> : <IconPencil size={16} />}
          </ActionIcon>
          {!bookmark.docker && !readOnly && (
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
                  size={30}
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
        </>
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
