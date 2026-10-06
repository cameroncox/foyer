import { Button, Group, MultiSelect, SegmentedControl, Switch, Text, Tooltip } from '@mantine/core'
import { IconUsers } from '@tabler/icons-react'
import { useState } from 'react'

import type { Bookmark, ShareTarget } from '../../api/client.ts'
import { useShareTargets } from '../../api/queries.ts'
import { useCurrentProfile } from '../profiles/profileContext.ts'
import classes from './Sharing.module.css'
import { initialShare, type ShareValue } from './shareValue.ts'

const GROUPS: Record<ShareTarget['kind'], string> = {
  person: 'People',
  yours: 'Yours',
  ownerless: 'Everyone’s',
  default: 'Default',
}

interface Props {
  value: ShareValue
  onChange: (value: ShareValue) => void
  /** The bookmark being edited, if any: its sharing when the form opened, which narrowing asks about. */
  bookmark?: Bookmark
  /** The bookmark's name, for the questions. */
  name: string
  /** The category it's saved in, which other profiles show it under. */
  categoryName: string
  /** Docker cards keep their status dot everywhere; the description says so. */
  docker?: boolean
  /** Shown under the picker, from {@link shareError} when saving. */
  error?: string | null
}

/**
 * Sharing in Add and Edit. Shared bookmarks show, read-only, in other profiles: everyone (Default
 * editors, or anyone with FOYER_ENABLE_SHARE_WITH_EVERYONE) or the profiles picked. It starts on
 * chosen profiles. Turning it off, or taking profiles away, asks first when they already had it.
 * Hidden with profiles off, when there's no one else to share with.
 */
export function SharedField({
  value,
  onChange,
  bookmark,
  name,
  categoryName,
  docker,
  error,
}: Props) {
  const me = useCurrentProfile()?.me
  const enabled = me?.profilesEnabled ?? false
  const targets = useShareTargets(enabled && value.shared)
  const [question, setQuestion] = useState<{
    title: string
    detail: string
    yes: string
    apply: () => void
  }>()
  if (!enabled || !me) {
    return null
  }

  const before = initialShare(bookmark)
  const keptEveryone = before.shared && before.everyone
  const canEveryone = me.canShareWithEveryone || keptEveryone

  // Profiles it already goes to stay listed even if they couldn't be picked now.
  const known = new Map<number, { name: string; group: string }>()
  for (const t of targets.data ?? []) {
    known.set(t.id, { name: t.name, group: GROUPS[t.kind] })
  }
  for (const p of bookmark?.sharedWith?.profiles ?? []) {
    if (!known.has(p.id)) {
      known.set(p.id, { name: p.name, group: 'Already shared with' })
    }
  }

  const data = Object.values(GROUPS)
    .concat('Already shared with')
    .map((group) => ({
      group,
      items: [...known]
        .filter(([, k]) => k.group === group)
        .map(([id, k]) => ({ value: String(id), label: k.name })),
    }))
    .filter((g) => g.items.length > 0)

  const nameOf = (id: number) => known.get(id)?.name ?? 'a profile'
  const ask = (title: string, detail: string, yes: string, apply: () => void) =>
    setQuestion({ title, detail, yes, apply })

  const setShared = (on: boolean) => {
    if (!on && before.shared) {
      ask(
        `Stop sharing “${name}”?`,
        'It disappears for everyone it’s shared with.',
        'Stop sharing',
        () => onChange({ ...value, shared: false }),
      )
    } else {
      onChange({ ...value, shared: on })
    }
  }

  const setEveryone = (everyone: boolean) => {
    if (!everyone && keptEveryone) {
      ask(
        `Stop sharing “${name}” with everyone?`,
        'Only the profiles you pick will see it.',
        'Pick profiles',
        () => onChange({ ...value, everyone: false }),
      )
    } else {
      onChange({ ...value, everyone })
    }
  }

  const setProfiles = (ids: number[]) => {
    const dropped = value.profileIds.filter(
      (id) => !ids.includes(id) && before.profileIds.includes(id),
    )
    if (dropped.length > 0 && !before.everyone) {
      const who = dropped.map(nameOf).join(' and ')
      ask(`Stop sharing “${name}” with ${who}?`, 'It disappears there.', 'Stop sharing', () =>
        onChange({ ...value, profileIds: ids }),
      )
    } else {
      onChange({ ...value, profileIds: ids })
    }
  }

  const description = !value.shared
    ? 'Only this profile shows it.'
    : `${value.everyone ? 'Every profile sees it' : 'The profiles you pick see it'}, read-only, in a category named ${categoryName}${docker ? ', with its status dot' : ''}.`

  return (
    <div className={classes.field} data-confirming={question ? true : undefined}>
      <Switch
        label={
          <Group gap={6} wrap="nowrap">
            <IconUsers size={15} aria-hidden="true" />
            Shared
          </Group>
        }
        description={description}
        labelPosition="left"
        checked={value.shared}
        onChange={(e) => setShared(e.currentTarget.checked)}
        styles={{ body: { justifyContent: 'space-between' }, labelWrapper: { flex: 1 } }}
      />
      {value.shared && (
        <>
          <Tooltip
            label="Only Default’s editors can share with everyone"
            disabled={canEveryone}
            position="top"
          >
            <SegmentedControl
              mt="sm"
              fullWidth
              size="xs"
              aria-label="Share with"
              value={value.everyone ? 'everyone' : 'chosen'}
              onChange={(v) => setEveryone(v === 'everyone')}
              data={[
                { value: 'chosen', label: 'Chosen profiles' },
                { value: 'everyone', label: 'Everyone', disabled: !canEveryone },
              ]}
            />
          </Tooltip>
          {!value.everyone && (
            <MultiSelect
              mt="xs"
              aria-label="Profiles to share with"
              placeholder={value.profileIds.length === 0 ? 'Pick profiles' : undefined}
              data={data}
              value={value.profileIds.map(String)}
              onChange={(ids) => setProfiles(ids.map(Number))}
              nothingFoundMessage={targets.isPending ? 'Loading…' : 'No other profiles yet'}
              searchable
              clearable={false}
              error={error}
              comboboxProps={{ withinPortal: true }}
            />
          )}
        </>
      )}
      {question && (
        <div role="alertdialog" aria-label="Stop sharing">
          <Text size="sm" mt="sm">
            <strong>{question.title}</strong> {question.detail}
          </Text>
          <Group justify="flex-end" gap="xs" mt="xs">
            <Button variant="default" size="xs" onClick={() => setQuestion(undefined)}>
              Keep sharing
            </Button>
            <Button
              color="red"
              size="xs"
              onClick={() => {
                question.apply()
                setQuestion(undefined)
              }}
            >
              {question.yes}
            </Button>
          </Group>
        </div>
      )}
    </div>
  )
}
