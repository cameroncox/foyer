import { IconBrandDocker } from '@tabler/icons-react'

import classes from './TagChip.module.css'

interface Props {
  tag: string
  /** The automatic host tag: filled, with a Docker icon. */
  host?: boolean
  highlighted?: boolean
}

export function TagChip({ tag, host, highlighted }: Props) {
  return (
    <span
      className={classes.chip}
      data-host={host || undefined}
      data-highlighted={highlighted || undefined}
    >
      {host && <IconBrandDocker size={11} stroke={2} aria-hidden="true" />}#{tag}
    </span>
  )
}
