import { ActionIcon, Drawer, Popover, ScrollArea } from '@mantine/core'
import { IconPlus, IconX } from '@tabler/icons-react'

import type { Bookmark, DashboardCategory } from '../../api/client.ts'
import { usePhone } from '../../hooks/usePhone.ts'
import { ReadOnlyBookmark } from '../sharing/ReadOnlyBookmark.tsx'
import { isReadOnly } from '../sharing/shared.ts'
import { BookmarkForm } from './BookmarkForm.tsx'
import { DockerBookmarkForm } from './DockerBookmarkForm.tsx'
import classes from './FormPanel.module.css'

/** The open form's heading, which names the popover's dialog. */
export const FORM_TITLE_ID = 'bookmark-form-title'

export type Panel = { kind: 'add'; name?: string } | { kind: 'edit'; bookmark: Bookmark } | null

interface Props {
  panel: Panel
  onPanelChange: (panel: Panel) => void
  categories: readonly DashboardCategory[]
  /** False on a profile the page can't change: no +. */
  canAdd?: boolean
}

/**
 * The floating + (any mode) and the popover above it, which holds Add or an Edit form (or, for
 * another profile's shared bookmark, what it is and who it's from). While open, the + turns into
 * × and closes it. On a phone the form is a bottom sheet instead. A profile the page can't
 * change has no +.
 */
export function FormPanel({ panel, onPanelChange, categories, canAdd = true }: Props) {
  const close = () => onPanelChange(null)
  const open = panel !== null
  const phone = usePhone()
  const form = <PanelForm panel={panel} categories={categories} onDone={close} />

  if (phone) {
    return (
      <>
        {!open && canAdd && (
          <ActionIcon
            className={classes.fab}
            size={60}
            radius="xl"
            aria-label="Add bookmark"
            onClick={() => onPanelChange({ kind: 'add' })}
          >
            <IconPlus size={26} />
          </ActionIcon>
        )}
        <Drawer
          opened={open}
          onClose={close}
          position="bottom"
          size="auto"
          padding={16}
          withCloseButton={false}
          aria-labelledby={FORM_TITLE_ID}
          styles={{ content: { height: 'auto', borderRadius: '18px 18px 0 0' } }}
          classNames={{ content: classes.sheet }}
        >
          <div className={classes.grabber} aria-hidden="true" />
          {form}
        </Drawer>
      </>
    )
  }

  if (!canAdd && !open) {
    return null
  }

  return (
    <Popover
      opened={open}
      onChange={(o) => !o && close()}
      position="top-end"
      offset={16}
      width={400}
      shadow="lg"
      radius="lg"
      trapFocus
      closeOnClickOutside={false}
      returnFocus
    >
      <Popover.Target>
        <ActionIcon
          className={classes.fab}
          data-open={open || undefined}
          size={60}
          radius="xl"
          aria-label={open ? 'Close' : 'Add bookmark'}
          aria-expanded={open}
          onClick={() => onPanelChange(open ? null : { kind: 'add' })}
        >
          {open ? <IconX size={24} /> : <IconPlus size={26} />}
        </ActionIcon>
      </Popover.Target>
      <Popover.Dropdown p={22} role="dialog" aria-labelledby={FORM_TITLE_ID}>
        <ScrollArea.Autosize mah="calc(100vh - 160px)" offsetScrollbars>
          {form}
        </ScrollArea.Autosize>
      </Popover.Dropdown>
    </Popover>
  )
}

function PanelForm({
  panel,
  categories,
  onDone,
}: {
  panel: Panel
  categories: readonly DashboardCategory[]
  onDone: () => void
}) {
  return (
    <>
      {panel?.kind === 'add' && (
        <BookmarkForm key="add" categories={categories} initialName={panel.name} onDone={onDone} />
      )}
      {panel?.kind === 'edit' &&
        (isReadOnly(panel.bookmark) ? (
          <ReadOnlyBookmark
            key={panel.bookmark.id}
            bookmark={panel.bookmark}
            categoryName={
              categories.find((c) => c.id === panel.bookmark.categoryId)?.name ?? 'its category'
            }
            onDone={onDone}
          />
        ) : panel.bookmark.docker ? (
          <DockerBookmarkForm
            key={panel.bookmark.id}
            categories={categories}
            bookmark={panel.bookmark}
            onDone={onDone}
          />
        ) : (
          <BookmarkForm
            key={panel.bookmark.id}
            categories={categories}
            bookmark={panel.bookmark}
            onDone={onDone}
          />
        ))}
    </>
  )
}
