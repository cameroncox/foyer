import { Button, CloseButton } from '@mantine/core'
import { useState } from 'react'

import { useCurrentProfile } from '../profiles/profileContext.ts'
import { usePickProfile } from '../profiles/usePickProfile.ts'
import { dismissDefaultBanner, isDefaultBannerDismissed } from './dismissedDefaultBanner.ts'
import classes from './Sharing.module.css'

/**
 * On Default, for those who can edit it: a reminder that its Docker bookmarks live here and only
 * shared ones reach other profiles, with a way back to the caller's own profile. Closed, it stays
 * closed on this device.
 */
export function DefaultBanner() {
  const current = useCurrentProfile()
  const pick = usePickProfile()
  const [dismissed, setDismissed] = useState(isDefaultBannerDismissed)
  const me = current?.me
  if (dismissed || !me?.profilesEnabled || me.current.kind !== 'default' || !me.current.canEdit) {
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
      <CloseButton
        className={personal ? undefined : classes.bannerAction}
        size="sm"
        aria-label="Dismiss"
        onClick={() => {
          dismissDefaultBanner()
          setDismissed(true)
        }}
      />
    </div>
  )
}
