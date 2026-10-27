# MCP Server

Internals Viewer can act as a Model Context Protocol (MCP) server, so an AI assistant such as Claude Code can read what you have open and move the app to the places it is explaining. It covers all five kinds of tab - Query, Allocations, Page, Index and Columnstore. The assistant works from the same evidence you see - the plan, the events, the call tree, the allocation map, the pages, the index structure, the columnstore segments and, for a Full Trace, the calls, timeline and memory - and the ids it quotes are the ones in the app.

The server cannot connect to another database or server, run a query or start a trace. All it changes in the app is what is selected, scrolled, zoomed or open, the way a click would, and it can open a page, an index or a columnstore index in a new tab, switch to a tab and close one. It can also turn the Query tab's menu toggles on and off, including Record Full Trace for the next run.

## Turning it on

Open **Settings** and expand **MCP Server**. **Enable Server** starts it, and the line above the group shows the address it listens on. It listens on this machine only, on the **Port** in the group, 8484 unless changed.

Every client sends an access token that Internals Viewer generates and stores encrypted for your Windows account. The token is never displayed - the app writes it into each client's own configuration when you register the client. **Regenerate** replaces it and updates every registered client in one go.

<!-- Screenshot: the MCP Server group expanded, with the client cards below the Access Token card -->

## Registering a client

Each client has a card with **Register**. Once registered, the card shows where the server was written, **Remove** takes it out again, and **Update** appears when the port or token has changed since. A card says so when the client is not installed.

- **Claude Code** - registers with the `claude` command at user scope, so the server is available in every project.
- **Cursor** - adds the server to Cursor's global `mcp.json`.
- **GitHub Copilot CLI** - registers with the `copilot` command in its user configuration.
- **Visual Studio Code** - adds the server to the user `mcp.json`, where Copilot agent mode lists its tools.
- **Claude Desktop** - needs Node.js first, see [Claude Desktop](#claude-desktop) below.

A client picks the server up the next time it starts, so restart a client that was already open.

## Claude Desktop

Claude Desktop starts its local MCP servers as programs, and its connectors cannot reach an address on your own machine, so it needs a small bridge between it and Internals Viewer. The bridge is the `mcp-remote` package, which Node.js downloads the first time Claude Desktop starts it. Internals Viewer writes the Claude Desktop configuration for you but does not install Node.js, so that is the one step to do yourself.

1. Install Node.js. Take the LTS release from [nodejs.org](https://nodejs.org), or install it with winget:

   ```
   winget install OpenJS.NodeJS.LTS
   ```

2. Open a new terminal and check it is on the path:

   ```
   npx --version
   ```

3. In Internals Viewer, open **Settings**, expand **MCP Server** and press **Register** on the **Claude Desktop** card. The card says Node.js is not installed until it can find `npx`, so restart Internals Viewer if it was open while Node.js was installed.

4. Restart Claude Desktop. The first start downloads the bridge, which takes a moment, after which the Internals Viewer tools appear under the tools button in the chat box.

Internals Viewer has to be running for the bridge to connect. If it was not running when Claude Desktop started, Claude Desktop shows the server as failed until it is restarted with Internals Viewer open.

## Starting a conversation

The conversation starts in the assistant, not in the app. With a database and a Query tab open and a query run or imported, ask the assistant about it in plain language - "look at the query open in Internals Viewer and tell me why it read so many pages". It finds the open document, reads the summary, the plan, the reads and the call tree, and quotes the ids so you can find the same things in the app.

In Claude Code, `/mcp` shows the server and its tools, and the `explore` prompt it provides starts an investigation with the evidence-first approach built in. Asking it to walk you through an operator makes it open the plan node, the call tree node and the trace position as it explains, and asking "what is this?" after you have selected something in the app makes it look at your selection - the selected event, plan node, call tree node, page, slot, field and log record, the playhead, and the ranges the timelines are showing.

The server also carries its own reading guides - how to read what the tools return, what each event means, how to read engine function names, the method for explaining an operator from a Full Trace, the app's own instruction pages so it describes things by the names you see, and the [deep dives](/docs/deep-dives/query-tracing) from this site - so an assistant that has never seen Internals Viewer can still work from the evidence. For a question nothing else answers it knows where the [source code](https://github.com/danny-sg/internals-viewer) for each part lives.

## What the assistant can see

- **The run** - the SQL, the plan with estimated and actual rows, the operators with their timing, and the messages.
- **Events** - reads from disk and the buffer pool, waits, locks, latches, columnstore scans and log records.
- **The call tree** - the engine functions behind each operator, the paths to a function, the hottest paths and the pages each function touched.
- **Full Trace** - every logged call with its arguments, the timeline, the memory, the memory map and the memory history.
- **Allocations** - the files and objects of a database, who owns a page and what the GAM, SGAM, PFS, DCM and BCM bits say about it, and the extents an object owns.
- **Pages** - a page's header, slots and records with their column values, including the page header and offset table as selectable structures.
- **Indexes** - the B-tree of an index level by level, its pages in order, and the keys and child pages of the page you have selected.
- **Columnstore** - the row groups, segments and dictionaries of a columnstore index.
- **The app** - what you have selected in the active tab, whatever kind it is, and what the timelines are showing.

## What the assistant can do in the app

- **Open** a call, a page, a plan node, an event or a memory block, move the playhead, and open an index or columnstore index in a new tab. A page opens in the Query tab's page dock when you are in a Query tab, and in a new Page tab otherwise.
- **Select and scroll** - slots, fields and bytes in the Page viewer, objects and overlays in the Allocations map, pages and records in the Index viewer, and segments, dictionaries and rows in the Columnstore viewer.
- **Switch and close tabs.** The assistant should ask before it closes one.
- **Turn the Query tab's menu toggles on and off** - the views that are visible, what is captured and shown, and Record Full Trace. Record Full Trace only turns on once you have accepted its warning and licence yourself from the Trace menu, and applies to the next run.
- **Drive the Trace Timeline** - turn the Operators, Memory and Calls panes and the other timeline options on and off, add custom operator tracks for a function (for example "mark where a b-tree is searched"), clear them, step the playhead by call, same call, batch, row or allocation, search, and select and zoom to a call such as "the first GetRow call". A fuzzy search of the call graph resolves half remembered function names.

## Walking through a seek or a scan

Ask the assistant to show you how SQL Server finds a key, for example "show me how SQL Server finds Id = 1000 in dbo.ClusteredTable", or the `index_seek` prompt. It selects the root page in the Index viewer, reads the keys on it, probes the slots of a binary search one at a time and follows the child page it chooses, down to the leaf page, where it opens the row. `index_scan` does the same for a scan in key order, down the left or right edge of the B-tree and along the leaf pages using the previous and next page in each header.
