import { Button, CloseButton, Kbd, TextInput } from '@mantine/core'
import { useHotkeys } from '@mantine/hooks'
import { IconCheck, IconPencil, IconSearch } from '@tabler/icons-react'
import { useRef } from 'react'

import { EditHint } from '../edit-mode/EditHint.tsx'
import { ThemeMenu } from './ThemeMenu.tsx'
import classes from './TopBar.module.css'

interface Props {
  query: string
  onQueryChange: (query: string) => void
  /** Enter in the search box: open the first result. */
  onSubmit: () => void
  editing: boolean
  onEditingChange: (editing: boolean) => void
}

/** The Foyer name, a centered search box (/ focuses it), the theme picker and Edit / Done. */
export function TopBar({ query, onQueryChange, onSubmit, editing, onEditingChange }: Props) {
  const input = useRef<HTMLInputElement>(null)
  useHotkeys([['/', () => !editing && input.current?.focus()]])

  return (
    <div className={classes.bar}>
      <div className={classes.inner}>
        <div className={classes.brand}>Foyer</div>
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
