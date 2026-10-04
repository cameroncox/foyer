import { createTheme, type MantineColorShade, type MantineColorsTuple } from '@mantine/core'

import { ACCENTS, findAccent } from './accents.ts'

const colors = Object.fromEntries(ACCENTS.map((a) => [a.key, a.shades])) as Record<
  string,
  MantineColorsTuple
>

/** Foyer's Mantine theme with one accent as the primary color. */
export function createFoyerTheme(accentKey: string) {
  const accent = findAccent(accentKey)

  // The swatch picked is the base color; use the shade it landed on, one lighter in dark mode.
  const base = Math.max(accent.shades.indexOf(accent.base.toLowerCase()), 1) as MantineColorShade
  return createTheme({
    colors,
    primaryColor: accent.key,
    primaryShade: { light: base, dark: (base - 1) as MantineColorShade },
    autoContrast: true,
    fontFamily: "'IBM Plex Sans', system-ui, sans-serif",
    fontFamilyMonospace: "'IBM Plex Mono', ui-monospace, monospace",
    headings: { fontFamily: "'IBM Plex Sans', system-ui, sans-serif" },
    defaultRadius: 'md',
  })
}
