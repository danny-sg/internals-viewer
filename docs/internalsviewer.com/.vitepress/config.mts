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
        text: "User Guide",
        items: [
          {
            text: "Getting Started",
            link: "docs/user-guide/getting-started",
          },
          { text: "Installation", link: "docs/user-guide/installation" },
          { text: "Permissions", link: "docs/user-guide/permissions" },
          { text: "Allocations", link: "docs/user-guide/allocations" },
          { text: "Page Viewer", link: "docs/user-guide/page-viewer" },
          { text: "Index View", link: "docs/user-guide/index-view" },
          {
            text: "Query",
            collapsed: false,
            items: [
              { text: "Overview", link: "docs/user-guide/query" },
              { text: "Timeline", link: "docs/user-guide/query/Timeline" },
              { text: "Events", link: "docs/user-guide/query/Events" },
              { text: "Reads", link: "docs/user-guide/query/Reads" },
              { text: "Locks", link: "docs/user-guide/query/Locks" },
              { text: "Latches", link: "docs/user-guide/query/Latches" },
              { text: "Waits", link: "docs/user-guide/query/Waits" },
              {
                text: "Allocations",
                link: "docs/user-guide/query/Allocations",
              },
              { text: "Call Stack", link: "docs/user-guide/query/CallStack" },
              {
                text: "Execution Plan",
                link: "docs/user-guide/query/ExecutionPlan",
              },
              { text: "SQL Editor", link: "docs/user-guide/query/Editor" },
              {
                text: "Log Records",
                link: "docs/user-guide/query/LogRecords",
              },
            ],
          },
          { text: "Settings", link: "docs/user-guide/settings" },
          { text: "Background", link: "docs/user-guide/background" },
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
              {
                text: "Lookups",
                link: "docs/tutorial/query/5-lookups",
              },
              { text: "Joins", link: "docs/tutorial/query/6-joins" },
              {
                text: "Log Records",
                link: "docs/tutorial/query/7-log-records",
              },
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
          { text: "Log Appliers", link: "docs/reference/log-appliers" },
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
