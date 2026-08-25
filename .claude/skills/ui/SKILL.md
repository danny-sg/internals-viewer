---
name: ui
description: Design language and guidance, and review for the Internals Viewer app
---

# Design

Internals Viewer is a WinUI3 desktop application that displays dense and complex information
about the internals of SQL Server. UI consistency is important, if the app is coherent,
consisent, and intuative the complexity is easier to navigate and understand.

User flow is:

    Connect to database > View allocations > Open detail view (Page/Index/Columnstore/Query)

The detail view then progressively show information about the specific area and link to each other.

Top level views are added as tabs. Tabs can contain Dock Tabs that add child elements as tabs that can
be rearrange. These child elements can be singleton, fixed/unclosable, or tabs can be dynamically added
by the view.

## View Commandbar

All top level views should have a command bar with a consistent layout:

Title/Sub-title | Commands | Details (optional) | Navigation (optional)

- Title/Sub-title gives information about the view type and the current object. It can also
  contain a navigation control if it is intrinsic to the type, e.g. the Page view has Page Address
  navigation in the title that shows the current address and allows a user to change it

- Commands are view specific, but ordering should be consistent - for example if two views
  contain the same commands they should always be displayed in the same order relative to each other.

- Details is specifc to the view but should always be presented the same, label/value in an aligned grid, max 2 rows, with label `TextFillColorTertiaryBrush`.

- Navigation is right aligned, and optional depending on the view.

# Views

# Checks

# Performance
