# Settings

Settings are opened from **Settings** at the bottom of the sidebar on the **Internals Viewer** tab. They are grouped into **Query**, for where query traces and symbols go, and **Debugging**, for WinDbg and Full Trace, followed by the **Diagnostic log**, **Current memory usage** and **About**.

Settings save as soon as they change - a text box when you move out of it.

<!-- Screenshot: the Settings page with the Query and Debugging groups expanded -->

## Query

Settings for [Query](/docs/user-guide/query) tracing - where trace files and symbols are kept, and how large a trace can grow.

### Trace path

Where the `.xel` trace files are written. Internals Viewer assumes it will usually be run on or near the SQL Server instance being traced, and the **Use Local Directory** switch chooses between two places:

- **Off**, the default - SQL Server writes trace files to its own log directory. This works against a remote instance, since nothing has to be shared between the two machines, but Internals Viewer cannot delete the files afterwards, so they build up over time. A warning above the card says so:

  > Trace files will be saved to the SQL Server log directory. Running traces will generate trace files that cannot be deleted post-trace by Internals Viewer. These files should be periodically removed. Alternatively use a local directory that will be cleared as part of the trace process.

- **On** - trace files go to the folder in the box, `%ProgramData%\InternalsViewer\Traces` unless changed with **Browse…**. Both Internals Viewer and the SQL Server service write and read this folder, so it suits an instance on the same machine. Because Internals Viewer manages the folder, it can delete each trace once it has been read - see **Auto-delete trace** below.

**Grant permissions** creates the folder if needed and gives each SQL Server service on the machine Modify access to it. The same is done before every traced query while **Use Local Directory** is on, so the button is only needed to put the permissions right without running a query.

### Auto-delete trace

On by default. Deletes each trace's `.xel` files once they have been read, so they don't accumulate in the folder. Only available while **Use Local Directory** is on, since files in SQL Server's log directory cannot be deleted.

### Full Columnstore allocation resolution

On by default. Maps every page chain of the columnstore indexes a query scans, so that each page read can be matched to the segment, dictionary or delete bitmap it fetched and linked to the object pool miss that caused it - see [Columnstore](/docs/user-guide/query/Timeline#columnstore). Mapping a large columnstore index takes a while, so it can be turned off when only the row store side of a query matters.

### Maximum trace size

The largest a trace file may grow, in MB - 150 by default, and up to 102,400. A trace that outgrows it loses events, so increase it if a large or long-running query's trace comes back incomplete.

### Symbols path

The folder SQL Server's debugging symbols (PDB files) are downloaded to when resolving [Call Stack](/docs/user-guide/query/CallStack) events, and searched by **Search symbols**. Defaults to `C:\Symbols`, and **Browse…** picks another.

## Debugging

Settings for sending commands from the [Call Stack](/docs/user-guide/query/CallStack#sending-commands-to-windbg) to a WinDbg session attached to SQL Server, and for recording with **Record Full Trace** - see [Full Trace](/docs/user-guide/query/FullTrace).

### WinDbg Password

The password the WinDbg session requires on its `InternalsViewer` named pipe, so nothing else on the machine can join it. A random password is generated the first time Settings is opened, and it is stored encrypted with Windows data protection (DPAPI), so only your Windows account can read it. It can be changed here, to match a WinDbg you start yourself.

### WinDbg Path

The WinDbg executable to start when attaching to SQL Server. Blank finds WinDbg from the Microsoft Store, then the Debugging Tools for Windows. The same installation is where Record Full Trace takes its Time Travel Debugging recorder from.

### Time Travel Warning

On by default. Shows the dialog explaining what a recording involves - the administrator prompt, the service restart and the size of the files - each time [Record Full Trace](/docs/user-guide/query/FullTrace#turning-it-on) is turned on. Ticking **Do Not Show Again** and choosing **Turn On** turns this off.

## Diagnostic log

The activity log from the internals loading pipeline - what Internals Viewer itself is doing as it reads a database, including the version of each server connected to and whether the login has `sysadmin`. **Open Log** opens it in a **Log** tab, and **Clear Log** empties it.

In the Log tab:

- **Log level** - how much is recorded from now on, from **Critical** to **Trace**. **Information** by default. It affects what is captured next, not the entries already there.
- **Max messages** - how many entries are kept, 1,000 by default.
- **Search** - filters the entries by message, category, level or exception.
- **Export** - saves the entries shown to a text file.
- **Clear** - empties the log.

This is the first place to look if a database fails to load or something doesn't decode as expected.

## Current memory usage

An indication of how much memory Internals Viewer is using, updated every second while Settings is open. It leaves out the memory used to open a [Full Trace](/docs/user-guide/query/FullTrace) recording, which can be far larger.

## About

Who made Internals Viewer, the copyright, and a link to the [GitHub repository](https://github.com/danny-sg/internals-viewer). The version is shown at the top of the sidebar on the **Internals Viewer** tab.

## Remembered automatically

Some choices are remembered without appearing on the Settings page, stored alongside it:

- **Connections** - the [recent connections](/docs/user-guide/getting-started#recent-connections), with their passwords encrypted, and the last values entered on the **SQL Server** page, without the password
- **SQL Editor** - the font size, the **Clear Buffer Pool** and **Disable Read-Ahead** toggles, whether **Results** and **History** are shown, and each database's query history
- **Query view** - the pane layout, the timeline's visibility and the Query and Events menu options
- **Call Stack** - the categories hidden by the [category filter](/docs/user-guide/query/CallStack#filtering-by-category), and the modules excluded from **Search symbols**
- **Execution Plan** - whether **Annotations** are shown
- **Trace** - whether the [Trace](/docs/user-guide/query/Trace) pane uses the nested or the flat **Layout**
- **Allocations** - whether the map's **Tooltip** is on

When Internals Viewer is installed as a package, settings are kept in the package's local settings. Otherwise they are in `%LOCALAPPDATA%\InternalsViewer\ApplicationData\LocalSettings.json`.
