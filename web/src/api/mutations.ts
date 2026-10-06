import { useMutation, useQueryClient } from '@tanstack/react-query'

import { useProfileSlug } from '../features/profiles/profileContext.ts'
import type {
  CreateBookmarkRequest,
  Dashboard,
  ReorderBookmarksRequest,
  UpdateBookmarkRequest,
  UpdateProfileRequest,
} from './client.ts'
import { api, profileHeaders, unwrap } from './client.ts'
import { queryKeys } from './queries.ts'

/** Every write refetches the dashboard when it settles; SSE would too, this is just sooner. */
function useInvalidateDashboard() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboards })
}

/** Headers naming the profile the page is on, so every write lands there. */
function useProfileHeaders() {
  return profileHeaders(useProfileSlug())
}

export function useCreateBookmark() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (body: CreateBookmarkRequest) =>
      unwrap(await api.POST('/api/bookmarks', { body, headers })),
    onSettled,
  })
}

export function useUpdateBookmark() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async ({ id, body }: { id: number; body: UpdateBookmarkRequest }) =>
      unwrap(await api.PUT('/api/bookmarks/{id}', { params: { path: { id } }, body, headers })),
    onSettled,
  })
}

export function useDeleteBookmark() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(await api.DELETE('/api/bookmarks/{id}', { params: { path: { id } }, headers })),
    onSettled,
  })
}

export function useDeleteBookmarks() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (bookmarkIds: number[]) =>
      unwrap(await api.POST('/api/bookmarks/delete', { body: { bookmarkIds }, headers })),
    onSettled,
  })
}

export function useResetBookmark() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(await api.POST('/api/bookmarks/{id}/reset', { params: { path: { id } }, headers })),
    onSettled,
  })
}

/**
 * Saves a drag. The page shows the new order at once (`optimistic`); if the server refuses it,
 * the old order comes back.
 */
export function useReorderBookmarks() {
  const queryClient = useQueryClient()
  const profile = useProfileSlug()
  const headers = profileHeaders(profile)
  return useMutation({
    mutationFn: async ({ body }: { body: ReorderBookmarksRequest; optimistic: Dashboard }) =>
      unwrap(await api.PUT('/api/bookmarks/order', { body, headers })),
    onMutate: async ({ optimistic }) => applyOptimistic(queryClient, profile, optimistic),
    onError: (_error, _vars, context) =>
      context && queryClient.setQueryData(queryKeys.dashboard(profile), context.previous),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboards }),
  })
}

export function useCreateCategory() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (name: string) =>
      unwrap(await api.POST('/api/categories', { body: { name }, headers })),
    onSettled,
  })
}

export function useRenameCategory() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async ({ id, name }: { id: number; name: string }) =>
      unwrap(
        await api.PUT('/api/categories/{id}', {
          params: { path: { id } },
          body: { name },
          headers,
        }),
      ),
    onSettled,
  })
}

export function useDeleteCategory() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(await api.DELETE('/api/categories/{id}', { params: { path: { id } }, headers })),
    onSettled,
  })
}

export function useReorderCategories() {
  const queryClient = useQueryClient()
  const profile = useProfileSlug()
  const headers = profileHeaders(profile)
  return useMutation({
    mutationFn: async ({ categoryIds }: { categoryIds: number[]; optimistic: Dashboard }) =>
      unwrap(await api.PUT('/api/categories/order', { body: { categoryIds }, headers })),
    onMutate: async ({ optimistic }) => applyOptimistic(queryClient, profile, optimistic),
    onError: (_error, _vars, context) =>
      context && queryClient.setQueryData(queryKeys.dashboard(profile), context.previous),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboards }),
  })
}

export function usePreviewImport() {
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (html: string) =>
      unwrap(await api.POST('/api/import/preview', { body: { html }, headers })),
  })
}

export function useImport() {
  const onSettled = useInvalidateDashboard()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async ({ html, folderIds }: { html: string; folderIds: string[] }) =>
      unwrap(await api.POST('/api/import', { body: { html, folderIds }, headers })),
    onSettled,
  })
}

/** Profile changes refetch /api/me, which lists them. */
function useInvalidateMe() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: queryKeys.me })
}

export function useCreateProfile() {
  const onSuccess = useInvalidateMe()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (name: string) =>
      unwrap(await api.POST('/api/profiles', { body: { name }, headers })),
    onSuccess,
  })
}

/** Renames a profile, or turns Show Docker bookmarks on or off; either changes what pages show. */
export function useUpdateProfile() {
  const queryClient = useQueryClient()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async ({ id, body }: { id: number; body: UpdateProfileRequest }) =>
      unwrap(await api.PUT('/api/profiles/{id}', { params: { path: { id } }, body, headers })),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.me }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboards }),
      ]),
  })
}

export function useDeleteProfile() {
  const onSuccess = useInvalidateMe()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(await api.DELETE('/api/profiles/{id}', { params: { path: { id } }, headers })),
    onSuccess,
  })
}

/** Moves Default's manual bookmarks to the caller's personal profile, once. */
export function useAcceptHandover() {
  const queryClient = useQueryClient()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async () => unwrap(await api.POST('/api/me/handover', { headers })),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.me }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboards }),
      ]),
  })
}

/** Leaves Default's bookmarks where they are, and stops the offer. */
export function useDeclineHandover() {
  const onSettled = useInvalidateMe()
  const headers = useProfileHeaders()
  return useMutation({
    mutationFn: async () => unwrap(await api.DELETE('/api/me/handover', { headers })),
    onSettled,
  })
}

async function applyOptimistic(
  queryClient: ReturnType<typeof useQueryClient>,
  profile: string | undefined,
  optimistic: Dashboard,
) {
  const key = queryKeys.dashboard(profile)
  await queryClient.cancelQueries({ queryKey: key })
  const previous = queryClient.getQueryData<Dashboard>(key)
  queryClient.setQueryData(key, optimistic)
  return { previous }
}
