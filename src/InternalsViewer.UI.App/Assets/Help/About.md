# About Internals Viewer

**Internals Viewer** is a visualisation tool for examining the internals of the SQL Server
storage engine — its pages, extents, allocation maps, indexes and query execution.

<!-- TODO: confirm the exact version string to display here (the app shows "Internals Viewer 2026.1").
     Consider binding this to the package version at runtime rather than hard-coding it. -->

## Purpose

Internals Viewer renders the physical structure of a SQL Server database for learning,
teaching and investigation. For an introduction to the underlying concepts, see the
[SQL Server internals primer](Internals.md).

## Getting started

- [Start and connecting](Start.md) — open a database.
- [Allocations / Database view](Allocations.md) — the map of the whole database.
- [Pages](Pages.md), [Indexes](Indexes.md), [Query](Query.md) — the detail views.

## Technology

- C# / .NET 10.0
- Windows App SDK (WinUI 3)

## Compatibility

- Windows 10 version 17763.0 or higher
- Tested on SQL Server 2019 – 2022

## Licence

Internals Viewer is open-source software, released under the **GNU General Public License
v3.0**.

## Links

- Source code and releases: https://github.com/danny-sg/internals-viewer
- Available from the Microsoft Store (search for *Internals Viewer*).

<!-- TODO: confirm the wording/credits for this page — e.g. author, acknowledgement of the
     original Internals Viewer, contributor credits, support/feedback link. Left factual for now. -->
