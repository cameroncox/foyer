import { ActionIcon, Menu, Text, UnstyledButton } from '@mantine/core'
import { IconCheck, IconChevronDown, IconPencil, IconPlus } from '@tabler/icons-react'
import { useState } from 'react'

import type { Profile } from '../../api/client.ts'
import { groupProfiles } from './groups.ts'
import { useCurrentProfile } from './profileContext.ts'
import type { ProfileDialog } from './ProfileDialogs.tsx'
import { ProfileIcon } from './ProfileIcon.tsx'
import classes from './ProfilePicker.module.css'
import { usePickProfile } from './usePickProfile.ts'

interface Props {
  onDialog: (dialog: ProfileDialog) => void
}

/**
 * The current profile beside the instance name; open, it lists Default, the caller's profiles
 * and everyone's, each with a pencil to rename (and delete) it where allowed, then New profile.
 * Nothing shows with profiles off.
 */
export function ProfilePicker({ onDialog }: Props) {
  const current = useCurrentProfile()
  const pick = usePickProfile()
  const [opened, setOpened] = useState(false)
  if (!current?.me.profilesEnabled) {
    return null
  }

  const { me } = current
  const { home, yours, everyones } = groupProfiles(me.profiles)
  const open = (dialog: ProfileDialog) => {
    setOpened(false)
    onDialog(dialog)
  }

  const row = (profile: Profile, description?: string) => (
    <div key={profile.id} className={classes.menuRow}>
      <Menu.Item
        className={classes.menuItem}
        leftSection={<ProfileIcon kind={profile.kind} />}
        rightSection={
          profile.id === me.current.id ? (
            <IconCheck size={16} aria-label="Current profile" />
          ) : (
            <Text size="xs" c="dimmed" ff="monospace">
              /{profile.slug}
            </Text>
          )
        }
        onClick={() => pick(profile.slug)}
      >
        <Text size="sm" fw={profile.id === me.current.id ? 600 : 400}>
          {profile.name}
        </Text>
        {description && (
          <Text size="xs" c="dimmed">
            {description}
          </Text>
        )}
      </Menu.Item>
      {profile.canRename && (
        <ActionIcon
          variant="subtle"
          color="gray"
          size={36}
          aria-label={`Rename ${profile.name}`}
          onClick={() => open({ kind: 'rename', profile })}
        >
          <IconPencil size={16} />
        </ActionIcon>
      )}
    </div>
  )

  return (
    <Menu position="bottom-start" width={340} shadow="md" opened={opened} onChange={setOpened}>
      <Menu.Target>
        <UnstyledButton className={classes.target} aria-label={`Profile: ${me.current.name}`}>
          <ProfileIcon kind={me.current.kind} />
          <span className={classes.name}>{me.current.name}</span>
          <IconChevronDown size={16} aria-hidden="true" />
        </UnstyledButton>
      </Menu.Target>
      <Menu.Dropdown>
        {home &&
          row(
            home,
            `Home, with Docker bookmarks · ${home.canEdit ? 'you can edit' : 'read-only for you'}`,
          )}
        {yours.length > 0 && <Menu.Label>Yours</Menu.Label>}
        {yours.map((p) => row(p))}
        {everyones.length > 0 && <Menu.Label>Everyone’s</Menu.Label>}
        {everyones.map((p) => row(p))}
        <Menu.Divider />
        <Menu.Item leftSection={<IconPlus size={16} />} onClick={() => open({ kind: 'new' })}>
          New profile
        </Menu.Item>
      </Menu.Dropdown>
    </Menu>
  )
}
