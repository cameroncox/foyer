import { Tooltip } from '@mantine/core'

import type { Bookmark } from '../../api/client.ts'
import { BookmarkIcon } from '../../components/BookmarkIcon.tsx'
import { TagChip } from '../../components/TagChip.tsx'
import classes from './BookmarkCard.module.css'
import { statusLabel } from './status.ts'

interface Props {
  bookmark: Bookmark
  /** On a phone, shrink to a tile in the board's 3-across grid: icon over name, no tags. */
  tile?: boolean
}

/** Icon, then name with tags under it. The URL and status show on hover; clicking opens the URL in a new tab. */
export function BookmarkCard({ bookmark, tile }: Props) {
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
        target="_blank"
        rel="noopener noreferrer"
        className={classes.card}
        data-stopped={bookmark.status === 'stopped' || undefined}
        data-tile={tile || undefined}
      >
        <BookmarkIcon bookmark={bookmark} statusLabel={status} />
        <div className={classes.body}>
          <span className={classes.name}>{bookmark.name}</span>
          {(bookmark.hostTag || bookmark.tags.length > 0) && (
            <div className={classes.tags}>
              {bookmark.hostTag && <TagChip tag={bookmark.hostTag} host />}
              {bookmark.tags.map((tag) => (
                <TagChip key={tag} tag={tag} />
              ))}
            </div>
          )}
        </div>
      </a>
    </Tooltip>
  )
}
