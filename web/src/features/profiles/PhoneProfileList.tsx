import { Text, UnstyledButton } from '@mantine/core'
import { IconCheck, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react'

import { groupProfiles } from './groups.ts'
import { useCurrentProfile } from './profileContext.ts'
import type { ProfileDialog } from './ProfileDialogs.tsx'
import { ProfileIcon } from './ProfileIcon.tsx'
import classes from './ProfilePicker.module.css'
import { usePickProfile } from './usePickProfile.ts'

interface Props {
  /** Closes the menu before anything opens over it. */
  onDone: () => void
  onDialog: (dialog: ProfileDialog) => void
}

/** The profile picker at the top of the phone menu: every profile, then New profile and the current one's actions. */
export function PhoneProfileList({ onDone, onDialog }: Props) {
  const current = useCurrentProfile()
  const pick = usePickProfile()
  if (!current?.me.profilesEnabled) {
    return null
  }

  const { me } = current
  const { home, yours, everyones } = groupProfiles(me.profiles)
  const then = (action: () => void) => () => {
    onDone()
    action()
  }

  return (
    <nav className={classes.list} aria-label="Profiles">
      {[home, ...yours, ...everyones]
        .filter((p) => p !== undefined)
        .map((profile) => (
          <UnstyledButton
            key={profile.id}
            className={classes.row}
            data-current={profile.id === me.current.id || undefined}
            aria-current={profile.id === me.current.id ? 'page' : undefined}
            onClick={then(() => pick(profile.slug))}
          >
            <ProfileIcon kind={profile.kind} />
            <span className={classes.rowName}>{profile.name}</span>
            {profile.id === me.current.id ? (
              <IconCheck size={16} aria-hidden="true" />
            ) : (
              <Text size="xs" c="dimmed" ff="monospace">
                /{profile.slug}
              </Text>
            )}
          </UnstyledButton>
        ))}
      <UnstyledButton
        className={`${classes.row} ${classes.rowAction}`}
        onClick={then(() => onDialog({ kind: 'new' }))}
      >
        <IconPlus size={16} aria-hidden="true" />
        New profile
      </UnstyledButton>
      {me.current.canManage && (
        <>
          <UnstyledButton
            className={`${classes.row} ${classes.rowAction}`}
            onClick={then(() => onDialog({ kind: 'rename', profile: me.current }))}
          >
            <IconPencil size={16} aria-hidden="true" />
            Rename {me.current.name}
          </UnstyledButton>
          <UnstyledButton
            className={`${classes.row} ${classes.rowAction}`}
            c="red"
            onClick={then(() => onDialog({ kind: 'delete', profile: me.current }))}
          >
            <IconTrash size={16} aria-hidden="true" />
            Delete {me.current.name}
          </UnstyledButton>
        </>
      )}
    </nav>
  )
}
