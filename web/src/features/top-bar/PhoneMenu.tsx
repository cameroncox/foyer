import { ActionIcon, Button, Drawer, SimpleGrid, Stack } from '@mantine/core'
import { IconPencil, IconPlus, IconX } from '@tabler/icons-react'

import { PhoneProfileList } from '../profiles/PhoneProfileList.tsx'
import { useCanEdit } from '../profiles/profileContext.ts'
import type { ProfileDialog } from '../profiles/ProfileDialogs.tsx'
import { ThemePicker } from './ThemePicker.tsx'
import classes from './TopBar.module.css'

interface Props {
  title: string
  opened: boolean
  onClose: () => void
  onAdd: () => void
  onEdit: () => void
  onDialog: (dialog: ProfileDialog) => void
}

/**
 * The phone menu, dropping from the top: the profiles, Add bookmark and Edit page (when the
 * profile can be changed), then the theme.
 */
export function PhoneMenu({ title, opened, onClose, onAdd, onEdit, onDialog }: Props) {
  const canEdit = useCanEdit()
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
        <div className={classes.brand}>
          <span className={classes.title}>{title}</span>
        </div>
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
        <PhoneProfileList onDone={onClose} onDialog={onDialog} />
        {canEdit && (
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
        )}
        <ThemePicker />
      </Stack>
    </Drawer>
  )
}
