import { createContext, use } from 'react'

import type { Me } from '../../api/client.ts'

export interface CurrentProfile {
  /** Sent with every API call; undefined with profiles off, so requests look as they did in 1.0. */
  slug: string | undefined
  me: Me
}

export const ProfileContext = createContext<CurrentProfile | null>(null)

/** The profile the page shows, or null outside one (component tests, before /api/me answers). */
export function useCurrentProfile() {
  return use(ProfileContext)
}

/** The slug to send as X-Foyer-Profile; undefined means the server's choice (Default, or the user's own). */
export function useProfileSlug() {
  return use(ProfileContext)?.slug
}

/** Whether this page may change the profile's bookmarks; true outside a profile. */
export function useCanEdit() {
  return use(ProfileContext)?.me.current.canEdit ?? true
}
