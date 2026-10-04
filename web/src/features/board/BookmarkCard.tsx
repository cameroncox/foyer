import { Tooltip } from '@mantine/core'

import type { Bookmark } from '../../api/client.ts'
import { BookmarkIcon } from '../../components/BookmarkIcon.tsx'
import { TagChip } from '../../components/TagChip.tsx'
import classes from './BookmarkCard.module.css'
import { statusLabel } from './status.ts'

interface Props {
  bookmark: Bookmark
  /** A tag to highlight because search matched it. */
  matchedTag?: string
  /** The first search result: Enter opens it, so it carries a focus ring. */
  first?: boolean
}

/** Icon, then name with tags under it. The URL and status show on hover; clicking opens the URL. */
export function BookmarkCard({ bookmark, matchedTag, first }: Props) {
  const status = statusLabel(bookmark)

  return (
    <Tooltip
      label={status ? `${bookmark.url} · ${status}` : bookmark.url}
      openDelay={500}
      position="bottom-start"
      withinPortal
    >
      <a
        href={bookmark.url}
        className={classes.card}
        data-stopped={bookmark.status === 'stopped' || undefined}
        data-first={first || undefined}
      >
        <BookmarkIcon bookmark={bookmark} statusLabel={status} />
        <div className={classes.body}>
          <span className={classes.name}>{bookmark.name}</span>
          {(bookmark.hostTag || bookmark.tags.length > 0) && (
            <div className={classes.tags}>
              {bookmark.hostTag && (
                <TagChip
                  tag={bookmark.hostTag}
                  host
                  highlighted={matchedTag === bookmark.hostTag}
                />
              )}
              {bookmark.tags.map((tag) => (
                <TagChip key={tag} tag={tag} highlighted={matchedTag === tag} />
              ))}
            </div>
          )}
        </div>
      </a>
    </Tooltip>
  )
}
