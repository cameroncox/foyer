import { Button, Group, Modal, Stack, Text, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { useNavigate } from 'react-router'

import type { Profile } from '../../api/client.ts'
import { useCreateProfile, useDeleteProfile, useRenameProfile } from '../../api/mutations.ts'
import { useCurrentProfile } from './profileContext.ts'
import { forgetProfile, loadRememberedProfile, rememberProfile } from './rememberedProfile.ts'
import { usePickProfile } from './usePickProfile.ts'

export type ProfileDialog =
  | { kind: 'new' }
  | { kind: 'rename'; profile: Profile }
  | { kind: 'delete'; profile: Profile }
  | null

interface Props {
  dialog: ProfileDialog
  onDialog: (dialog: ProfileDialog) => void
}

/**
 * New profile, Rename and Delete, opened from the picker or the phone menu. Delete is reached
 * from Rename, for profiles that can be deleted.
 */
export function ProfileDialogs({ dialog, onDialog }: Props) {
  const onClose = () => onDialog(null)
  return (
    <>
      <NewProfileModal opened={dialog?.kind === 'new'} onClose={onClose} />
      {dialog?.kind === 'rename' && (
        <RenameProfileModal
          key={dialog.profile.id}
          profile={dialog.profile}
          onClose={onClose}
          onDelete={() => onDialog({ kind: 'delete', profile: dialog.profile })}
        />
      )}
      {dialog?.kind === 'delete' && (
        <DeleteProfileModal profile={dialog.profile} onClose={onClose} />
      )}
    </>
  )
}

/** Letters, digits and hyphens, as the server checks. */
const NAME = /^[A-Za-z0-9-]+$/

function validateName(value: string) {
  const name = value.trim()
  if (!name) {
    return 'Give it a name.'
  }

  return NAME.test(name) ? null : 'Use letters, digits and hyphens only.'
}

function NewProfileModal({ opened, onClose }: { opened: boolean; onClose: () => void }) {
  const me = useCurrentProfile()?.me
  const create = useCreateProfile()
  const pick = usePickProfile()
  const form = useForm({ initialValues: { name: '' }, validate: { name: validateName } })
  const slug = form.values.name.trim().toLowerCase()

  const close = () => {
    form.reset()
    create.reset()
    onClose()
  }

  return (
    <Modal opened={opened} onClose={close} title="New profile" centered>
      <form
        onSubmit={form.onSubmit(({ name }) =>
          create.mutate(name.trim(), {
            onSuccess: (created) => {
              close()
              pick(created.slug)
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
          <Stack gap={4} p="sm" bg="var(--mantine-color-default-hover)" style={{ borderRadius: 8 }}>
            <Text size="xs" c="dimmed">
              Opens at
            </Text>
            <Text size="sm" ff="monospace">
              {window.location.host}/{slug || 'name'}
            </Text>
            <Text size="xs" c="dimmed">
              {me?.user ? 'Only you can see it.' : 'Everyone can see and edit it.'} It starts empty;
              Docker containers stay in Default.
            </Text>
          </Stack>
          <Group justify="flex-end">
            <Button variant="default" onClick={close}>
              Cancel
            </Button>
            <Button type="submit" loading={create.isPending}>
              Create and open
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}

function RenameProfileModal({
  profile,
  onClose,
  onDelete,
}: {
  profile: Profile
  onClose: () => void
  onDelete: () => void
}) {
  const current = useCurrentProfile()
  const rename = useRenameProfile()
  const navigate = useNavigate()
  const form = useForm({ initialValues: { name: profile.name }, validate: { name: validateName } })

  return (
    <Modal opened onClose={onClose} title={`Rename ${profile.name}`} centered>
      <form
        onSubmit={form.onSubmit(({ name }) =>
          rename.mutate(
            { id: profile.id, name: name.trim() },
            {
              onSuccess: (renamed) => {
                onClose()
                // Its URL moved with it.
                if (loadRememberedProfile() === profile.slug) {
                  rememberProfile(renamed.slug)
                }

                if (current?.me.current.id === profile.id) {
                  void navigate(`/${renamed.slug}`, { replace: true })
                }
              },
            },
          ),
        )}
      >
        <Stack gap="md">
          <TextInput
            label="Name"
            description="Letters, digits and hyphens. Its address changes too."
            data-autofocus
            {...form.getInputProps('name')}
            error={form.errors.name ?? rename.error?.message}
          />
          <Group justify="flex-end">
            {profile.canDelete && (
              <Button variant="subtle" color="red" mr="auto" onClick={onDelete}>
                Delete profile…
              </Button>
            )}
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={rename.isPending}>
              Save
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  )
}

function DeleteProfileModal({ profile, onClose }: { profile: Profile; onClose: () => void }) {
  const current = useCurrentProfile()
  const remove = useDeleteProfile()
  const navigate = useNavigate()

  const confirm = () =>
    remove.mutate(profile.id, {
      onSuccess: () => {
        onClose()
        notifications.show({ message: `Deleted ${profile.name}` })
        if (loadRememberedProfile() === profile.slug) {
          forgetProfile()
        }

        if (current?.me.current.id === profile.id) {
          void navigate('/', { replace: true })
        }
      },
      onError: (error) =>
        notifications.show({
          color: 'red',
          title: 'Couldn’t delete the profile',
          message: error.message,
        }),
    })

  return (
    <Modal opened onClose={onClose} title={`Delete ${profile.name}?`} centered>
      <Stack gap="md">
        <Text size="sm">
          Its bookmarks and categories are deleted too
          {profile.kind === 'ownerless' ? ', for everyone who uses it' : ''}. This can’t be undone.
        </Text>
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button color="red" loading={remove.isPending} onClick={confirm}>
            Delete profile
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
