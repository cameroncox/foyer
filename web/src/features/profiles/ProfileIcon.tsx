import { IconAppWindow, IconHome, IconUser } from '@tabler/icons-react'

import type { Profile } from '../../api/client.ts'

/** Home for Default, a person for the caller's own profiles, a window for shared ones. */
export function ProfileIcon({ kind, size = 16 }: { kind: Profile['kind']; size?: number }) {
  const Icon = kind === 'default' ? IconHome : kind === 'ownerless' ? IconAppWindow : IconUser
  return <Icon size={size} stroke={1.8} aria-hidden="true" />
}
