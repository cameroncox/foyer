import { ActionIcon, Popover } from '@mantine/core'
import { IconPalette } from '@tabler/icons-react'

import { ThemePicker } from './ThemePicker.tsx'

/** The palette button and its popover holding the theme picker. */
export function ThemeMenu() {
  return (
    <Popover position="bottom-end" shadow="md" withArrow>
      <Popover.Target>
        <ActionIcon variant="default" size={44} radius="md" aria-label="Theme and accent color">
          <IconPalette size={20} stroke={1.8} />
        </ActionIcon>
      </Popover.Target>
      <Popover.Dropdown>
        <ThemePicker />
      </Popover.Dropdown>
    </Popover>
  )
}
