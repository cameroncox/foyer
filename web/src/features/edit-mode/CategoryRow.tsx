import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { ActionIcon, Button, Group, TextInput } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconGripVertical, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'

import type { DashboardCategory } from '../../api/client.ts'
import { useDeleteCategory, useRenameCategory } from '../../api/mutations.ts'
import { blockedDelete } from '../sharing/shared.ts'
import classes from './CategoryDrawer.module.css'

/**
 * A drawer row: handle, inline rename (Enter or leaving the field saves, Esc reverts), count,
 * delete. Delete asks first, or explains why not when the category holds another profile's
 * shared bookmarks.
 */
export function CategoryRow({ category }: { category: DashboardCategory }) {
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: category.id })
  const rename = useRenameCategory()
  const remove = useDeleteCategory()
  const [name, setName] = useState(category.name)
  const [confirming, setConfirming] = useState(false)
  const count = category.bookmarks.length
  const blocked = blockedDelete(category)

  const saveName = () => {
    const trimmed = name.trim()
    if (!trimmed || trimmed === category.name) {
      setName(category.name)
      return
    }

    rename.mutate(
      { id: category.id, name: trimmed },
      {
        onError: (error) => {
          setName(category.name)
          notifications.show({ color: 'red', title: 'Couldn’t rename', message: error.message })
        },
      },
    )
  }

  if (confirming && blocked) {
    return (
      <li className={classes.confirm}>
        <div>{blocked}</div>
        <Group justify="flex-end" gap="xs">
          <Button variant="default" size="xs" onClick={() => setConfirming(false)}>
            OK
          </Button>
        </Group>
      </li>
    )
  }

  if (confirming) {
    return (
      <li className={classes.confirm}>
        <div>
          <strong>Delete “{category.name}”?</strong>{' '}
          {count === 0
            ? 'It has no bookmarks.'
            : `Its ${count === 1 ? 'bookmark moves' : `${count} bookmarks move`} to Uncategorized.`}
        </div>
        <Group justify="flex-end" gap="xs">
          <Button variant="default" size="xs" onClick={() => setConfirming(false)}>
            Cancel
          </Button>
          <Button
            color="red"
            size="xs"
            loading={remove.isPending}
            onClick={() => remove.mutate(category.id)}
          >
            Delete
          </Button>
        </Group>
      </li>
    )
  }

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={classes.row}
      data-dragging={isDragging || undefined}
    >
      <button
        type="button"
        ref={setActivatorNodeRef}
        {...attributes}
        {...listeners}
        className={classes.handle}
        aria-label={`Drag ${category.name}`}
      >
        <IconGripVertical size={18} />
      </button>
      <TextInput
        className={classes.name}
        variant="unstyled"
        aria-label={`Rename ${category.name}`}
        value={name}
        onChange={(e) => setName(e.currentTarget.value)}
        onBlur={saveName}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.currentTarget.blur()
          } else if (e.key === 'Escape') {
            setName(category.name)
          }
        }}
      />
      <span className={classes.count}>{count}</span>
      <ActionIcon
        variant="subtle"
        color="red"
        size={36}
        aria-label={`Delete ${category.name}`}
        onClick={() => setConfirming(true)}
      >
        <IconTrash size={16} />
      </ActionIcon>
    </li>
  )
}
