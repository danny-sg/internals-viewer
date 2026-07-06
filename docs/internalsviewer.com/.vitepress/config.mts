import { defineConfig } from "vitepress";

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "Internals Viewer",
  base: "/internals-viewer/",
  description: "Internals Viewer Documentation",
  themeConfig: {
    // https://vitepress.dev/reference/default-theme-config
    nav: [{ text: "Home", link: "/" }],
    siteTitle: false,
    search: {
      provider: "local",
    },
    docFooter: {
      prev: false,
      next: false,
    },

    sidebar: [
      {
        text: "Introduction",
        items: [
          {
            text: "Getting Started",
            link: "docs/introduction/getting-started",
          },
          { text: "Installation", link: "docs/introduction/installation" },
          { text: "Database", link: "docs/introduction/database-view" },
          { text: "Page Viewer", link: "docs/introduction/page-viewer" },
          { text: "Index Viewer", link: "docs/introduction/index-viewer" },
          { text: "Query", link: "docs/introduction/query" },
          { text: "Background", link: "docs/introduction/background" },
        ],
      },
      {
        text: "Tutorial",
        items: [
          { text: "Introduction", link: "docs/tutorial/0-introduction" },
          {
            text: "Connecting and allocations",
            link: "docs/tutorial/1-connecting-and-allocations",
          },
          { text: "Viewing pages", link: "docs/tutorial/2-viewing-pages" },
          { text: "Indexes", link: "docs/tutorial/3-indexes" },
          {
            text: "Query",
            collapsed: false,
            items: [
              {
                text: "Using the Query view",
                link: "docs/tutorial/query/1-using-the-query-view",
              },
              {
                text: "Views and layout",
                link: "docs/tutorial/query/2-views-and-layout",
              },
              {
                text: "The execution plan",
                link: "docs/tutorial/query/3-execution-plan",
              },
              {
                text: "Scans vs seeks",
                link: "docs/tutorial/query/4-scans-vs-seeks",
              },
              { text: "Joins", link: "docs/tutorial/query/5-joins" },
            ],
          },
          { text: "LOB data", link: "docs/tutorial/5-lob-data" },
        ],
      },
      {
        text: "Deep Dive",
        items: [
          {
            text: "How the database is loaded",
            link: "docs/deep-dives/loading-a-database",
          },
          {
            text: "How query tracing works",
            link: "docs/deep-dives/query-tracing",
          },
          {
            text: "How the Index view works",
            link: "docs/deep-dives/index-view",
          },
        ],
      },
      {
        text: "Reference",
        items: [
          { text: "Page Header", link: "docs/reference/page-header" },
          { text: "Data Records", link: "docs/reference/data-records" },
          { text: "Index Records", link: "docs/reference/index-records" },
          { text: "Compression", link: "docs/reference/compression" },
          { text: "Glossary", link: "docs/reference/glossary" },
          { text: "Resources", link: "docs/reference/resources" },
        ],
      },
    ],

    socialLinks: [
      { icon: "github", link: "https://github.com/danny-sg/internals-viewer" },
    ],

    logo: "/docs/logo.svg",
  },
});
