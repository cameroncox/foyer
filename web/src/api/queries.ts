import { keepPreviousData, useQuery } from '@tanstack/react-query'

import { useProfileSlug } from '../features/profiles/profileContext.ts'
import { api, profileHeaders, unwrap } from './client.ts'

export const queryKeys = {
  /** Every profile's dashboard; invalidating this refetches whichever is showing. */
  dashboards: ['dashboard'] as const,
  dashboard: (profile: string | undefined) => ['dashboard', profile ?? null] as const,
  /** Every /api/me answer, whichever profile it was asked about. */
  me: ['me'] as const,
  meFor: (profile: string | undefined) => ['me', profile ?? null] as const,
  settings: ['settings'] as const,
  /** Every profile's share targets; profile changes refetch them. */
  shareTargets: ['share-targets'] as const,
  shareTargetsFor: (profile: string | undefined) => ['share-targets', profile ?? null] as const,
}

export function useDashboard() {
  const profile = useProfileSlug()
  return useQuery({
    queryKey: queryKeys.dashboard(profile),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET('/api/dashboard', { signal, headers: profileHeaders(profile) })),
  })
}

/**
 * Who's asking and which profile `profile` resolves to (undefined: Default, or the user's own).
 * The previous answer stays up while switching, so the page doesn't blank between profiles.
 */
export function useMe(profile: string | undefined) {
  return useQuery({
    queryKey: queryKeys.meFor(profile),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET('/api/me', { signal, headers: profileHeaders(profile) })),
    placeholderData: keepPreviousData,
    retry: false,
  })
}

/** The profiles a bookmark on this page can be shared with; asked for once the form needs them. */
export function useShareTargets(enabled: boolean) {
  const profile = useProfileSlug()
  return useQuery({
    queryKey: queryKeys.shareTargetsFor(profile),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET('/api/share-targets', { signal, headers: profileHeaders(profile) })),
    enabled,
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
