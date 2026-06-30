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

The version on Github uses a self-signing certificate that needs to be installed first before the application is installed.

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
