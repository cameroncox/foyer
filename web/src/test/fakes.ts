import { vi } from 'vitest'

import type { Bookmark, Dashboard, DashboardCategory, Me, Profile } from '../api/client.ts'

/** Stand-in for EventSource (jsdom has none). Tests fire events on the latest instance. */
export class FakeEventSource {
  static instances: FakeEventSource[] = []
  readonly url: string
  closed = false
  private listeners = new Map<string, Set<() => void>>()

  constructor(url: string) {
    this.url = url
    FakeEventSource.instances.push(this)
  }

  static get latest() {
    return FakeEventSource.instances.at(-1)!
  }

  addEventListener(type: string, listener: () => void) {
    if (!this.listeners.has(type)) {
      this.listeners.set(type, new Set())
    }

    this.listeners.get(type)!.add(listener)
  }

  removeEventListener(type: string, listener: () => void) {
    this.listeners.get(type)?.delete(listener)
  }

  emit(type: string) {
    this.listeners.get(type)?.forEach((l) => l())
  }

  close() {
    this.closed = true
  }
}

let nextId = 100

export function bookmark(name: string, extra: Partial<Bookmark> = {}): Bookmark {
  return {
    id: nextId++,
    source: 'manual',
    name,
    url: `https://${name.toLowerCase().replace(/\s+/g, '')}.lan`,
    icon: null,
    iconUrl: null,
    categoryId: 1,
    tags: [],
    hostTag: null,
    status: null,
    docker: null,
    isShared: false,
    canEdit: true,
    sharedBy: null,
    sharedFrom: null,
    sharedWith: null,
    ...extra,
  }
}

export function dockerBookmark(
  name: string,
  status: 'running' | 'warning' | 'stopped',
  extra: Partial<Bookmark> = {},
): Bookmark {
  return bookmark(name, {
    source: 'docker',
    hostTag: 'docker-4',
    status,
    docker: {
      host: 'docker-4',
      containerName: name.toLowerCase(),
      state: status === 'stopped' ? 'exited' : 'running',
      health: status === 'warning' ? 'unhealthy' : 'none',
      labelCategory: null,
      labelTags: [],
      categoryOverridden: false,
      tagsOverridden: false,
    },
    ...extra,
  })
}

export function category(name: string, bookmarks: Bookmark[], isSystem = false): DashboardCategory {
  return { id: nextId++, name, isSystem, bookmarks }
}

export function profile(name: string, extra: Partial<Profile> = {}): Profile {
  return {
    id: nextId++,
    name,
    slug: name.toLowerCase(),
    kind: 'owned',
    canEdit: true,
    canRename: true,
    canDelete: true,
    showsDockerBookmarks: false,
    canShowDockerBookmarks: false,
    ...extra,
  }
}

export const defaultProfile = (extra: Partial<Profile> = {}) =>
  profile('Default', {
    slug: 'default',
    kind: 'default',
    canRename: false,
    canDelete: false,
    ...extra,
  })

/** /api/me with profiles off, as a 1.0 install answers: Default only, editable. */
export function profilesOff(): Me {
  const home = defaultProfile({ id: 1 })
  return {
    profilesEnabled: false,
    user: null,
    current: home,
    canEditDefault: true,
    profiles: [home],
    canShareWithEveryone: true,
    handoverCount: 0,
  }
}

/**
 * Stubs fetch so /api/dashboard answers with whatever `current()` returns at call time, and
 * /api/me with profiles off.
 */
export function stubDashboard(current: () => Dashboard | Response) {
  const fetch = vi.fn(async (request: Request) => {
    const path = new URL(request.url).pathname
    if (path === '/api/me') {
      return new Response(JSON.stringify(profilesOff()), {
        headers: { 'Content-Type': 'application/json' },
      })
    }

    if (path !== '/api/dashboard') {
      return new Response(null, { status: 404 })
    }

    const value = current()
    return value instanceof Response
      ? value
      : new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
