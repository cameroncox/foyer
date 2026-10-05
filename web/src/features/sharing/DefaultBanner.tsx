import { Button } from '@mantine/core'

import { useCurrentProfile } from '../profiles/profileContext.ts'
import { usePickProfile } from '../profiles/usePickProfile.ts'
import classes from './Sharing.module.css'

/**
 * On Default, for those who can edit it: a reminder that its Docker bookmarks live here and only
 * shared ones reach other profiles, with a way back to the caller's own profile.
 */
export function DefaultBanner() {
  const current = useCurrentProfile()
  const pick = usePickProfile()
  const me = current?.me
  if (!me?.profilesEnabled || me.current.kind !== 'default' || !me.current.canEdit) {
    return null
  }

  const personal = me.profiles.find((p) => p.kind === 'personal')
  return (
    <div className={classes.banner} role="note">
      <span>
        <strong>You’re editing Default.</strong> Docker bookmarks live here. Only bookmarks marked
        shared show in other profiles.
      </span>
      {personal && (
        <Button
          className={classes.bannerAction}
          variant="default"
          size="xs"
          onClick={() => pick(personal.slug)}
        >
          Back to {personal.name}
        </Button>
      )}
    </div>
  )
}
