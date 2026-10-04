import {
  closestCenter,
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import { restrictToVerticalAxis } from '@dnd-kit/modifiers'
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { Button, Group, Text, TextInput } from '@mantine/core'
import { IconDownload } from '@tabler/icons-react'
import { useState } from 'react'

import type { Dashboard } from '../../api/client.ts'
import { useCreateCategory } from '../../api/mutations.ts'
import classes from './CategoryDrawer.module.css'
import { CategoryRow } from './CategoryRow.tsx'
import { moveCategory } from './reorder.ts'

interface Props {
  dashboard: Dashboard
  onReorder: (result: NonNullable<ReturnType<typeof moveCategory>>) => void
  onImport: () => void
}

/** Edit mode's right-hand drawer: categories in order, Uncategorized pinned last, add and import. */
export function CategoryDrawer({ dashboard, onReorder, onImport }: Props) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )
  const create = useCreateCategory()
  const [newName, setNewName] = useState('')
  const movable = dashboard.categories.filter((c) => !c.isSystem)
  const pinned = dashboard.categories.find((c) => c.isSystem)

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    if (!over) {
      return
    }

    const result = moveCategory(
      dashboard,
      movable.findIndex((c) => c.id === active.id),
      movable.findIndex((c) => c.id === over.id),
    )
    if (result) {
      onReorder(result)
    }
  }

  const add = (event: React.FormEvent) => {
    event.preventDefault()
    if (newName.trim()) {
      create.mutate(newName.trim(), { onSuccess: () => setNewName('') })
    }
  }

  return (
    <div className={classes.drawer}>
      <div className={classes.header}>
        <h2 className={classes.title}>Categories</h2>
        <Text size="xs" c="dimmed">
          Drag to reorder
        </Text>
      </div>

      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        modifiers={[restrictToVerticalAxis]}
        onDragEnd={onDragEnd}
      >
        <SortableContext items={movable.map((c) => c.id)} strategy={verticalListSortingStrategy}>
          <ol className={classes.list} aria-label="Categories">
            {movable.map((category) => (
              <CategoryRow key={`${category.id}:${category.name}`} category={category} />
            ))}
            {pinned && (
              <li className={classes.pinned}>
                <span style={{ flex: 1 }}>{pinned.name}</span>
                <Text size="xs" c="dimmed">
                  always last
                </Text>
                <span className={classes.count}>{pinned.bookmarks.length}</span>
              </li>
            )}
          </ol>
        </SortableContext>
      </DndContext>

      <form onSubmit={add}>
        <Group gap={8} wrap="nowrap" align="flex-start">
          <TextInput
            style={{ flex: 1 }}
            aria-label="New category"
            placeholder="New category"
            value={newName}
            onChange={(e) => {
              setNewName(e.currentTarget.value)
              create.reset()
            }}
            error={create.error?.message}
          />
          <Button type="submit" variant="outline" loading={create.isPending}>
            Add
          </Button>
        </Group>
      </form>

      <div className={classes.footer}>
        <Button
          fullWidth
          variant="default"
          leftSection={<IconDownload size={16} />}
          onClick={onImport}
        >
          Import browser bookmarks
        </Button>
      </div>
    </div>
  )
}
