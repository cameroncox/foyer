import { ActionIcon, Button, Drawer, SimpleGrid, Stack, TextInput } from '@mantine/core'
import { IconPencil, IconPlus, IconSearch, IconX } from '@tabler/icons-react'

import { ThemePicker } from './ThemePicker.tsx'
import classes from './TopBar.module.css'

interface Props {
  title: string
  opened: boolean
  onClose: () => void
  query: string
  onQueryChange: (query: string) => void
  onAdd: () => void
  onEdit: () => void
}

/** The phone menu, dropping from the top: search, Add bookmark and Edit page, then the theme. */
export function PhoneMenu({ title, opened, onClose, query, onQueryChange, onAdd, onEdit }: Props) {
  const then = (action: () => void) => () => {
    onClose()
    action()
  }

  return (
    <Drawer
      opened={opened}
      onClose={onClose}
      position="top"
      size="auto"
      padding={0}
      withCloseButton={false}
      aria-label="Menu"
      // Mantine gives a top drawer a fixed height even at size="auto"; let it fit the content.
      styles={{ content: { height: 'auto', borderRadius: '0 0 16px 16px' } }}
      classNames={{ content: classes.menu }}
    >
      <div className={classes.menuHeader}>
        <div className={classes.brand}>{title}</div>
        <ActionIcon
          variant="light"
          color="gray"
          size={44}
          radius="md"
          aria-label="Close menu"
          onClick={onClose}
        >
          <IconX size={22} />
        </ActionIcon>
      </div>
      <Stack gap={14} p={16}>
        <TextInput
          type="search"
          size="md"
          placeholder="Search bookmarks"
          aria-label="Search bookmarks"
          value={query}
          onChange={(e) => onQueryChange(e.currentTarget.value)}
          // Enter closes the menu so the results underneath can be seen.
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              onClose()
            }
          }}
          leftSection={<IconSearch size={18} stroke={2} aria-hidden="true" />}
        />
        <SimpleGrid cols={2} spacing={10}>
          <Button size="md" px="sm" leftSection={<IconPlus size={18} />} onClick={then(onAdd)}>
            Add bookmark
          </Button>
          <Button
            size="md"
            px="sm"
            variant="default"
            leftSection={<IconPencil size={16} />}
            onClick={then(onEdit)}
          >
            Edit page
          </Button>
        </SimpleGrid>
        <ThemePicker />
      </Stack>
    </Drawer>
  )
}
