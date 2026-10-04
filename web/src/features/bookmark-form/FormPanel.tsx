import { ActionIcon, Popover, ScrollArea } from '@mantine/core'
import { IconPlus, IconX } from '@tabler/icons-react'

import type { Bookmark, DashboardCategory } from '../../api/client.ts'
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
}

/**
 * The floating + (any mode) and the popover above it, which holds Add or an Edit form. While
 * open, the + turns into × and closes it.
 */
export function FormPanel({ panel, onPanelChange, categories }: Props) {
  const close = () => onPanelChange(null)
  const open = panel !== null

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
          {panel?.kind === 'add' && (
            <BookmarkForm
              key="add"
              categories={categories}
              initialName={panel.name}
              onDone={close}
            />
          )}
          {panel?.kind === 'edit' &&
            (panel.bookmark.docker ? (
              <DockerBookmarkForm
                key={panel.bookmark.id}
                categories={categories}
                bookmark={panel.bookmark}
                onDone={close}
              />
            ) : (
              <BookmarkForm
                key={panel.bookmark.id}
                categories={categories}
                bookmark={panel.bookmark}
                onDone={close}
              />
            ))}
        </ScrollArea.Autosize>
      </Popover.Dropdown>
    </Popover>
  )
}
