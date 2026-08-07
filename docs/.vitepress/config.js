import { defineConfig } from 'vitepress'

export default defineConfig({
  title: "RagNext Labs LLC",
  description: "RagNext Labs LLC - Official Home of the RagNext Game Engine & Node-Based Story Designer",
  base: '/',
  themeConfig: {
    logo: '/logo.png',
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Steam Store', link: 'https://store.steampowered.com/app/4944750/RagNext_Studio/' },
      { text: 'Creator Guide', link: '/guide/chapter-1-getting-started' },
      { text: 'Contact', link: 'mailto:contact@ragnext.com' }
    ],

    socialLinks: [
      { icon: 'discord', link: 'https://discord.gg/kYV2hJ7mF' }
    ],

    sidebar: [
      {
        text: '📖 Master Creator Manual',
        items: [
          { text: 'Ch 1: Getting Started & Tour', link: '/guide/chapter-1-getting-started' },
          { text: 'Ch 2: Rooms, Exits & Atmosphere', link: '/guide/chapter-2-rooms-and-exits' },
          { text: 'Ch 3: Objects, Characters & Inventories', link: '/guide/chapter-3-objects-and-characters' },
          { text: 'Ch 4: Variables & Dynamic Text', link: '/guide/chapter-4-variables-and-templates' },
          { text: 'Ch 5: Visual Action Graph', link: '/guide/chapter-5-visual-action-graph' },
          { text: 'Ch 6: Interactive Screens & Hotspots', link: '/guide/chapter-6-interactive-screens-hotspots' },
          { text: 'Ch 7: Timers & Global Functions', link: '/guide/chapter-7-timers-and-global-functions' },
          { text: 'Ch 8: Sound, Media & Polish', link: '/guide/chapter-8-audio-media-polish' },
          { text: 'Ch 9: Packaging & Publishing', link: '/guide/chapter-9-packaging-and-publishing' },
          { text: 'Ch 10: Complete RPG Tutorial', link: '/guide/chapter-10-complete-rpg-tutorial' }
        ]
      },
      {
        text: 'Quick Overview',
        items: [
          { text: 'Getting Started Overview', link: '/guide/getting-started' },
          { text: 'Cross-Platform Player', link: '/guide/cross-platform-player' },
          { text: 'Visual Scripts & Triggers', link: '/guide/actions-and-triggers' },
          { text: 'Variables & State', link: '/guide/variables-and-state' },
          { text: 'Rooms & Navigation', link: '/guide/rooms-and-navigation' },
          { text: 'AI Assistance', link: '/guide/ai-assistance' }
        ]
      },
      {
        text: 'Action Reference',
        items: [
          { text: 'Commands List', link: '/guide/commands' },
          { text: 'Conditions List', link: '/guide/conditions' }
        ]
      }
    ],

    footer: {
      message: 'RagNext Labs LLC — Software Engine & Development Studio',
      copyright: 'Copyright © 2026-present RagNext Labs LLC. All rights reserved.'
    }
  }
})
