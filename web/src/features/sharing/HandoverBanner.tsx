import { Button } from '@mantine/core'
import { notifications } from '@mantine/notifications'

import { useAcceptHandover, useDeclineHandover } from '../../api/mutations.ts'
import { useCurrentProfile } from '../profiles/profileContext.ts'
import classes from './Sharing.module.css'

/**
 * On a Default editor's own profile, once profiles are turned on over bookmarks made without
 * them: an offer to move Default's manual bookmarks here. Either answer settles it for everyone.
 */
export function HandoverBanner() {
  const current = useCurrentProfile()
  const accept = useAcceptHandover()
  const decline = useDeclineHandover()
  const me = current?.me
  if (!me?.profilesEnabled || me.current.kind !== 'personal' || me.handoverCount === 0) {
    return null
  }

  const count = me.handoverCount
  const busy = accept.isPending || decline.isPending
  const failed = (title: string) => (error: Error) =>
    notifications.show({ color: 'red', title, message: error.message })

  return (
    <div className={classes.banner} role="note">
      <span>
        <strong>
          Default has {count} {count === 1 ? 'bookmark' : 'bookmarks'} from before profiles.
        </strong>{' '}
        Move them here to make them yours. Docker bookmarks stay in Default, and shared ones still
        show there.
      </span>
      <span className={classes.bannerActions}>
        <Button
          variant="default"
          size="xs"
          disabled={busy}
          onClick={() => decline.mutate(undefined, { onError: failed('Couldn’t dismiss this') })}
        >
          Leave them in Default
        </Button>
        <Button
          size="xs"
          loading={accept.isPending}
          disabled={decline.isPending}
          onClick={() =>
            accept.mutate(undefined, {
              onSuccess: ({ moved }) =>
                notifications.show({
                  message: `Moved ${moved} ${moved === 1 ? 'bookmark' : 'bookmarks'} to ${me.current.name}`,
                }),
              onError: failed('Couldn’t move the bookmarks'),
            })
          }
        >
          Move them here
        </Button>
      </span>
    </div>
  )
}
