# Privacy Policy

Last updated: 10 October 2026

Internals Viewer is a desktop tool for looking inside SQL Server databases. It does not collect, store or
share any personal data. This policy explains what the app does with the information it handles, where that
information goes, and what stays on your machine.

## What the App Collects

The application does not collect data. Internals Viewer sends no telemetry, crash reports, analytics or
usage statistics, and has no advertising or user accounts. It has no update check of its own and does not
contact its author. The Microsoft Store keeps the Store version up to date, as described under Where You
Get the App. Nothing you do in the app is sent to the author or to anyone else, except as described under
Network Connections.

The app keeps a technical log for diagnosing problems, which you can read inside the app. It is held in
memory and is not written to disk unless you export it to a file yourself.

## Network Connections

The app connects to two kinds of endpoint, both chosen or triggered by you:

- **The SQL Server instance you connect to.** The app sends it the credentials you enter, the queries it
  runs on your behalf, and the commands it uses to read pages and manage Extended Events sessions. This
  data goes nowhere else.
- **The Microsoft public symbol server** at `msdl.microsoft.com`, only when you ask the app to download
  symbols for call stacks. The request contains the name and identity of the symbol file. Microsoft
  receives the request, including your IP address, under the
  [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).

Opening a database file or a backup file reads it from disk and makes no network connection.

## Information Stored on Your Machine

The app keeps the following on your machine so that it works between sessions. None of it leaves your
machine through the app.

- **Settings**, including instance names, database names, user names and the folders you have chosen.
- **Recent Connections.** With SQL Server Authentication or Active Directory Password, the password is
  kept so you can reconnect without typing it again. It is encrypted with the Windows Data Protection API
  for the current Windows user, so only that user on that machine can decrypt it.
- **Query History** for each database, holding the text of the queries you have run in the app.
- **Page Bookmarks.**
- **Extended Events trace files** for each traced query, holding the query text, its execution plan, the
  events raised while it ran and their call stacks.
- **Time Travel Debugging recordings**, when you use Full Trace. A recording captures everything the SQL
  Server process does while it runs, including data held in memory. Treat it as containing the data in
  your databases.
- **Symbol files** downloaded from Microsoft.

Where each of these lives is listed in [SECURITY.md](SECURITY.md). You can delete them at any time. Recent
Connections and Query History can be cleared from inside the app. Uninstalling the Microsoft Store version
removes its settings. Trace files, recordings and symbol files are left in place for you to delete.

## Tools the App Starts

Attach WinDbg and Full Trace start Microsoft's WinDbg and Time Travel Debugging on your machine with
administrator rights. Those tools are Microsoft's. The Microsoft Privacy Statement applies to them,
including any diagnostic data they send to Microsoft under their own settings.

## Where You Get the App

- **Microsoft Store.** Microsoft handles the download and installation, and checks for and installs
  updates, under the Microsoft Privacy Statement. The author sees aggregated Store statistics such as
  acquisition counts, with nothing that identifies you.
- **GitHub.** Downloading a release or visiting the repository is covered by the
  [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).

## The Documentation Site

[internalsviewer.com](https://internalsviewer.com) is a static site hosted on GitHub Pages. It sets no
cookies and uses no analytics or third-party scripts. It remembers your light or dark theme choice in your
browser's local storage. GitHub may collect visitor IP addresses in its logs to maintain the security and
integrity of its service, as described in the GitHub Privacy Statement.

## Changes

Changes to this policy are made in this repository, where its history is visible, and the date at the top
is updated.

## Contact

For questions about privacy, open an issue at
<https://github.com/danny-sg/internals-viewer/issues>. For a security problem, use the process in
[SECURITY.md](SECURITY.md) instead.
