import { CloseButton, Kbd, TextInput } from '@mantine/core'
import { useHotkeys } from '@mantine/hooks'
import { IconSearch } from '@tabler/icons-react'
import { useRef } from 'react'

import { ThemeMenu } from './ThemeMenu.tsx'
import classes from './TopBar.module.css'

interface Props {
  query: string
  onQueryChange: (query: string) => void
  /** Enter in the search box: open the first result. */
  onSubmit: () => void
}

/** The Foyer name, a centered search box (/ focuses it), and the theme picker. */
export function TopBar({ query, onQueryChange, onSubmit }: Props) {
  const input = useRef<HTMLInputElement>(null)
  useHotkeys([['/', () => input.current?.focus()]])

  return (
    <div className={classes.bar}>
      <div className={classes.inner}>
        <div className={classes.brand}>Foyer</div>
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
        <div className={classes.actions}>
          <ThemeMenu />
        </div>
      </div>
    </div>
  )
}
