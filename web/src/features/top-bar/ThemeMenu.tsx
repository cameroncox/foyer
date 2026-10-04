import {
  ActionIcon,
  CheckIcon,
  ColorSwatch,
  Popover,
  SegmentedControl,
  SimpleGrid,
  Stack,
  Tooltip,
  useMantineColorScheme,
  type MantineColorScheme,
} from '@mantine/core'
import { IconPalette } from '@tabler/icons-react'

import { ACCENTS } from '../../theme/accents.ts'
import { useAccent } from '../../theme/AccentContext.ts'

const SCHEMES = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
  { value: 'auto', label: 'Auto' },
]

/** Light / Dark / Auto on top, the 26 accents below, seven to a row. Both stay on this device. */
export function ThemeMenu() {
  const { colorScheme, setColorScheme } = useMantineColorScheme()
  const { accent, setAccent } = useAccent()

  return (
    <Popover position="bottom-end" shadow="md" withArrow>
      <Popover.Target>
        <ActionIcon variant="default" size={44} radius="md" aria-label="Theme and accent color">
          <IconPalette size={20} stroke={1.8} />
        </ActionIcon>
      </Popover.Target>
      <Popover.Dropdown>
        <Stack gap="md">
          <SegmentedControl
            fullWidth
            data={SCHEMES}
            value={colorScheme}
            onChange={(value) => setColorScheme(value as MantineColorScheme)}
            aria-label="Color scheme"
          />
          <SimpleGrid cols={7} spacing={8} role="radiogroup" aria-label="Accent color">
            {ACCENTS.map((a) => (
              <Tooltip key={a.key} label={a.name} openDelay={200}>
                <ColorSwatch
                  component="button"
                  type="button"
                  color={a.base}
                  size={28}
                  role="radio"
                  aria-checked={a.key === accent}
                  aria-label={a.name}
                  onClick={() => setAccent(a.key)}
                  style={{ color: '#fff', cursor: 'pointer' }}
                >
                  {a.key === accent && <CheckIcon size={12} />}
                </ColorSwatch>
              </Tooltip>
            ))}
          </SimpleGrid>
        </Stack>
      </Popover.Dropdown>
    </Popover>
  )
}
