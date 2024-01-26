import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "Internals Viewer",
  description: "Internals Viewer Documentation",
  themeConfig: {
    // https://vitepress.dev/reference/default-theme-config
    nav: [
      { text: 'Home', link: '/' },
    ],

    sidebar: [
      {
        text: 'Introduction',
        items: [
          { text: 'Getting Started', link: 'docs/introduction/getting-started' },
          { text: 'Installation', link: 'docs/introduction/installation' },
          { text: 'Background', link: 'docs/introduction/background' }
        ]
      }
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/danny-sg/internals-viewer' }
    ],

    logo: 'logo.png'
  }
})
