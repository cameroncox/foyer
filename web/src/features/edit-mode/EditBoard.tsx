import {
  closestCorners,
  DndContext,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
  KeyboardSensor,
  PointerSensor,
  useDroppable,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  arrayMove,
  rectSortingStrategy,
  SortableContext,
  sortableKeyboardCoordinates,
} from '@dnd-kit/sortable'
import { useMemo, useRef, useState } from 'react'

import type { Bookmark, Dashboard } from '../../api/client.ts'
import { type Containers, findContainer, moveAcross, toContainers } from './containers.ts'
import classes from './EditBoard.module.css'
import { EditCard } from './EditCard.tsx'
import { moveBookmark, type Slot } from './reorder.ts'

const categoryKey = (id: number) => `category-${id}`
const parseCategoryKey = (key: string | number) =>
  typeof key === 'string' && key.startsWith('category-')
    ? Number(key.slice('category-'.length))
    : undefined

interface Props {
  dashboard: Dashboard
  editingId?: number
  onEdit: (bookmark: Bookmark) => void
  /** Saves a drop: the dashboard as it should look, and the request for the target category. */
  onMove: (result: NonNullable<ReturnType<typeof moveBookmark>>) => void
}

/**
 * Every category (empty ones too, as drop targets) as a grid of draggable cards. A card moves
 * between categories as it's dragged over them; the drop sends one reorder request.
 */
export function EditBoard({ dashboard, editingId, onEdit, onMove }: Props) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )
  const [dragging, setDragging] = useState<Containers | null>(null)
  const from = useRef<Slot | null>(null)
  const byId = useMemo(
    () => new Map(dashboard.categories.flatMap((c) => c.bookmarks.map((b) => [b.id, b] as const))),
    [dashboard],
  )
  const containers = dragging ?? toContainers(dashboard)

  const onDragStart = ({ active }: DragStartEvent) => {
    const start = toContainers(dashboard)
    const categoryId = findContainer(start, Number(active.id))!
    from.current = { categoryId, index: start[categoryId].indexOf(Number(active.id)) }
    setDragging(start)
  }

  const onDragOver = ({ active, over }: DragOverEvent) => {
    if (!over || !dragging) {
      return
    }

    const overCategory = parseCategoryKey(over.id) ?? findContainer(dragging, Number(over.id))
    if (overCategory === undefined) {
      return
    }

    const overIndex =
      parseCategoryKey(over.id) === undefined
        ? dragging[overCategory].indexOf(Number(over.id))
        : undefined
    setDragging(moveAcross(dragging, Number(active.id), overCategory, overIndex))
  }

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    const working = dragging
    const start = from.current
    setDragging(null)
    from.current = null
    if (!over || !working || !start) {
      return
    }

    const id = Number(active.id)
    const categoryId = findContainer(working, id)!
    let ids = working[categoryId]
    if (parseCategoryKey(over.id) === undefined && ids.includes(Number(over.id))) {
      ids = arrayMove(ids, ids.indexOf(id), ids.indexOf(Number(over.id)))
    }

    const result = moveBookmark(dashboard, start, { categoryId, index: ids.indexOf(id) })
    if (result) {
      onMove(result)
    }
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCorners}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDragEnd={onDragEnd}
      onDragCancel={() => setDragging(null)}
    >
      <div className={classes.board}>
        {dashboard.categories.map((category) => (
          <CategoryGrid
            key={category.id}
            id={category.id}
            name={category.name}
            bookmarks={(containers[category.id] ?? []).map((id) => byId.get(id)!).filter(Boolean)}
            editingId={editingId}
            onEdit={onEdit}
          />
        ))}
      </div>
    </DndContext>
  )
}

interface CategoryGridProps {
  id: number
  name: string
  bookmarks: Bookmark[]
  editingId?: number
  onEdit: (bookmark: Bookmark) => void
}

function CategoryGrid({ id, name, bookmarks, editingId, onEdit }: CategoryGridProps) {
  const { setNodeRef, isOver } = useDroppable({ id: categoryKey(id) })

  return (
    <section className={classes.section} aria-labelledby={`edit-category-${id}`}>
      <h2 id={`edit-category-${id}`} className={classes.heading}>
        {name}
        <span className={classes.count}>{bookmarks.length}</span>
      </h2>
      <SortableContext
        id={categoryKey(id)}
        items={bookmarks.map((b) => b.id)}
        strategy={rectSortingStrategy}
      >
        <div ref={setNodeRef} className={classes.grid} data-over={isOver || undefined}>
          {bookmarks.length === 0 && <div className={classes.empty}>Drop bookmarks here</div>}
          {bookmarks.map((bookmark) => (
            <EditCard
              key={bookmark.id}
              bookmark={bookmark}
              editing={bookmark.id === editingId}
              onEdit={() => onEdit(bookmark)}
            />
          ))}
        </div>
      </SortableContext>
    </section>
  )
}
