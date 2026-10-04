import { useMutation, useQueryClient } from '@tanstack/react-query'

import type {
  CreateBookmarkRequest,
  Dashboard,
  ReorderBookmarksRequest,
  UpdateBookmarkRequest,
} from './client.ts'
import { api, unwrap } from './client.ts'
import { queryKeys } from './queries.ts'

/** Every write refetches the dashboard when it settles; SSE would too, this is just sooner. */
function useInvalidateDashboard() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboard })
}

export function useCreateBookmark() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async (body: CreateBookmarkRequest) =>
      unwrap(await api.POST('/api/bookmarks', { body })),
    onSettled,
  })
}

export function useUpdateBookmark() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async ({ id, body }: { id: number; body: UpdateBookmarkRequest }) =>
      unwrap(await api.PUT('/api/bookmarks/{id}', { params: { path: { id } }, body })),
    onSettled,
  })
}

export function useDeleteBookmark() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(await api.DELETE('/api/bookmarks/{id}', { params: { path: { id } } })),
    onSettled,
  })
}

export function useResetBookmark() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(await api.POST('/api/bookmarks/{id}/reset', { params: { path: { id } } })),
    onSettled,
  })
}

/**
 * Saves a drag. The page shows the new order at once (`optimistic`); if the server refuses it,
 * the old order comes back.
 */
export function useReorderBookmarks() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ body }: { body: ReorderBookmarksRequest; optimistic: Dashboard }) =>
      unwrap(await api.PUT('/api/bookmarks/order', { body })),
    onMutate: async ({ optimistic }) => applyOptimistic(queryClient, optimistic),
    onError: (_error, _vars, context) =>
      context && queryClient.setQueryData(queryKeys.dashboard, context.previous),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
  })
}

export function useCreateCategory() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async (name: string) =>
      unwrap(await api.POST('/api/categories', { body: { name } })),
    onSettled,
  })
}

export function useRenameCategory() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async ({ id, name }: { id: number; name: string }) =>
      unwrap(await api.PUT('/api/categories/{id}', { params: { path: { id } }, body: { name } })),
    onSettled,
  })
}

export function useDeleteCategory() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async (id: number) =>
      unwrap(await api.DELETE('/api/categories/{id}', { params: { path: { id } } })),
    onSettled,
  })
}

export function useReorderCategories() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ categoryIds }: { categoryIds: number[]; optimistic: Dashboard }) =>
      unwrap(await api.PUT('/api/categories/order', { body: { categoryIds } })),
    onMutate: async ({ optimistic }) => applyOptimistic(queryClient, optimistic),
    onError: (_error, _vars, context) =>
      context && queryClient.setQueryData(queryKeys.dashboard, context.previous),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
  })
}

export function usePreviewImport() {
  return useMutation({
    mutationFn: async (html: string) =>
      unwrap(await api.POST('/api/import/preview', { body: { html } })),
  })
}

export function useImport() {
  const onSettled = useInvalidateDashboard()
  return useMutation({
    mutationFn: async ({ html, folderIds }: { html: string; folderIds: string[] }) =>
      unwrap(await api.POST('/api/import', { body: { html, folderIds } })),
    onSettled,
  })
}

async function applyOptimistic(
  queryClient: ReturnType<typeof useQueryClient>,
  optimistic: Dashboard,
) {
  await queryClient.cancelQueries({ queryKey: queryKeys.dashboard })
  const previous = queryClient.getQueryData<Dashboard>(queryKeys.dashboard)
  queryClient.setQueryData(queryKeys.dashboard, optimistic)
  return { previous }
}
