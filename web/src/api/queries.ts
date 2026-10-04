import { useQuery } from '@tanstack/react-query'

import { api, unwrap } from './client.ts'

export const queryKeys = {
  dashboard: ['dashboard'] as const,
  settings: ['settings'] as const,
}

export function useDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/dashboard', { signal })),
  })
}

/** FOYER_* settings the page shows; they only change with a restart, so fetched once. */
export function useSettings() {
  return useQuery({
    queryKey: queryKeys.settings,
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/settings', { signal })),
    staleTime: Infinity,
  })
}
