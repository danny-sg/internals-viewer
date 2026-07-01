# Internals Viewer 2026

Internals Viewer is a visualisation tool for viewing the internals of the SQL Server Storage Engine.

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

- Windows 10 version 17763.0 or higher
- Tested on SQL Server 2019 - 2025

### Technologies

- C#
- .NET 10.0
- Windows App SDK (WinUI 3)

## Advisory

Use caution when running on any database. Internals Viewer does not make any modifications
to a database, but it is not advisable to run on production servers due to the I/O
overhead and risk of some functions.

Use caution with the Query tracing as the default Clear Buffer Pool will run `DBCC FREEPROCCACHE`
before executing a query.

## Usage

### Connecting to a database

Internals Viewer can either connect to a live database or an offline .MDF file.

#### SQL Server

The `sysadmin` role is required.

Set the instance name, authentication type, User Id and Password if required for the authentication type, select the database to connect to and click **Connect**.

#### Database File

Database files must not be online in a database server as SQL Server holds an exclusive lock on the file.

Internals Viewer opens the database entirely from its own engine so no additional permissions are required.

Click **Browse** and find the offline .mdf file, then click **Open**.

### Allocations

Allocations will display when a database is opened.

The allocations provide a visual map of the individual pages in database file(s), grouped to extents of 8 pages. The lower half displays the allocation names and details and includes links into the objects.

Each different object is colour coded.

#### Allocation Map

Click a page to open it in the Page Viewer.

Use the mouse wheel or scroll bar to move up and down the map. Use Ctrl + mouse wheel to zoom in and out of the allocation map.

#### Command Bar

##### Tooltip

Toggle tooltip on and off. When on hovering over a page will display the page address, extent, PFS status, and the object the page is allocated to.

##### Allocations

Toggle the allocation details on and off

##### Overlay

Selects an overlay for the allocation map:

###### GAM - Global Allocation Map

Extents that are in use/allocated.

###### SGAM - Shared Global Allocation Map

Extents that are partially in use.

###### PFS - Page Free Space

Space usage in individual pages.

###### Buffer Pool

> SQL Server connections only

Pages that are currently in the server buffer pool (memory).

###### DCM - Differential Change Map

Extents that have changed since the last full database backup.

###### BCM - Bulk Change Map

Extents modified by bulk operations since the last transaction log backup.

##### Refresh

Refreshes the allocations, re-reading metadata and allocation pages.

##### Query

Opens Query tracing

##### Page Address

Type in a page address in the format `File Id:Page Id` to open a page.

Right click for options including _Copy to DBCC PAGE_ that will copy the command into the clipboard.

#### Allocation Details

Click on an object to highlight in the allocation map.

##### Key

Colour code for the object

##### Page Count

Number of pages allocated to the object

##### Root Page

The root page entry point to the object. Click to open in the Page Viewer.

##### First Page

The first data page for the object. Click to open in the Page Viewer.

##### First IAM Page

The first IAM (Index Allocation Map) page for the object. Click to open in the Page Viewer.

##### Index

Available if the object is a clustered or non-clustered index. Click to open the Index Viewer.
