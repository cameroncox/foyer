import { Group, Stack, Text, TextInput } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { useState } from 'react'

import classes from './IconField.module.css'

interface Props {
  value: string
  onChange: (value: string) => void
  /** The bookmark URL, for the favicon fallback in the preview. */
  url: string
  /** Shown in the letter tile when there's no image. */
  name: string
}

/** Icon value with a live preview of what the server resolves it to. */
export function IconField({ value, onChange, url, name }: Props) {
  const [query] = useDebouncedValue(
    `icon=${encodeURIComponent(value.trim())}&url=${encodeURIComponent(url.trim())}`,
    400,
  )
  const src = `/api/icons/preview?${query}`
  const [failed, setFailed] = useState<string | null>(null)

  return (
    <Stack gap={6}>
      <Group gap={10} wrap="nowrap" align="flex-end">
        <TextInput
          label="Icon"
          style={{ flex: 1, minWidth: 0 }}
          ff="monospace"
          value={value}
          onChange={(e) => onChange(e.currentTarget.value)}
          placeholder="jellyfin.svg"
        />
        <div className={classes.preview} role="img" aria-label="Icon preview">
          {failed === src ? (
            <span aria-hidden="true">{name.trim().charAt(0).toUpperCase() || '?'}</span>
          ) : (
            <img key={src} src={src} alt="" onError={() => setFailed(src)} />
          )}
        </div>
      </Group>
      <Text size="xs" c="dimmed">
        An image URL, a data: URI, a dashboard-icons name, or mdi-/si- icons. Leave blank to use the
        site&apos;s favicon.
      </Text>
    </Stack>
  )
}
