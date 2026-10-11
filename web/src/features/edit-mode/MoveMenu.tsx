import { Button, Group, Menu, Modal, Stack, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { IconArrowForwardUp, IconPlus } from '@tabler/icons-react'
import { useState } from 'react'

import { useCreateProfile } from '../../api/mutations.ts'
import { groupProfiles } from '../profiles/groups.ts'
import { useCurrentProfile } from '../profiles/profileContext.ts'
import { validateName } from '../profiles/profileName.ts'

export interface MoveTarget {
  id: number
  name: string
}

interface Props {
  count: number
  moving: boolean
  onMove: (target: MoveTarget) => void
}

/**
 * Select mode's Move to: the other profiles the caller can edit, grouped as the picker groups
 * them, then New profile, which makes one and moves the bookmarks there. Nothing shows with
 * profiles off.
 */
export function MoveMenu({ count, moving, onMove }: Props) {
  const current = useCurrentProfile()
  const [creating, setCreating] = useState(false)
  if (!current?.me.profilesEnabled) {
    return null
  }

  const { me } = current
  const editable = me.profiles.filter((p) => p.canEdit && p.id !== me.current.id)
  const { home, yours, everyones } = groupProfiles(editable)
  const groups = [
    { label: 'Default', profiles: home ? [home] : [] },
    { label: 'Yours', profiles: yours },
    { label: 'Everyone’s', profiles: everyones },
  ].filter((g) => g.profiles.length > 0)

  return (
    <>
      <Menu position="bottom-end" withArrow shadow="md">
        <Menu.Target>
          <Button
            variant="default"
            size="xs"
            leftSection={<IconArrowForwardUp size={14} />}
            disabled={count === 0}
            loading={moving}
          >
            Move to
          </Button>
        </Menu.Target>
        <Menu.Dropdown>
          {groups.map((group) => (
            <div key={group.label}>
              <Menu.Label>{group.label}</Menu.Label>
              {group.profiles.map((profile) => (
                <Menu.Item key={profile.id} onClick={() => onMove(profile)}>
                  {profile.name}
                </Menu.Item>
              ))}
            </div>
          ))}
          {groups.length > 0 && <Menu.Divider />}
          <Menu.Item leftSection={<IconPlus size={14} />} onClick={() => setCreating(true)}>
            New profile…
          </Menu.Item>
        </Menu.Dropdown>
      </Menu>
      <NewProfileForMove
        opened={creating}
        count={count}
        onClose={() => setCreating(false)}
        onCreated={onMove}
      />
    </>
  )
}

interface NewProfileProps {
  opened: boolean
  count: number
  onClose: () => void
  onCreated: (target: MoveTarget) => void
}

function NewProfileForMove({ opened, count, onClose, onCreated }: NewProfileProps) {
  const create = useCreateProfile()
  const form = useForm({ initialValues: { name: '' }, validate: { name: validateName } })

  const close = () => {
    form.reset()
    create.reset()
    onClose()
  }

  return (
    <Modal opened={opened} onClose={close} title="Move to a new profile" centered>
      <form
        onSubmit={form.onSubmit(({ name }) =>
          create.mutate(name.trim(), {
            onSuccess: (created) => {
              close()
              onCreated(created)
            },
          }),
        )}
      >
        <Stack gap="md">
          <TextInput
            label="Name"
            description="Letters, digits and hyphens."
            data-autofocus
            {...form.getInputProps('name')}
            error={form.errors.name ?? create.error?.message}
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={close}>
              Cancel
            </Button>
            <Button type="submit" loading={create.isPending}>
              Create and move {count}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}
