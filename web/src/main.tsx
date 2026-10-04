import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/500.css'
import '@mantine/core/styles.css'
import './styles/global.css'

import { createRoot } from 'react-dom/client'

import { Root } from './Root.tsx'

createRoot(document.getElementById('root')!).render(<Root />)
