# Security Policy

Internals Viewer reads SQL Server storage structures directly, attaches a debugger to SQL Server and runs
helpers that need administrator rights. It is a tool for people who already have full control of the server
they point it at. This policy sets out what counts as a vulnerability, how to report one, and what the app
does with your data and credentials.

## Use at Your Own Risk

Internals Viewer is provided as is, without warranty of any kind, under the GNU General Public License v3 in
[LICENSE](LICENSE). You run it at your own risk.

Do not run it against a production server. It never modifies a database, but it reads pages directly, which
adds I/O load, and some features go further:

- Clear Buffer Pool, on by default for a traced query, runs `CHECKPOINT` and then `DBCC DROPCLEANBUFFERS`,
  so every database on the instance loses its cached pages.
- Attach WinDbg sets breakpoints in the SQL Server process. The whole instance pauses each time one is hit.
  If the debugger exits without detaching, Windows ends the SQL Server process with it.
- Full Trace restarts the SQL Server service to record it with Time Travel Debugging, which drops every
  connection, and the instance runs many times slower while recording.

Harm caused by running the tool against a server you depend on is not a vulnerability and will not get an
advisory. If you think the app could have warned you better, open an ordinary issue.

## Supported Versions

Fixes are released for the latest version only. The Microsoft Store version updates automatically. If you
installed from a GitHub release, update to the latest release before reporting.

| Version                | Supported |
| ---------------------- | --------- |
| 4.6.x (latest release) | Yes       |
| Earlier                | No        |

## Reporting a Vulnerability

Please do not open a public issue for a security problem.

Report it privately through GitHub:
<https://github.com/danny-sg/internals-viewer/security/advisories/new>

Include:

- The version, and whether it was installed from the Microsoft Store or a GitHub release.
- What you did and what happened.
- Optionally, a file that triggers the problem if there is one (`.mdf`, `.bak`, `.xel`, an execution plan, a `.pdb` or a
  Time Travel Debugging trace), as long as it contains nothing confidential.

Internals Viewer is maintained by one person. You should get an acknowledgement within 7 days. A confirmed
problem is fixed in the next release and published as a GitHub security advisory, with credit to the
reporter unless you ask not to be named. If you have heard nothing after 14 days, open a public issue that
says only that you have sent a report, with no details.

## Scope

In scope:

- Opening a crafted file causes memory corruption, code execution, or reads outside that file. The backup
  file reader (MTF, MS_XPRESS and zstd decompression), the transaction log reader, the Extended Events
  parser, the execution plan parser and the native DIA and Time Travel Debugging bridges are the main
  surface.
- Gaining more privilege than you already have through the elevated helpers, the WinDbg launch or the named
  pipe to the WinDbg session.
- A password or connection string written anywhere it should not be, or sent anywhere other than the SQL
  Server you connected to.
- A network connection other than the ones listed under Data.
- A problem with how releases are built or signed.

Out of scope:

- Anything that needs `sysadmin` on the instance or local administrator on the machine to begin with, where
  the outcome is something that access already allows. The app needs `sysadmin` to read pages and local
  administrator to attach a debugger or record a trace. That access is the point of the tool.
- Harm from running the tool against a production server. See Use at Your Own Risk above.
- Vulnerabilities in SQL Server, WinDbg, Time Travel Debugging, the Windows App SDK or a NuGet package.
  Report those to their maintainers.

## Data

Internals Viewer does not collect any data. There is no telemetry, crash reporting, analytics, account or
update check. Updates come from the Microsoft Store or from the releases in this repository.

The app makes two kinds of network connection:

- To the SQL Server instance you ask it to connect to.
- To the Microsoft public symbol server at `msdl.microsoft.com`, to download `.pdb` symbol files for call
  stacks, and only when you ask it to. The request contains the symbol file name and identity, nothing else.

Everything else stays on your machine:

- Settings are in the package's local settings when installed from the Store or an `.msix`, otherwise in
  `%LOCALAPPDATA%\InternalsViewer\ApplicationData\LocalSettings.json`.
- Symbols are in `C:\Symbols` by default. The folder is set in Settings.
- Extended Events traces are in `%ProgramData%\InternalsViewer\Traces` by default. The folder is set in
  Settings.
- Time Travel Debugging recordings are in `%ProgramData%\InternalsViewer\TimeTravel`.
- WinDbg engine files are in `%LOCALAPPDATA%\InternalsViewer\WinDbg`.

The documentation site at internalsviewer.com is a static site on GitHub Pages with no analytics scripts.

## Credentials

- Windows Authentication is the default and stores no secret.
- With SQL Server Authentication or Active Directory Password, the password is kept with the Recent
  Connections entry so you can reconnect without typing it again. It is encrypted with the Windows Data
  Protection API (DPAPI) for the current user, so only the same Windows account on the same machine can
  decrypt it. If it cannot be decrypted you are prompted for it. The stored connection string never contains
  the password.
- The password for the WinDbg named pipe is generated by the app and stored the same way. It guards the pipe
  to a debugger attached to SQL Server. It also appears on the command line of the WinDbg process the app
  starts, so anyone who can read your process list can join that session.
- The app connects to SQL Server with `TrustServerCertificate=true`. The connection is encrypted but the
  server certificate is not validated, so the app will not detect a server impersonating yours. Connect
  over a network you trust.

## Elevated Components

Attaching WinDbg and recording with Time Travel Debugging need administrator rights and show a UAC prompt.
Both run Microsoft's own tools:

- WinDbg is started elevated as a named pipe server and the app joins the session as a client.
- `InternalsViewer.TraceHarness.exe` is started elevated to record SQL Server with TTD. It checks that the
  TTD binaries are signed by Microsoft before running them, stops and starts the SQL Server service to begin
  and end the recording, and writes the recording under `%ProgramData%\InternalsViewer\TimeTravel`.

Nothing else in the app runs elevated.

## Releases and Signing

- The Microsoft Store version is signed by Microsoft after certification. Prefer it.
- GitHub releases are built by GitHub Actions from the tagged commit and signed with a self-signed
  certificate. The certificate and its password are GitHub Actions encrypted secrets that exist on the build
  runner only during the build. `Install.ps1` adds that certificate to your trusted store, so only run it for
  a package downloaded from the releases in this repository.
- The native components (DIA bridge, Time Travel Debugging bridge and trace harness) are built from the C++
  projects under `src/Query` and shipped from `src/runtimes/win-x64/native`.
- NuGet dependencies are monitored by Dependabot.
