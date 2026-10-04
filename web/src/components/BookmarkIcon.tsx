import { useState } from 'react'

import type { Bookmark } from '../api/client.ts'
import classes from './BookmarkIcon.module.css'

const MONOCHROME = /^(mdi|si)-(?!.*-#[0-9a-f]{3,6}$)/i

interface Props {
  bookmark: Bookmark
  statusLabel?: string | null
  /** Smaller tile, for edit-mode cards. */
  compact?: boolean
}

/** The resolved icon, or a letter tile when there's none or it fails to load, with the status dot. */
export function BookmarkIcon({ bookmark, statusLabel, compact }: Props) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const showImage = bookmark.iconUrl && bookmark.iconUrl !== failedUrl

  return (
    <div className={classes.tile} data-compact={compact || undefined}>
      {showImage ? (
        <img
          src={bookmark.iconUrl!}
          alt=""
          className={`${classes.image} ${MONOCHROME.test(bookmark.icon ?? '') ? classes.monochrome : ''}`}
          onError={() => setFailedUrl(bookmark.iconUrl)}
        />
      ) : (
        <span aria-hidden="true">{bookmark.name.trim().charAt(0).toUpperCase() || '?'}</span>
      )}
      {bookmark.status && (
        <span
          className={classes.dot}
          data-status={bookmark.status}
          role="img"
          aria-label={statusLabel ?? bookmark.status}
        />
      )}
    </div>
  )
}
