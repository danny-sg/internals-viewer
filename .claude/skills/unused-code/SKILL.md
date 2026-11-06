---
name: unused-code
description: Finds unused types and members in Internals Viewer projects by Roslyn reachability from production code, where code reached only from tests or from other unused code counts as unused, then removes them without losing test coverage. Use whenever the user asks whether a type or member is used, asks to find, review or remove unused, dead, leftover or test-only code in a project, or says unused types keep being missed, even when they name only one type.
---

# Unused code

A type is unused when no live production code reaches it. Searching for its name is not enough. `MemoryHistorySummary`
was named in production code, but only by `MemoryHistoryStore.GetSummary`, and only tests called that. A name search
also cannot tell overloads apart, so a hit for `GetRanges` says nothing about the one-argument `GetRanges`.

The analyser in `scripts/UnusedCodeAnalyser` loads the whole solution with Roslyn and follows references from production
code. It reports what nothing reaches. Every finding still gets checked by hand before it goes in the report.

## Run the analyser

Pass the assembly names to review. Every other non-test project in the solution counts as a user of them, so UI.App and
the MCP server keep alive whatever they call.

```bash
dotnet run --project .claude/skills/unused-code/scripts/UnusedCodeAnalyser -- src/InternalsViewer.slnx <output directory> InternalsViewer.Query InternalsViewer.Query.FullTrace
```

Put the output directory in the scratchpad. A run takes one to two minutes, so give the command a 10 minute timeout.

The analyser builds the source generators into `bin\analysis` and `obj\analysis`, then loads every project with those
folders as its output. It never touches the default `bin` and `obj` folders, and it deletes the `analysis` folders when it
finishes. The analysis runs in a child process because the generator DLLs stay locked until that process exits.

Check the project lines before reading the reports. Every project should show `0 errors`. Exit code 2 means a project
did not compile. References from its broken code are then missing, so a finding may be false. Workspace messages about
`.vcxproj` files and unresolved assemblies are filtered out because they are expected.

| File | What it lists |
| --- | --- |
| `types.txt` | Types nothing live reaches, with who still names them |
| `members.txt` | Members of live types that nothing live reaches, including positional and layout ones |
| `members-by-file.txt` | The same members grouped by file, without positional record properties or native layout fields |
| `removals.tsv` | The `members-by-file.txt` list as input for the remover |
| `implemented-only.txt` | Interfaces that types implement but nothing uses as a type |
| `never-constructed.txt` | Live classes that no live code constructs |
| `registration-only.txt` | Types whose only use is a DI registration |

Each finding has a status. **no references** means nothing names it. **tests only** means only tests use it. **only from
dead code** means production code names it, but only from code that is itself unused.

## Check every finding

The analyser follows syntax, so verify each finding before reporting it.

- **Types.** Search non-test `src` for the name, including `.xaml`. Read each hit and confirm it is the same type and not
  another type of the same name.
- **Members.** Search for the name. A hit is often a different overload or another type's member, so read the call before
  deciding the analyser is wrong. When the analyser and a name search disagree, the analyser is usually right.
- **`implemented-only.txt`.** These are real findings. `IEventParser<TEvent>` was implemented by 13 parsers, but every
  `Map` was called on the concrete class.
- **`never-constructed.txt`.** These are mostly not findings. DI creates services registered in `App`, marshalling creates
  `SafeHandle` types, `Activator` creates the export converters, and some classes only have static members. A class that
  only has static members is worth mentioning, because it could be a `static class`.
- **`registration-only.txt`.** These are types registered in DI that nothing injects. Check the registration code before
  calling one unused.

These look unused to the analyser but are not:

- **Positional record properties.** Record equality and hashing read them, and records such as `SymbolKey` and
  `LockIdentity` are dictionary keys. They are left out of `members-by-file.txt` and `removals.tsv`.
- **Native layout fields.** Fields such as `Reserved` keep the struct layout the native bridge writes. These are also
  left out.
- **Properties read by reflection.** System.Text.Json serialises exported types. `ExportPackage` finds every non-abstract
  `EngineEvent` and `LogRecord` type with `GetTypes`. Before removing a property from a type reachable from
  `QueryExport`, check whether removing it changes the export.
- **String bindings in code-behind.** XAML `{x:Bind}` and `{Binding}` names are counted, but a `PropertyPath("Name")`
  built in C# is not. Search for the name in quotes.

## Report

The review ends with a report. Remove nothing until the user agrees. Use this shape:

1. A table of unused types: the type as a link to its declaration, and why nothing live reaches it.
2. For each cluster, the members that would go with it. For example, removing a type also removes the test-only method
   that returns it.
3. The members from `members-by-file.txt`, summarised by area with counts and the most notable ones. Give the path of the
   full file.
4. What the run could not see: projects that did not compile, properties that may be serialised, and the fact that a
   use from unused code in another project still counts as a use.

## Remove

Work in this order. Each removal can leave more code unused, so the last step loops back.

1. **Whole types.** Delete the file of a top-level type. Remove a nested type with the remover by giving the line its name
   is declared on.
2. **Members.** Trim `removals.tsv` to what the user agreed to remove. Then run the remover with `src` as the root:

   ```bash
   dotnet run --project .claude/skills/unused-code/scripts/MemberRemover -- src <path to removals.tsv>
   ```

   It removes each member with its doc comment and keeps the file's line endings and BOM. It refuses a file when a line
   does not declare the named member. Line numbers only match the run that wrote `removals.tsv`, so run the analyser
   again after editing a file and before removing more from it.
3. **Follow-ups the remover cannot make.**
   - Remove `using` directives that are no longer needed.
   - A primary constructor parameter that only fed a removed property is now unread. `KeyHashLookup` lost its logger
     this way and became a `static class`.
   - Data that only fed a removed reader can go too. `TraceRowReturnMarkMap` kept a second copy of every mark for
     `GetCount` and `GetMarks`, so removing them removed the copy, and `Count` now comes from `Ticks`.
4. **Build the reviewed projects**, then build every test project that references them.
5. **Fix the tests.** A test of a removed feature goes, or only its assertion goes when the rest of the test covers live
   code. A test that used a removed convenience overload to set up or observe live code keeps its coverage:
   - Switch it to the production overload when one exists. `ParseEvent(string)` became `ParseEvent(buffer, length)`.
   - Otherwise move the overload into the tests. Make it an extension method in the project's existing helper class, so
     the call sites do not change. `TraceCallStore.Builder.Add(span)` moved to `TraceCallRowHelper` and `TraceCallRows`.
     For a static method or a property, add a helper at the test project root, as `FunctionSignatureHelper` and
     `TraceTickSeriesHelper` did.
   - A helper that needs private state may make that member `internal`, since the test projects have
     `InternalsVisibleTo`. Say so in the summary.
6. **Run the analyser again** and repeat until it finds nothing new.
7. **Run the affected test suites.** The `testing` skill has the commands and the known failures.
8. **Final checks.**
   - Every edited file keeps its line endings. Use the remover or Python with `newline=""`, never `sed -i`.
   - Lines stay within 140 characters. A rename shifts the continuation lines aligned under a call, so realign them.
   - Search for comments and `cref`s that name removed members. Comments belong to their author, so list them for the
     user rather than editing them.

## Limits

- The reviewed projects need users. UI.App and the MCP server are entry points, so this cannot review them. Reflection,
  XAML and the MCP SDK reach their code in ways the analyser does not see.
- Code in another project counts as a use even when that code is itself unused.
- Unused `using` directives and unread parameters are not reported.
