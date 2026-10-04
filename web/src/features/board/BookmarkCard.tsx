import { Tooltip } from '@mantine/core'

import type { Bookmark } from '../../api/client.ts'
import { BookmarkIcon } from '../../components/BookmarkIcon.tsx'
import { TagChip, TagOverflowChip } from '../../components/TagChip.tsx'
import classes from './BookmarkCard.module.css'
import { statusLabel } from './status.ts'

interface Props {
  bookmark: Bookmark
  /** On a phone, shrink to a tile in the board's 3-across grid: icon over name, no tags. */
  tile?: boolean
}

/** Chips shown before the rest collapse into a "+N" chip; the host tag counts as one. */
const VISIBLE_TAGS = 2

/**
 * Icon, then name with up to two tags under it. The URL, status and any tags left off show on
 * hover; clicking opens the URL in a new tab.
 */
export function BookmarkCard({ bookmark, tile }: Props) {
  const status = statusLabel(bookmark)
  const chips = [
    ...(bookmark.hostTag ? [{ tag: bookmark.hostTag, host: true }] : []),
    ...bookmark.tags.map((tag) => ({ tag, host: false })),
  ]
  const shown = chips.slice(0, VISIBLE_TAGS)
  const hidden = chips.length - shown.length

  return (
    <Tooltip
      label={[bookmark.url, status, hidden ? chips.map((c) => `#${c.tag}`).join(' ') : null]
        .filter(Boolean)
        .join(' · ')}
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
          {chips.length > 0 && (
            <div className={classes.tags}>
              {shown.map((c) => (
                <TagChip key={`${c.host}:${c.tag}`} tag={c.tag} host={c.host} />
              ))}
              {hidden > 0 && <TagOverflowChip count={hidden} />}
            </div>
          )}
        </div>
      </a>
    </Tooltip>
  )
}
