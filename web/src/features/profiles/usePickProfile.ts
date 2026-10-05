import { useNavigate } from 'react-router'

import { rememberProfile } from './rememberedProfile.ts'

/** Opens a profile at its URL and remembers it for this device, so `/` reopens it. */
export function usePickProfile() {
  const navigate = useNavigate()
  return (slug: string) => {
    rememberProfile(slug)
    void navigate(`/${slug}`)
  }
}
