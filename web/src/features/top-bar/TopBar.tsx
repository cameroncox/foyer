import { ActionIcon, Button, CloseButton, Kbd, TextInput } from '@mantine/core'
import { useHotkeys } from '@mantine/hooks'
import { IconCheck, IconList, IconMenu2, IconPencil, IconSearch } from '@tabler/icons-react'
import { useRef, useState } from 'react'

import { usePhone } from '../../hooks/usePhone.ts'
import { useTitle } from '../../hooks/useTitle.ts'
import { EditHint } from '../edit-mode/EditHint.tsx'
import { PhoneMenu } from './PhoneMenu.tsx'
import { ThemeMenu } from './ThemeMenu.tsx'
import classes from './TopBar.module.css'

interface Props {
  query: string
  onQueryChange: (query: string) => void
  /** Enter in the search box: open the first result. */
  onSubmit: () => void
  editing: boolean
  onEditingChange: (editing: boolean) => void
  /** Phone menu's Add bookmark. */
  onAdd: () => void
  /** Phone edit mode's Categories button; the drawer is a full screen there. */
  onOpenCategories: () => void
}

/**
 * The Foyer name, a centered search box (/ focuses it), the theme picker and Edit / Done. On a
 * phone: the name and a menu holding the rest, or Categories and Done while editing.
 */
export function TopBar(props: Props) {
  return usePhone() ? <PhoneTopBar {...props} /> : <DesktopTopBar {...props} />
}

function DesktopTopBar({ query, onQueryChange, onSubmit, editing, onEditingChange }: Props) {
  const title = useTitle()
  const input = useRef<HTMLInputElement>(null)
  useHotkeys([['/', () => !editing && input.current?.focus()]])

  return (
    <div className={classes.bar}>
      <div className={classes.inner}>
        <div className={classes.brand}>{title}</div>
        {editing ? (
          <EditHint className={classes.search} />
        ) : (
          <TextInput
            ref={input}
            className={classes.search}
            type="search"
            size="md"
            placeholder="Search bookmarks"
            aria-label="Search bookmarks"
            value={query}
            onChange={(e) => onQueryChange(e.currentTarget.value)}
            onKeyDown={(e) => {
              if (e.key === 'Escape') {
                e.preventDefault()
                onQueryChange('')
              } else if (e.key === 'Enter') {
                e.preventDefault()
                onSubmit()
              }
            }}
            leftSection={<IconSearch size={18} stroke={2} aria-hidden="true" />}
            rightSection={
              query ? (
                <CloseButton aria-label="Clear search" onClick={() => onQueryChange('')} />
              ) : (
                <Kbd size="xs" aria-hidden="true">
                  /
                </Kbd>
              )
            }
          />
        )}
        <div className={classes.actions}>
          <ThemeMenu />
          {editing ? (
            <Button
              size="md"
              leftSection={<IconCheck size={16} />}
              onClick={() => onEditingChange(false)}
            >
              Done
            </Button>
          ) : (
            <Button
              size="md"
              variant="default"
              leftSection={<IconPencil size={16} />}
              onClick={() => onEditingChange(true)}
            >
              Edit
            </Button>
          )}
        </div>
      </div>
    </div>
  )
}

function PhoneTopBar({
  query,
  onQueryChange,
  editing,
  onEditingChange,
  onAdd,
  onOpenCategories,
}: Props) {
  const [menuOpen, setMenuOpen] = useState(false)
  const title = useTitle()

  if (editing) {
    return (
      <div className={classes.bar}>
        <div className={classes.inner}>
          <div className={classes.editingTitle}>Editing</div>
          <Button variant="default" leftSection={<IconList size={16} />} onClick={onOpenCategories}>
            Categories
          </Button>
          <Button onClick={() => onEditingChange(false)}>Done</Button>
        </div>
      </div>
    )
  }

  return (
    <div className={classes.bar}>
      <div className={classes.inner}>
        <div className={classes.brand}>{title}</div>
        {/* A search made in the menu stays visible here, so it's clear the page is filtered. */}
        {query && (
          <TextInput
            className={classes.search}
            type="search"
            aria-label="Search bookmarks"
            value={query}
            onChange={(e) => onQueryChange(e.currentTarget.value)}
            leftSection={<IconSearch size={16} stroke={2} aria-hidden="true" />}
            rightSection={
              <CloseButton aria-label="Clear search" onClick={() => onQueryChange('')} />
            }
          />
        )}
        <ActionIcon
          variant="subtle"
          color="gray"
          size={44}
          radius="md"
          aria-label="Open menu"
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen(true)}
        >
          <IconMenu2 size={22} />
        </ActionIcon>
      </div>
      <PhoneMenu
        title={title}
        opened={menuOpen}
        onClose={() => setMenuOpen(false)}
        query={query}
        onQueryChange={onQueryChange}
        onAdd={onAdd}
        onEdit={() => onEditingChange(true)}
      />
    </div>
  )
}
