import { useQuery } from '@tanstack/react-query'

import { api, unwrap } from './client.ts'

export const queryKeys = {
  dashboard: ['dashboard'] as const,
}

export function useDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: async ({ signal }) => unwrap(await api.GET('/api/dashboard', { signal })),
  })
}
