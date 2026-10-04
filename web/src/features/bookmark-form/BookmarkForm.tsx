import { Alert, Button, Group, Stack, Text, TextInput, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { useState } from 'react'

import type { Bookmark, DashboardCategory } from '../../api/client.ts'
import { useCreateBookmark, useDeleteBookmark, useUpdateBookmark } from '../../api/mutations.ts'
import { categoryFields, NEW_CATEGORY, uncategorizedId } from './categoryChoice.ts'
import { CategoryField } from './CategoryField.tsx'
import { IconField } from './IconField.tsx'
import { isHttpUrl } from './tags.ts'
import { TagsField } from './TagsField.tsx'

interface Props {
  categories: readonly DashboardCategory[]
  /** Editing this manual bookmark; absent when adding. */
  bookmark?: Bookmark
  /** Pre-fills Name when adding from "Nothing matches" or the bookmarklet. */
  initialName?: string
  /** Pre-fills URL when adding from the bookmarklet. */
  initialUrl?: string
  /** After a save or delete; also Cancel, unless {@link onCancel} is given. */
  onDone: () => void
  onCancel?: () => void
}

/** Add, or edit a manual bookmark: Name, Category, URL, Tags, Icon. Edit adds Delete. */
export function BookmarkForm({
  categories,
  bookmark,
  initialName,
  initialUrl,
  onDone,
  onCancel = onDone,
}: Props) {
  const create = useCreateBookmark()
  const update = useUpdateBookmark()
  const remove = useDeleteBookmark()
  const [confirmDelete, setConfirmDelete] = useState(false)

  const form = useForm({
    initialValues: {
      name: bookmark?.name ?? initialName ?? '',
      category: String(bookmark?.categoryId ?? uncategorizedId(categories)),
      newCategory: '',
      url: bookmark?.url ?? initialUrl ?? '',
      tags: bookmark?.tags ?? [],
      icon: bookmark?.icon ?? '',
    },
    validate: {
      name: (v) => (v.trim() ? null : 'Name is required'),
      url: (v) => (isHttpUrl(v) ? null : 'Enter a full http:// or https:// address'),
      newCategory: (v, values) =>
        values.category === NEW_CATEGORY && !v.trim() ? 'Name the new category' : null,
    },
  })

  const saving = create.isPending || update.isPending || remove.isPending
  const error = create.error ?? update.error ?? remove.error

  // Failures land in the mutation's error, shown above the fields; only success closes the form.
  const submit = form.onSubmit((values) => {
    const body = {
      name: values.name.trim(),
      url: values.url.trim(),
      icon: values.icon.trim() || null,
      tags: values.tags,
      ...categoryFields(values.category, values.newCategory),
    }
    if (bookmark) {
      update.mutate({ id: bookmark.id, body }, { onSuccess: onDone })
    } else {
      create.mutate(body, { onSuccess: onDone })
    }
  })

  return (
    <form onSubmit={submit} noValidate>
      <Stack gap="md">
        <Title order={2} size="h4" id="bookmark-form-title">
          {bookmark ? 'Edit bookmark' : 'Add bookmark'}
        </Title>
        {error && (
          <Alert color="red" variant="light" p="xs">
            {error.message}
          </Alert>
        )}
        <TextInput label="Name" data-autofocus {...form.getInputProps('name')} />
        <CategoryField
          categories={categories}
          value={form.values.category}
          onChange={(v) => form.setFieldValue('category', v)}
          newName={form.values.newCategory}
          onNewNameChange={(v) => form.setFieldValue('newCategory', v)}
          newNameError={form.errors.newCategory as string | undefined}
        />
        <TextInput
          label="URL"
          type="url"
          ff="monospace"
          placeholder="https://"
          {...form.getInputProps('url')}
        />
        <TagsField value={form.values.tags} onChange={(tags) => form.setFieldValue('tags', tags)} />
        <IconField
          value={form.values.icon}
          onChange={(v) => form.setFieldValue('icon', v)}
          url={form.values.url}
          name={form.values.name}
        />

        {confirmDelete && bookmark ? (
          <Alert color="red" variant="light" p="sm">
            <Text size="sm" fw={500}>
              Delete “{bookmark.name}”?
            </Text>
            <Group justify="flex-end" gap="xs" mt="xs">
              <Button variant="default" size="xs" onClick={() => setConfirmDelete(false)}>
                Keep
              </Button>
              <Button
                color="red"
                size="xs"
                loading={remove.isPending}
                onClick={() => remove.mutate(bookmark.id, { onSuccess: onDone })}
              >
                Delete
              </Button>
            </Group>
          </Alert>
        ) : (
          <Group gap="xs" justify="flex-end">
            {bookmark && (
              <Button
                variant="subtle"
                color="red"
                mr="auto"
                onClick={() => setConfirmDelete(true)}
                disabled={saving}
              >
                Delete
              </Button>
            )}
            <Button variant="default" onClick={onCancel} disabled={saving}>
              Cancel
            </Button>
            <Button type="submit" loading={create.isPending || update.isPending}>
              {bookmark ? 'Save' : 'Add bookmark'}
            </Button>
          </Group>
        )}
      </Stack>
    </form>
  )
}
