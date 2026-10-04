import { Button, Text } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconBookmarkPlus } from '@tabler/icons-react'
import { useEffect, useRef } from 'react'

import { useTitle } from '../../hooks/useTitle.ts'
import { bookmarkletHref } from './bookmarklet.ts'

/** A link to drag to the bookmarks bar; clicked there, it adds the current page to Foyer. */
export function BookmarkletLink() {
  const title = useTitle() || 'Foyer'
  const link = useRef<HTMLAnchorElement>(null)

  // React refuses javascript: URLs in href, so it's set on the element directly.
  useEffect(() => {
    link.current?.setAttribute('href', bookmarkletHref(window.location.origin))
  }, [])

  return (
    <div>
      <Button
        component="a"
        ref={link}
        fullWidth
        variant="default"
        leftSection={<IconBookmarkPlus size={16} />}
        onClick={(e) => {
          e.preventDefault()
          notifications.show({ message: 'Drag this to your bookmarks bar, then click it there.' })
        }}
      >
        Add to {title}
      </Button>
      <Text size="xs" c="dimmed" mt={6}>
        Drag to your bookmarks bar, then click it on any page to bookmark it.
      </Text>
    </div>
  )
}
