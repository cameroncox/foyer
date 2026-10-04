import { Kbd } from '@mantine/core'

import { BookmarkCard } from '../board/BookmarkCard.tsx'
import type { SearchHit } from './search.ts'
import { matchCountLine } from './search.ts'
import classes from './SearchResults.module.css'

interface Props {
  query: string
  hits: readonly SearchHit[]
}

/** One flat grid of matches, the first ringed because Enter opens it. */
export function SearchResults({ query, hits }: Props) {
  if (hits.length === 0) {
    return <p className={classes.none}>Nothing matches “{query.trim()}”</p>
  }

  return (
    <div className={classes.results}>
      <div className={classes.line} role="status">
        {matchCountLine(hits.length, query)}
      </div>
      <div className={classes.grid}>
        {hits.map((hit, i) => (
          <BookmarkCard
            key={hit.bookmark.id}
            bookmark={hit.bookmark}
            matchedTag={hit.matchedTag}
            first={i === 0}
          />
        ))}
      </div>
      <div className={classes.hint}>
        <Kbd size="xs">Enter</Kbd> opens the first result · <Kbd size="xs">Esc</Kbd> clears
      </div>
    </div>
  )
}
