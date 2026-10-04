import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'

import { queryKeys } from '../api/queries.ts'

/**
 * Follows /api/events and refetches the dashboard when it changes. EventSource reconnects on
 * its own; "connected" after a reconnect also refetches, since changes may have been missed.
 */
export function useLiveUpdates(url = '/api/events') {
  const queryClient = useQueryClient()

  useEffect(() => {
    const source = new EventSource(url)
    let connectedBefore = false
    const refetch = () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboard })

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
