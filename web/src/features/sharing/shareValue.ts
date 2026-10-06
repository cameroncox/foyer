import type { Bookmark } from '../../api/client.ts'

/** Who a bookmark is shared with, as the forms hold it. */
export interface ShareValue {
  shared: boolean
  everyone: boolean
  profileIds: number[]
}

/** The form's starting value: the bookmark's own sharing, or not shared. */
export function initialShare(bookmark?: Bookmark): ShareValue {
  return {
    shared: bookmark?.isShared ?? false,
    everyone: bookmark?.sharedWith?.everyone ?? false,
    profileIds: bookmark?.sharedWith?.profiles.map((p) => p.id) ?? [],
  }
}

/** The request fields for a share value. */
export function shareBody(value: ShareValue) {
  return {
    isShared: value.shared,
    shareWith: value.shared
      ? { everyone: value.everyone, profileIds: value.everyone ? [] : value.profileIds }
      : null,
  }
}

/** Why the form can't save: shared with chosen profiles, but none picked. */
export function shareError(value: ShareValue): string | null {
  return value.shared && !value.everyone && value.profileIds.length === 0
    ? 'Pick at least one profile, or turn sharing off.'
    : null
}
