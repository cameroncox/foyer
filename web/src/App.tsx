import { AppShell, Title } from '@mantine/core'

export default function App() {
  return (
    <AppShell header={{ height: 56 }} padding="md">
      <AppShell.Header px="md" style={{ display: 'flex', alignItems: 'center' }}>
        <Title order={3}>Foyer</Title>
      </AppShell.Header>
      <AppShell.Main />
    </AppShell>
  )
}
