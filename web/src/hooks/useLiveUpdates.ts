import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

import { queryKeys } from '../api/queries.ts'
import { useProfileSlug } from '../features/profiles/profileContext.ts'

/**
 * Follows /api/events and refetches the dashboard when it changes. EventSource reconnects on
 * its own; "connected" after a reconnect also refetches, since changes may have been missed.
 * EventSource can't send headers, so the profile goes in the query string.
 */
export function useLiveUpdates() {
  const queryClient = useQueryClient()
  const profile = useProfileSlug()
  const url = profile ? `/api/events?profile=${encodeURIComponent(profile)}` : '/api/events'

  useEffect(() => {
    const source = new EventSource(url)
    let connectedBefore = false
    const refetch = () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboards })

    const onConnected = () => {
      if (connectedBefore) {
        void refetch()
      }

      connectedBefore = true
    }

    source.addEventListener('connected', onConnected)
    source.addEventListener('bookmarks-changed', refetch)
    return () => source.close()
  }, [queryClient, url])
}
