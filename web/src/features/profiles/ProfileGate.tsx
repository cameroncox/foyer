import { Alert, Button, Center, Loader, Stack, Text } from '@mantine/core'
import { Fragment, type ReactNode, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router'

import { ApiError } from '../../api/client.ts'
import { useMe } from '../../api/queries.ts'
import { ProfileContext } from './profileContext.ts'
import { forgetProfile, loadRememberedProfile } from './rememberedProfile.ts'

interface Props {
  /** The profile to open, instead of the URL's (quick add passes its ?profile=). */
  profile?: string
  children: ReactNode
}

/**
 * Works out the profile the page shows and provides it below: the URL's, else the one last
 * picked on this device, else the server's choice (the user's personal profile, or Default).
 * A remembered pick that's gone or no longer visible is dropped quietly; a URL that names one
 * shows a not-found page. Switching profiles remounts the page, so nothing carries over.
 */
export function ProfileGate({ profile, children }: Props) {
  const params = useParams()
  const fromUrl = profile ?? params.profile
  const [remembered, setRemembered] = useState(loadRememberedProfile)
  const requested = fromUrl ?? remembered
  const me = useMe(requested)
  const missing = me.error instanceof ApiError && me.error.status === 404

  // A remembered pick that's gone is dropped while rendering, so the next render asks without it.
  if (missing && fromUrl === undefined && remembered !== undefined) {
    forgetProfile()
    setRemembered(undefined)
  }

  const current = useMemo(
    () =>
      me.data && {
        slug: me.data.profilesEnabled ? me.data.current.slug : undefined,
        me: me.data,
      },
    [me.data],
  )

  if (current) {
    return (
      <ProfileContext value={current}>
        <Fragment key={current.slug ?? ''}>{children}</Fragment>
      </ProfileContext>
    )
  }

  if (missing && fromUrl !== undefined) {
    return (
      <Center py={80} px="md">
        <Stack align="center" gap="sm">
          <Text fw={600} size="lg">
            There’s no profile “{fromUrl}”.
          </Text>
          <Text c="dimmed" size="sm">
            It may have been renamed or deleted, or it belongs to someone else.
          </Text>
          <Button component={Link} to="/" variant="default" mt="xs">
            Go home
          </Button>
        </Stack>
      </Center>
    )
  }

  if (me.isError && !missing) {
    return (
      <Center py={80} px="md">
        <Alert color="red" title="Couldn't open Foyer" maw={480}>
          {me.error.message}
        </Alert>
      </Center>
    )
  }

  return (
    <Center py="xl">
      <Loader aria-label="Loading profile" />
    </Center>
  )
}
