import { Menu, Text, UnstyledButton } from '@mantine/core'
import { IconCheck, IconChevronDown, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react'

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
 * and everyone's, then New profile, and Rename and Delete for the current one when allowed.
 * Amber on Default for those who can edit it, since changes there reach every profile's Docker
 * cards. Nothing shows with profiles off.
 */
export function ProfilePicker({ onDialog }: Props) {
  const current = useCurrentProfile()
  const pick = usePickProfile()
  if (!current?.me.profilesEnabled) {
    return null
  }

  const { me } = current
  const { home, yours, everyones } = groupProfiles(me.profiles)
  const item = (profile: Profile, description?: string) => (
    <Menu.Item
      key={profile.id}
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
  )

  return (
    <Menu position="bottom-start" width={320} shadow="md">
      <Menu.Target>
        <UnstyledButton
          className={classes.target}
          data-default-editor={(me.current.kind === 'default' && me.current.canEdit) || undefined}
          aria-label={`Profile: ${me.current.name}`}
        >
          <ProfileIcon kind={me.current.kind} />
          <span className={classes.name}>{me.current.name}</span>
          <IconChevronDown size={16} aria-hidden="true" />
        </UnstyledButton>
      </Menu.Target>
      <Menu.Dropdown>
        {home &&
          item(
            home,
            `Home, with Docker bookmarks · ${home.canEdit ? 'you can edit' : 'read-only for you'}`,
          )}
        {yours.length > 0 && <Menu.Label>Yours</Menu.Label>}
        {yours.map((p) => item(p))}
        {everyones.length > 0 && <Menu.Label>Everyone’s</Menu.Label>}
        {everyones.map((p) => item(p))}
        <Menu.Divider />
        <Menu.Item leftSection={<IconPlus size={16} />} onClick={() => onDialog({ kind: 'new' })}>
          New profile
        </Menu.Item>
        {me.current.canManage && (
          <>
            <Menu.Item
              leftSection={<IconPencil size={16} />}
              onClick={() => onDialog({ kind: 'rename', profile: me.current })}
            >
              Rename {me.current.name}…
            </Menu.Item>
            <Menu.Item
              color="red"
              leftSection={<IconTrash size={16} />}
              onClick={() => onDialog({ kind: 'delete', profile: me.current })}
            >
              Delete {me.current.name}…
            </Menu.Item>
          </>
        )}
      </Menu.Dropdown>
    </Menu>
  )
}
