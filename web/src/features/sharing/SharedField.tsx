import { Button, Group, Switch, Text } from '@mantine/core'
import { IconUsers } from '@tabler/icons-react'
import { useState } from 'react'

import { useCurrentProfile } from '../profiles/profileContext.ts'
import classes from './Sharing.module.css'

interface Props {
  checked: boolean
  onChange: (shared: boolean) => void
  /** Shared when the form opened: turning it off then asks first. */
  wasShared: boolean
  /** The bookmark's name, for the stop-sharing question. */
  name: string
  /** The category it's saved in, which other profiles show it under. */
  categoryName: string
  /** Docker cards keep their status dot everywhere; the description says so. */
  docker?: boolean
}

/**
 * The Shared switch in Add and Edit: shared bookmarks show, read-only, in every other profile.
 * Turning it off on a bookmark that was shared asks first, since it disappears for everyone
 * else. Hidden with profiles off, when there's no one else to share with.
 */
export function SharedField({ checked, onChange, wasShared, name, categoryName, docker }: Props) {
  const enabled = useCurrentProfile()?.me.profilesEnabled ?? false
  const [confirming, setConfirming] = useState(false)
  if (!enabled) {
    return null
  }

  const description = checked
    ? `Every profile sees it, read-only, in a category named ${categoryName}${docker ? ', with its status dot' : ''}.`
    : 'Only this profile shows it.'

  return (
    <div className={classes.field} data-confirming={confirming || undefined}>
      <Switch
        label={
          <Group gap={6} wrap="nowrap">
            <IconUsers size={15} aria-hidden="true" />
            Shared
          </Group>
        }
        description={description}
        labelPosition="left"
        checked={checked}
        onChange={(e) => {
          const on = e.currentTarget.checked
          if (!on && wasShared) {
            setConfirming(true)
          } else {
            onChange(on)
          }
        }}
        styles={{ body: { justifyContent: 'space-between' }, labelWrapper: { flex: 1 } }}
      />
      {confirming && (
        <div role="alertdialog" aria-label="Stop sharing">
          <Text size="sm" mt="sm">
            <strong>Stop sharing “{name}”?</strong> It disappears for everyone else.
          </Text>
          <Group justify="flex-end" gap="xs" mt="xs">
            <Button variant="default" size="xs" onClick={() => setConfirming(false)}>
              Keep sharing
            </Button>
            <Button
              color="red"
              size="xs"
              onClick={() => {
                setConfirming(false)
                onChange(false)
              }}
            >
              Stop sharing
            </Button>
          </Group>
        </div>
      )}
    </div>
  )
}
