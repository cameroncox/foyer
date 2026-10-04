import { Select, Stack, Text, TextInput } from '@mantine/core'

import type { DashboardCategory } from '../../api/client.ts'
import { categoryOptions, NEW_CATEGORY, uncategorizedId } from './categoryChoice.ts'

interface Props {
  categories: readonly DashboardCategory[]
  value: string
  onChange: (value: string) => void
  newName: string
  onNewNameChange: (name: string) => void
  newNameError?: string
}

/**
 * Searchable category select; "New category…" reveals a name field, and the category is created
 * on save. Clearing falls back to Uncategorized, so the clear button only shows for other values.
 */
export function CategoryField({
  categories,
  value,
  onChange,
  newName,
  onNewNameChange,
  newNameError,
}: Props) {
  const uncategorized = uncategorizedId(categories)
  return (
    <Stack gap={8}>
      <Select
        label="Category"
        data={categoryOptions(categories)}
        value={value}
        onChange={(v) => onChange(v ?? uncategorized)}
        allowDeselect={false}
        searchable
        // Typing replaces the current category's name rather than appending to it.
        onFocus={(e) => e.currentTarget.select()}
        nothingFoundMessage="No matching category"
        clearable={value !== uncategorized}
        clearButtonProps={{ 'aria-label': 'Clear category' }}
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
