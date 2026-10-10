# Internals Viewer

Internals Viewer is a visualisation tool for viewing the internals of the SQL Server Storage Engine.

[Internals Viewer Medium Articles](https://medium.com/internals-viewer)

## Version 4.6 - New Features

- Query
  - Full Trace - record a query with Time Travel Debugging for a complete call tree
    - Flame Chart of the recorded calls per thread, with the plan operators above them
    - Memory allocated per call and per plan operator
  - Columnstore
    - Timeline band for segment scans, rowgroup reads and elimination, object pool lookups and filters
    - Aggregate pushdown and filter events, with plan annotations
    - Parallel rowgroup reads shown per thread
  - Call Stack
    - Filter the tree to a node, and by category
    - Activity bands and operator rows
  - Events pane details for every property of an event
  - Index pane - zoom to page, levels overlay, page data with previous/next navigation
- Settings
  - Full columnstore allocation resolution
  - Time travel warning

## Installation

### Microsoft Store

The easiest way to install and receive automatic updates is to use the Microsoft Store.

Click this link or search for Internals Viewer in the Microsoft Store:

<a href="https://get.microsoft.com/installer/download/9MSW42CQMK2V?referrer=appbadge" target="_self" >
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

### Manual Installation

The releases on GitHub are built from its source code.

The application is packaged as an .msix file. Windows will only install a package that has been signed. The version from the Microsoft Store is signed with a Microsoft certificate as it has been through a verification process.

The version on GitHub uses a self-signing certificate that needs to be installed first before the application is installed.

The script `Install.ps1` installs the certificate and then installs the .msix package.

Steps:

1. Download the latest release artifacts from [Releases](https://github.com/danny-sg/internals-viewer/releases)
2. Extract the files to a folder and navigate to \internals-viewer-msix-platform\artifacts\msix-package-platform\InternalsViewer.UI.App_version\
3. Run `powershell -ExecutionPolicy Bypass -File Install.ps1`
4. You will be prompted to install the certificate. Accept prompts to continue.

### Compatibility

- Windows 10 version 2004 (build 19041) or higher, or Windows 11
- Tested on SQL Server 2019 - 2025

### Technologies

- C#
- .NET 10.0
- Windows App SDK (WinUI 3)

## Advisory

Use caution when running on any database. Internals Viewer does not make any modifications
to a database, but it is not advisable to run on production servers due to the I/O
overhead and risk of some functions.

Use caution with Query tracing - the **Clear Buffer Pool** option runs `CHECKPOINT` and `DBCC DROPCLEANBUFFERS`
before executing a query, emptying the buffer pool for the whole server.

## Usage

### Connecting to a database

Internals Viewer can connect to a live database, open the data files of a detached or offline database, or open a full database backup (.bak) without restoring it.

#### SQL Server

The `sysadmin` role is required.

Set the instance name, authentication type, User Id and Password if required for the authentication type, select the database to connect to and click **Connect**.
