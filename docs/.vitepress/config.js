import { defineConfig } from 'vitepress'

export default defineConfig({
  title: "RagNext Labs LLC",
  description: "RagNext Labs LLC - Official Home of the RagNext Game Engine & Node-Based Story Designer",
  base: '/',
  themeConfig: {
    logo: '/logo.png',
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'Contact', link: 'mailto:contact@ragnext.com' }
    ],

    socialLinks: [
      { icon: 'discord', link: 'https://discord.gg/kYV2hJ7mF' }
    ],

    sidebar: [
      {
        text: 'Introduction',
        items: [
          { text: 'Getting Started', link: '/guide/getting-started' },
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
