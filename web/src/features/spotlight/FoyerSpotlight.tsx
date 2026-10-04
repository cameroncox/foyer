import { CloseButton, Kbd } from '@mantine/core'
import { createSpotlightStore, Spotlight, type SpotlightStore } from '@mantine/spotlight'
import { IconBrandDocker, IconFolder, IconHash, IconSearch } from '@tabler/icons-react'
import { type ReactNode, useEffect, useMemo, useState } from 'react'

import type { DashboardCategory } from '../../api/client.ts'
import { BookmarkIcon } from '../../components/BookmarkIcon.tsx'
import { navigation } from '../../navigation.ts'
import { statusLabel } from '../board/status.ts'
import classes from './FoyerSpotlight.module.css'
import {
  type Facet,
  facets,
  matchingFacets,
  type Scope,
  type ScopeKind,
  scopeLabel,
  spotlightHits,
} from './scope.ts'

/** Highlights the action at `index` (-1 for none), the way the arrow keys do. */
function select(store: SpotlightStore, index: number) {
  const list = document.getElementById(store.getState().listId)
  list?.querySelector('[data-selected]')?.removeAttribute('data-selected')
  const action = index < 0 ? undefined : list?.querySelectorAll('[data-action]')[index]
  action?.setAttribute('data-selected', 'true')
  store.updateState((state) => ({ ...state, selected: action ? index : -1 }))
}

const MAX_HITS = 50

const FACET_ICONS: Record<ScopeKind, ReactNode> = {
  host: <IconBrandDocker size={18} stroke={1.75} />,
  tag: <IconHash size={18} stroke={1.75} />,
  category: <IconFolder size={18} stroke={1.75} />,
}

interface Props {
  categories: readonly DashboardCategory[]
  /** Off while editing or while a form or modal is up, so Space stays theirs. */
  enabled: boolean
}

/**
 * Space (or ⌘K) opens a jump-to box. Empty, it offers Docker hosts, tags and categories to narrow
 * by; typing searches bookmarks (and those filters). Enter opens the highlighted bookmark.
 */
export function FoyerSpotlight({ categories, enabled }: Props) {
  const [store] = useState(createSpotlightStore)
  const [query, setQuery] = useState('')
  const [scope, setScope] = useState<Scope | null>(null)

  const all = useMemo(() => facets(categories), [categories])
  const hits = useMemo(
    () => spotlightHits(categories, scope, query).slice(0, MAX_HITS),
    [categories, scope, query],
  )
  const browsing = !scope && query.trim() === ''
  const filters = scope || browsing ? [] : matchingFacets(all, query)

  // Typing already highlights the first row (Mantine does that on each query change); picking or
  // clearing a filter swaps the list without one, so do the same there.
  useEffect(() => select(store, scope ? 0 : -1), [store, scope])

  const narrow = (facet: Facet) => {
    setScope({ kind: facet.kind, value: facet.value })
    setQuery('')
  }

  const facetAction = (facet: Facet) => (
    <Spotlight.Action
      key={`${facet.kind}:${facet.value}`}
      label={facet.kind === 'category' ? facet.value : `#${facet.value}`}
      leftSection={FACET_ICONS[facet.kind]}
      rightSection={<span className={classes.count}>{facet.count}</span>}
      closeSpotlightOnTrigger={false}
      onClick={() => narrow(facet)}
    />
  )

  const facetGroup = (label: string, list: Facet[]) =>
    list.length > 0 && (
      <Spotlight.ActionsGroup label={label}>{list.map(facetAction)}</Spotlight.ActionsGroup>
    )

  return (
    <Spotlight.Root
      store={store}
      query={query}
      onQueryChange={setQuery}
      shortcut={enabled ? ['space', 'mod + K'] : null}
      // Space presses a focused button; leave it be.
      tagsToIgnore={['INPUT', 'TEXTAREA', 'SELECT', 'BUTTON']}
      onSpotlightClose={() => setScope(null)}
      scrollable
      maxHeight={440}
      classNames={{ action: classes.action }}
    >
      <Spotlight.Search
        placeholder={scope ? `Search in ${scopeLabel(scope)}` : 'Jump to a bookmark'}
        aria-label="Jump to a bookmark"
        leftSection={<IconSearch size={20} stroke={1.75} />}
        onKeyDown={(e) => {
          if (e.key === 'Backspace' && scope && query === '') {
            e.preventDefault()
            setScope(null)
          }
        }}
      />
      {scope && (
        <div className={classes.scope}>
          <span>Showing</span>
          <span className={classes.scopeChip}>
            {FACET_ICONS[scope.kind]}
            {scopeLabel(scope)}
          </span>
          <CloseButton size="sm" aria-label="Clear filter" onClick={() => setScope(null)} />
        </div>
      )}
      <Spotlight.ActionsList>
        {browsing ? (
          <>
            {facetGroup('Docker hosts', all.hosts)}
            {facetGroup('Tags', all.tags)}
            {facetGroup('Categories', all.categories)}
          </>
        ) : (
          <>
            {hits.length > 0 && (
              <Spotlight.ActionsGroup label="Bookmarks">
                {hits.map(({ bookmark }) => (
                  <Spotlight.Action
                    key={bookmark.id}
                    label={bookmark.name}
                    description={bookmark.url.replace(/^https?:\/\//, '')}
                    leftSection={
                      <BookmarkIcon
                        bookmark={bookmark}
                        statusLabel={statusLabel(bookmark)}
                        compact
                      />
                    }
                    dimmedSections={false}
                    highlightQuery
                    onClick={() => navigation.open(bookmark.url)}
                  />
                ))}
              </Spotlight.ActionsGroup>
            )}
            {facetGroup('Filters', filters)}
          </>
        )}
        {!browsing && hits.length === 0 && filters.length === 0 && (
          <Spotlight.Empty>Nothing matches “{query.trim()}”</Spotlight.Empty>
        )}
        {browsing && all.categories.length === 0 && (
          <Spotlight.Empty>No bookmarks yet</Spotlight.Empty>
        )}
      </Spotlight.ActionsList>
      <Spotlight.Footer className={classes.footer}>
        <Kbd size="xs">↑</Kbd>
        <Kbd size="xs">↓</Kbd> move · <Kbd size="xs">Enter</Kbd> {browsing ? 'filter' : 'open'}
        {scope && (
          <>
            {' '}
            · <Kbd size="xs">⌫</Kbd> clear filter
          </>
        )}{' '}
        · <Kbd size="xs">Esc</Kbd> close
      </Spotlight.Footer>
    </Spotlight.Root>
  )
}
