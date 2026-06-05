import { defineConfig } from 'vitepress'

export default defineConfig({
  title: "RagNext Game Designer",
  description: "Official Documentation for the RagNext Game Engine Node Editor",
  base: '/RagNext-Game-Designer/',
  themeConfig: {
    logo: '/logo.png',
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Guide', link: '/guide/getting-started' }
    ],

    sidebar: [
      {
        text: 'Introduction',
        items: [
          { text: 'Getting Started', link: '/guide/getting-started' }
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
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026-present Steve Lane'
    }
  }
})
