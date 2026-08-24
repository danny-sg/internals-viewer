---
name: testing
description: Running and writing tests for Internals Viewer. Use when running the test suite or a single test, adding a test project or test class, interpreting test failures, or deciding whether a failure is a real regression or an environmental one.
---

# Testing

xUnit throughout, one test project per source project, all under `src/Tests/`. `ImplicitUsings` is on and
there is a global `using Xunit`, so test files need no using block for the framework.

## Running tests

```bash
dotnet test src/Tests/InternalsViewer.Internals.Tests -p:Platform=x64
```

`InternalsViewer.UI.App.Tests` targets `net10.0-windows` and references the app project, so it needs the MSIX
opt-out as well — without it the reference build runs the app's packaging step, which fails on most machines:

```bash
dotnet test src/Tests/InternalsViewer.UI.App.Tests -p:Platform=x64 -p:EnableMsixTooling=false
```

A single class or method:

```bash
dotnet test src/Tests/InternalsViewer.Query.Tests --filter "FullyQualifiedName~XmlEventParserTests"
```

```bash
dotnet test src/Tests/InternalsViewer.Query.Tests --filter "FullyQualifiedName~XmlEventParserTests.Parses_File_Event"
```

Do not run the whole suite after a mechanical change such as a rename or a namespace move — build to confirm
it compiles, batch the remaining work, and run the suite once at the end.

## Test projects

| Project | Covers |
| --- | --- |
| `InternalsViewer.Internals.Tests` | Storage engine internals. Split into `UnitTests/` and `IntegrationTests/`, with shared helpers in `Helpers/` |
| `InternalsViewer.Execution.Tests` | Iterator/join integration plus access-path units. Links `TestServiceHost`, `RequiresFileFact` and `TestDatabase.mdf` from Internals.Tests rather than duplicating them |
| `InternalsViewer.Query.Tests` | Event parsing, plan parsing, callstack projection |
| `InternalsViewer.Connection.BackupFile.Tests` | Backup file reading. Has its own `RequiresFileFact`/`RequiresFileTheory` |
| `InternalsViewer.UI.App.Tests` | Extracted UI logic plus headless SkiaSharp pixel tests that render into an `SKBitmap` |
| `InternalsViewer.Internals.Tests.VerificationTool` | Console tool, not a test project |

## Conventions

- Method names are `Verb_Describes_Behaviour` — `Parses_File_Event`, `Can_Run_Simple_Query`.
- Most test projects keep test files flat in the project root. `InternalsViewer.UI.App.Tests` is the exception:
  its folders and namespaces mirror the app project (`Controls\Timeline\Renderers`, etc.).
- The app project grants `InternalsVisibleTo` to its test project, so internal types are testable directly.

## Tests that need external resources

Several tests are gated on resources that are not in the repo, and skip or fail rather than run:

- `RequiresConnectionStringFact` / `RequiresConnectionStringTheory` — skips unless the named connection string
  is present in **user secrets**. `ConnectionStringHelper` reads it from there, not from an appsettings file.
- `RequiresFileFact` — skips unless a data file is present. `TestDatabase.mdf` under
  `IntegrationTests/Test Data/` is checked in; larger backup fixtures are not.
- `QueryRunnerTests.Can_Run_Simple_Query` is a genuine integration test: it needs a live SQL Server and
  symbols in `C:\Symbols`. Without a server it fails after roughly a 15-second timeout. That is
  environmental, not a regression. `Invalid_Query_Gives_IsSuccess_False` passes trivially in the same case.

## Baselines — failures that are already on main

Check against these before treating a red test as something you broke:

- `InternalsViewer.Internals.Tests` — 14 unit failures on a clean main (`DateTimeConverter` locale
  sensitivity, allocation chain tests) plus 3 tests pinned to hardcoded values that have since changed.
  Baseline is roughly 600 of 617 passing with a lab server attached.
- `InternalsViewer.UI.App.Tests` — `DockLayoutSerializer.Round_Trips_A_Split_Layout` fails on clean main.
  Baseline 88 of 89.

A "test host process crashed" abort has been seen once after all tests had already passed, and did not
reproduce on rerun.
