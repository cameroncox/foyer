import '@testing-library/jest-dom/vitest'

import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

import { FakeEventSource } from './fakes.ts'

beforeEach(() => {
  FakeEventSource.instances = []
  vi.stubGlobal('EventSource', FakeEventSource)
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

// jsdom lacks matchMedia and ResizeObserver, which Mantine reads.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})

class NoopResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
}

window.ResizeObserver ??= NoopResizeObserver as unknown as typeof ResizeObserver

// jsdom has no scrollIntoView; Mantine's dropdowns call it on the selected option.
Element.prototype.scrollIntoView ??= function scrollIntoView() {}
