import { Button, Select, Text } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconBookmarkPlus } from '@tabler/icons-react'
import { useEffect, useRef, useState } from 'react'

import type { DashboardCategory } from '../../api/client.ts'
import { usePhone } from '../../hooks/usePhone.ts'
import { useTitle } from '../../hooks/useTitle.ts'
import { bookmarkletHref } from './bookmarklet.ts'

interface Props {
  categories: readonly DashboardCategory[]
}

/**
 * A link to drag to the bookmarks bar; clicked there, it adds the current page to Foyer. The
 * category picked above it is built into the link, so each category can have its own. Hidden on
 * phones: there's no bookmarks bar to drag to, and a bookmarklet run from a phone's bookmarks
 * list doesn't see the page it was opened from.
 */
export function BookmarkletLink({ categories }: Props) {
  const title = useTitle() || 'Foyer'
  const phone = usePhone()
  const link = useRef<HTMLAnchorElement>(null)
  const [picked, setPicked] = useState<string | null>(null)
  // Uncategorized, or a category since deleted, needs no category in the link.
  const category = categories.find((c) => String(c.id) === picked && !c.isSystem)

  const href = bookmarkletHref(window.location.origin, category?.id)
  const label = category ? `Add to ${title}: ${category.name}` : `Add to ${title}`

  // React refuses javascript: URLs in href, so it's set on the element directly.
  useEffect(() => {
    link.current?.setAttribute('href', href)
  }, [href, phone])

  if (phone) {
    return null
  }

  return (
    <div>
      <Select
        label="Bookmarklet category"
        size="xs"
        mb={8}
        data={categories.map((c) => ({ value: String(c.id), label: c.name }))}
        value={String(category?.id ?? categories.find((c) => c.isSystem)?.id ?? '')}
        onChange={setPicked}
        allowDeselect={false}
      />
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
        {label}
      </Button>
      <Text size="xs" c="dimmed" mt={6}>
        Drag to your bookmarks bar, then click it on any page to bookmark it.
      </Text>
    </div>
  )
}
