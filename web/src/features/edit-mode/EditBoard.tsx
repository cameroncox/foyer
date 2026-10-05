import {
  closestCenter,
  type CollisionDetection,
  DndContext,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
  getFirstCollision,
  KeyboardSensor,
  PointerSensor,
  pointerWithin,
  rectIntersection,
  type UniqueIdentifier,
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
import { Checkbox } from '@mantine/core'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import type { Bookmark, Dashboard } from '../../api/client.ts'
import { type Containers, findContainer, moveAcross, toContainers } from './containers.ts'
import classes from './EditBoard.module.css'
import { EditCard } from './EditCard.tsx'
import { moveBookmark, type Slot } from './reorder.ts'
import { allState, toggleAll, toggleOne } from './selection.ts'

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
  /** Set while picking bookmarks to delete: cards become checkboxes and nothing drags. */
  selection?: Selection
}

export interface Selection {
  selected: ReadonlySet<number>
  onChange: (selected: Set<number>) => void
}

/**
 * Every category (empty ones too, as drop targets) as a grid of draggable cards. A card moves
 * between categories as it's dragged over them; the drop sends one reorder request. Another
 * profile's shared card stays in its own category, since only its owner can move it.
 */
export function EditBoard({ dashboard, editingId, onEdit, onMove, selection }: Props) {
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
  const containersRef = useRef(containers)
  const lastOverId = useRef<UniqueIdentifier | null>(null)
  const recentlyMoved = useRef(false)

  useEffect(() => {
    containersRef.current = containers
    // A card that just changed category settles in its new place before targets are re-judged.
    requestAnimationFrame(() => {
      recentlyMoved.current = false
    })
  }, [containers])

  /**
   * The category under the pointer wins, then the nearest card inside it decides the slot.
   * Corner-based detection let a nearby card beat an empty category (a new one, typically),
   * so cards landed in the category above or snapped back. Between categories, and right after
   * a move, the last target holds, so layout shifts can't bounce the card around.
   */
  const collisionDetection: CollisionDetection = useCallback((args) => {
    const pointerHits = pointerWithin(args)
    const hits = pointerHits.length > 0 ? pointerHits : rectIntersection(args)
    let overId = getFirstCollision(hits, 'id')

    if (overId != null) {
      const category = parseCategoryKey(overId)
      const ids = category === undefined ? [] : (containersRef.current[category] ?? [])
      if (ids.length > 0) {
        const inCategory = args.droppableContainers.filter((c) => ids.includes(Number(c.id)))
        overId = closestCenter({ ...args, droppableContainers: inCategory })[0]?.id ?? overId
      }

      lastOverId.current = overId
      return [{ id: overId }]
    }

    if (recentlyMoved.current) {
      lastOverId.current = args.active.id
    }

    return lastOverId.current == null ? [] : [{ id: lastOverId.current }]
  }, [])

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

    const card = byId.get(Number(active.id))
    if (card && !card.canEdit && overCategory !== from.current?.categoryId) {
      return
    }

    const overIndex =
      parseCategoryKey(over.id) === undefined
        ? dragging[overCategory].indexOf(Number(over.id))
        : undefined
    const moved = moveAcross(dragging, Number(active.id), overCategory, overIndex)
    if (moved !== dragging) {
      recentlyMoved.current = true
      setDragging(moved)
    }
  }

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    const working = dragging
    const start = from.current
    setDragging(null)
    from.current = null
    lastOverId.current = null
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
      collisionDetection={collisionDetection}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDragEnd={onDragEnd}
      onDragCancel={() => {
        setDragging(null)
        lastOverId.current = null
      }}
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
            selection={selection}
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
  selection?: Selection
}

function CategoryGrid({ id, name, bookmarks, editingId, onEdit, selection }: CategoryGridProps) {
  const { setNodeRef, isOver } = useDroppable({ id: categoryKey(id) })
  const all = selection && allState(selection.selected, bookmarks)

  return (
    <section className={classes.section} aria-labelledby={`edit-category-${id}`}>
      <h2 id={`edit-category-${id}`} className={classes.heading}>
        {selection && all?.selectable && (
          <Checkbox
            size="xs"
            aria-label={`Select all in ${name}`}
            checked={all.checked}
            indeterminate={all.indeterminate}
            onChange={() => selection.onChange(toggleAll(selection.selected, bookmarks))}
          />
        )}
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
              selection={
                selection && {
                  selected: selection.selected.has(bookmark.id),
                  onToggle: () => selection.onChange(toggleOne(selection.selected, bookmark.id)),
                }
              }
            />
          ))}
        </div>
      </SortableContext>
    </section>
  )
}
