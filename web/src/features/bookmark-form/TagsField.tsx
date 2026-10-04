import { TagsInput } from '@mantine/core'

import { normalizeTags } from './tags.ts'

interface Props {
  value: string[]
  onChange: (tags: string[]) => void
  /** Shown before the input for Docker bookmarks: the automatic host tag, which can't be removed. */
  hostTag?: string | null
}

export function TagsField({ value, onChange, hostTag }: Props) {
  return (
    <TagsInput
      label="Tags"
      description={hostTag ? `#${hostTag} is added automatically from the host.` : undefined}
      placeholder="Add tag"
      value={value}
      onChange={(tags) => onChange(normalizeTags(tags))}
      splitChars={[',', ' ']}
      clearable={false}
      comboboxProps={{ withinPortal: false }}
    />
  )
}
