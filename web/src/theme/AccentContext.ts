import { createContext, useContext } from 'react'

import { DEFAULT_ACCENT } from './accents.ts'

export interface AccentState {
  accent: string
  setAccent: (key: string) => void
}

export const AccentContext = createContext<AccentState>({
  accent: DEFAULT_ACCENT,
  setAccent: () => {},
})

export function useAccent() {
  return useContext(AccentContext)
}
