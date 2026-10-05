import type { Profile } from '../../api/client.ts'

export interface ProfileGroups {
  home: Profile | undefined
  /** The caller's personal profile, then their others. */
  yours: Profile[]
  /** Ownerless profiles everyone shares. */
  everyones: Profile[]
}

/** /api/me's list, as the picker groups it. The server already sorts within each kind. */
export function groupProfiles(profiles: readonly Profile[]): ProfileGroups {
  return {
    home: profiles.find((p) => p.kind === 'default'),
    yours: profiles.filter((p) => p.kind === 'personal' || p.kind === 'owned'),
    everyones: profiles.filter((p) => p.kind === 'ownerless'),
  }
}
