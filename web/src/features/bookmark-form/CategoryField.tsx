import { Select, Stack, Text, TextInput } from '@mantine/core'

import type { DashboardCategory } from '../../api/client.ts'
import { categoryOptions, NEW_CATEGORY } from './categoryChoice.ts'

interface Props {
  categories: readonly DashboardCategory[]
  value: string
  onChange: (value: string) => void
  newName: string
  onNewNameChange: (name: string) => void
  newNameError?: string
}

/** Category select; "New category…" reveals a name field, and the category is created on save. */
export function CategoryField({
  categories,
  value,
  onChange,
  newName,
  onNewNameChange,
  newNameError,
}: Props) {
  return (
    <Stack gap={8}>
      <Select
        label="Category"
        data={categoryOptions(categories)}
        value={value}
        onChange={(v) => v && onChange(v)}
        allowDeselect={false}
        comboboxProps={{ withinPortal: false }}
      />
      {value === NEW_CATEGORY && (
        <>
          <TextInput
            aria-label="New category name"
            placeholder="New category name"
            value={newName}
            onChange={(e) => onNewNameChange(e.currentTarget.value)}
            error={newNameError}
            data-autofocus
          />
          <Text size="xs" c="dimmed">
            Created when you save, at the end of the category list.
          </Text>
        </>
      )}
    </Stack>
  )
}
