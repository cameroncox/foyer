import { Code } from '@mantine/core'
import { IconBookmark } from '@tabler/icons-react'

import classes from './EmptyState.module.css'

export function EmptyState() {
  return (
    <section className={classes.box}>
      <div className={classes.badge}>
        <IconBookmark size={30} stroke={1.8} aria-hidden="true" />
      </div>
      <h1 className={classes.title}>No bookmarks yet</h1>
      <p className={classes.text}>
        Add one by hand, or label a container with <Code>coxdev.bookmark.enabled=true</Code> and it
        will show up here.
      </p>
    </section>
  )
}
