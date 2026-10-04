import { IconBrandDocker } from '@tabler/icons-react'

import classes from './TagChip.module.css'

interface Props {
  tag: string
  /** The automatic host tag: filled, with a Docker icon. */
  host?: boolean
}

export function TagChip({ tag, host }: Props) {
  return (
    <span className={classes.chip} data-host={host || undefined}>
      {host && <IconBrandDocker size={11} stroke={2} aria-hidden="true" />}#{tag}
    </span>
  )
}

/** Stands in for the tags a card leaves off; the full list is in the card's tooltip. */
export function TagOverflowChip({ count }: { count: number }) {
  return (
    <span className={classes.chip} data-overflow>
      +{count}
    </span>
  )
}
