# Settings

Settings are opened from the start page. They cover where trace files and symbols are stored, plus the application's diagnostic log. All settings save as soon as they are changed.

## Use Local Directory

Internals Viewer assumes it will usually be run locally, on or near the SQL Server instance being traced.

With **Use Local Directory** on (the default), the `.xel` trace files used for [Query](/docs/user-guide/query) tracing are saved to the **Trace Path**, defaulted to `app directory\InternalsViewer\Traces`. Because this location is managed by Internals Viewer, it can clean the files up after each trace - see **Auto-Delete Trace** below.

With it off, Internals Viewer instead uses the SQL Server log directory for trace files. This lets Internals Viewer be used against a remote instance, but it can no longer clean up the `.xel` files it generates - they will build up over time. The application shows a warning in this mode:

> Trace files will be saved to the SQL Server log directory. Running traces will generate trace files that cannot be deleted post-trace by Internals Viewer. These files should be periodically removed. Alternatively use a local directory that will be cleared as part of the trace process.

### Trace Path

The folder `.xel` trace files are written to. Both Internals Viewer and the SQL Server service need permission to read and write this folder.

### Grant Permissions

Grants the SQL Server service permission to the Trace Path, by enumerating the services on the machine, finding the SQL Server service(s), and granting them access to the folder. This runs automatically the first time a trace is captured, and can be re-run here if the Trace Path changes or permissions need resetting.

### Auto-Delete Trace

On by default. Deletes each trace's `.xel` file once it has been captured and loaded, so trace files don't accumulate in the Trace Path. Only takes effect when **Use Local Directory** is on.

### Maximum Trace Size

The maximum size, in MB, a trace file is allowed to grow to. Large or long-running queries can generate large trace files, so this can be increased if a trace is being cut short.

## Symbols Path

The folder SQL Server's debugging symbols (PDB files) are downloaded to when resolving [Call Stack](/docs/user-guide/query/CallStack) events. Defaults to `C:\Symbols`.

## Diagnostic Log

The activity log from the internals loading pipeline - what Internals Viewer itself is doing as it reads a database. **Open Log** opens the log in its own tab, where it can be filtered by log level, searched, and exported. **Clear Log** empties it.

This is the first place to look if a database fails to load or something doesn't decode as expected.
