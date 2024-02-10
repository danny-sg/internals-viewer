import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "Internals Viewer",
  base: "/internals-viewer/",
  description: "Internals Viewer Documentation",
  themeConfig: {
    // https://vitepress.dev/reference/default-theme-config
    nav: [
      { text: 'Home', link: '/' },
    ],
    siteTitle:false,
    search: {
      provider: 'local'
    },
    docFooter: {
      prev: false,
      next: false,
    },

    sidebar: [
      {
        text: 'Introduction',
        items: [
          { text: 'Getting Started', link: 'docs/introduction/getting-started' },
          { text: 'Installation', link: 'docs/introduction/installation' },
          { text: 'Database', link: 'docs/introduction/database-view' },
          { text: 'Page Viewer', link: 'docs/introduction/page-viewer' },
          { text: 'Background', link: 'docs/introduction/background' }
        ]
      },
      {
        text: 'Tutorial',
        items: [
          { text: 'Introduction', link: 'docs/tutorial/0-introduction' },
          { text: 'Connecting and allocations', link: 'docs/tutorial/1-connecting-and-allocations' },
          { text: 'Viewing pages', link: 'docs/tutorial/2-viewing-pages' },
          { text: 'Indexes', link: 'docs/tutorial/3-indexes' },
        ]
      },
      {
        text: 'Reference',
        items: [
          {text: "Page Header", link: "docs/reference/page-header"},
          {text: "Data Records", link: "docs/reference/data-records"},
          {text: "Index Records", link: "docs/reference/index-records"},
          {text: "Compression", link: "docs/reference/compression"},
        ]
      }
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/danny-sg/internals-viewer' }
    ],

    logo: '/docs/logo.svg'
  }
})
