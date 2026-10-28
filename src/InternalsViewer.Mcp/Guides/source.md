# Where the code lives

Internals Viewer is open source at https://github.com/danny-sg/internals-viewer, default branch `master`. When a
question goes past what the run, the guides and the deep dives can answer, the code is the final word on what the app
does with what it recorded. Read a file with your own web or file access when you have it (a raw file is at
`https://raw.githubusercontent.com/danny-sg/internals-viewer/master/<path>`), otherwise tell the user which file would
answer the question. Nothing here lets you run code or change the app.

Everything is under `src/`. The dependencies point inwards: the app depends on the query projects, which depend on the
storage engine internals, which depend on nothing else.

## Storage engine internals, `src/InternalsViewer.Internals`

Pages, records, allocation chains, compression, columnstore segment decoding and metadata, read directly from the data
files or over a connection. `Engine/Pages` and `Engine/Records` decode what the `page_read` tool returns. `Engine/Address`
holds `PageAddress` and `RowIdentifier`.

## Query tracing, `src/Query/InternalsViewer.Query`

The Extended Events capture and everything built on it. `Events/` holds one type per event with its parser (reads under
`Events/Reads`, locks, latches, waits, columnstore and batch mode under `Events/BatchMode`) and the folding that turns
raw events into the groups the app shows (`Events/Consolidation`). `Plans/` parses the execution plan XML into the model
the `query_plan` tool reads. `CallStack/` builds the call tree from the captured frames, resolves symbols and classifies them:
the category and operator rules are the text files in `Mappings/`, and `OperatorCallStackMatcher.cs` is what decides an
operator's entry and exit frames. `Export/` writes and reads the `.ivtrace` export.

## Full Trace, `src/Query/InternalsViewer.Query.FullTrace`

The Time Travel Debugging recording and replay. `Recording/` records SQL Server, `Native/` and `Stubs/` drive the replay
through the native bridge, and `Hooks/` are the C# readers that run at each engine function of interest: `Calls/` is the
call log those hooks fill, `Timeline/` the per-thread spans and the operator lifetimes, `Memory/` the allocations, the
memory map and the memory history, `Iterators/` the link from iterator objects to plan nodes, `Output/` the output row
lineage, `Locators/` the page and row arguments. `TraceLoader.cs` runs a load end to end and `TraceResult.cs` is what
the tools read.

## Execution simulation, `src/InternalsViewer.Execution`

The Volcano-model iterators, access paths and executors behind the Simulation view, which replays query execution
against the pages.

## The app, `src/InternalsViewer.UI.App`

The WinUI 3 app: `Views/` (XAML), `ViewModels/` and `Models/`, with the drawn surfaces in `Controls/` (the timelines,
the plan, the allocation map). `Assets/Instructions` holds the instruction pages the `app` guide is built from.

## This server, `src/InternalsViewer.Mcp`

`Tools/` has one class per tool group, `Documents/` the ids and indexes the tools share, `Guides/` these guides,
`Clients/` the client registration and `Hosting/` the HTTP host.

## Native bridges

`src/Query/InternalsViewer.Query.DiaBridge` resolves symbols from PDB files and `src/Query/InternalsViewer.Query.TimeTravelBridge`
wraps the replay API. Both are C++ and shipped as checked-in binaries under `src/runtimes`.
