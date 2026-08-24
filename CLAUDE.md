# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Internals Viewer is a visualisation tool for the SQL Server storage engine: it reads pages, records,
allocation structures and metadata directly, replays query execution against them, and draws the result
in a WinUI 3 desktop app.

## Build

The solution is `src/InternalsViewer.slnx` (.NET 10). Most projects are `x64`-only — `InternalsViewer.UI.App`
and the native `InternalsViewer.Query.DiaBridge` never build under `Any CPU`.

```bash
dotnet build src/InternalsViewer.slnx -p:Platform=x64
```

Build the app on its own (this is the command that works when the full solution build trips over MSIX tooling):

```bash
dotnet build src/InternalsViewer.UI.App -p:Platform=x64 -p:EnableMsixTooling=false
```

Docs site (VitePress, in `docs/internalsviewer.com`) — `npm run docs:build` also acts as the link check:

```bash
npm run docs:build --prefix docs/internalsviewer.com
```

For running tests, see the `testing` skill.

## Architecture

Layered, with dependencies pointing inwards. `InternalsViewer.Internals` is the core and depends on nothing
else in the solution.

- **`InternalsViewer.Internals`** — storage engine internals. Pages, records, allocation chains, compression,
  columnstore segment decoding, metadata. Everything reachable through DI via `RegisterServices()`.
- **`InternalsViewer.Execution`** — execution simulation (Volcano-model iterators, access paths, executors)
  built on top of Internals. Registers itself with `RegisterExecutionServices()`; callers invoke both.
- **`InternalsViewer.Query`** — Extended Events capture, execution plan parsing, callstack resolution.
  Plan types live under `Plans/` (`Model/`, `Operators/`, `Joins/`, `Parsers/`); event types under `Events/`.
- **`InternalsViewer.Connection.BackupFile`** — reads pages straight out of `.bak` files (MTF container,
  compressed and striped backups included).
- **`InternalsViewer.Connection.Sandbox`**, **`InternalsViewer.TransactionLog`** — supporting connection and
  log-reading layers.
- **`InternalsViewer.Query.DiaBridge`** — native C++ (`.vcxproj`) shim over the DIA SDK for PDB symbol
  resolution. The shipped binary is the checked-in copy under `src/runtimes/win-x64/native`.
- **`InternalsViewer.Internals.Metadata.SourceGenerators`** — source generator for metadata types.
- **`InternalsViewer.UI.App`** — WinUI 3 app. MVVM: `Views/` (XAML) → `ViewModels/` → `Models/`, with
  SkiaSharp-based custom drawing in `Controls/`. Tabs are dockable documents.

Notes that apply to only one project live in a `CLAUDE.md` inside that project — see
[src/InternalsViewer.UI.App/CLAUDE.md](src/InternalsViewer.UI.App/CLAUDE.md). Those load automatically when
files in that folder are read, so keep project-specific detail there rather than here.

## Code style

Project code style rules are in [.github/copilot-instructions.md](.github/copilot-instructions.md), shared with
GitHub Copilot so there is one source of truth:

@.github/copilot-instructions.md

Additional conventions for this repository:

- **Comments belong to the author of the code.** Do not add explanatory comments to code you write, and never
  edit or "finish" a comment someone else wrote — only point out one that has become inaccurate.
- XML doc `<summary>` is a single sentence with **no** full stop; put detail in `<remarks>`.
- Leave a blank line between non-trivial statements, including consecutive `var` declarations and
  multi-statement `switch` case bodies.
- UI display strings are Title Case ("Bit Pack Entries", not "Bit pack entries").
- Never use "..." in prose or display strings — write "etc." instead.
- Prefer short sentences over semicolons in prose.

## Known environmental build failures

These are tooling problems on some machines, not code regressions — do not chase them:

- `MSB6011` / `mspdbcmf.exe could not be found` (WinAppSdkCheckFastlinkPdb) during the UI.App x64 post-build.
- `WinAppSdkCreateAppStoreContainer` failing with `FileNotFound System.Security.Permissions 8.0.0.0`.
- `WMC9999 Object reference not set` / `WMC1509 No LocalAssembly parameter` in the XAML markup compiler,
  cascading into bogus `WMC0001 Unknown type` errors for App.xaml converters.

Passing `-p:EnableMsixTooling=false` avoids the packaging steps behind most of these.
