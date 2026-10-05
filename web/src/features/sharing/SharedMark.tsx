import { Tooltip } from '@mantine/core'
import { IconUsers } from '@tabler/icons-react'

import type { Bookmark } from '../../api/client.ts'
import { sharedLabel } from './shared.ts'
import classes from './Sharing.module.css'

/** The small people icon on a shared card, naming who it's from. Nothing when not shared. */
export function SharedMark({ bookmark, className }: { bookmark: Bookmark; className?: string }) {
  const label = sharedLabel(bookmark)
  if (!label) {
    return null
  }

  return (
    <Tooltip label={label} withinPortal openDelay={300}>
      <span
        className={className ? `${classes.mark} ${className}` : classes.mark}
        role="img"
        aria-label={label}
      >
        <IconUsers size={14} stroke={2} aria-hidden="true" />
      </span>
    </Tooltip>
  )
}
