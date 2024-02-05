# Internals Viewer 2024

Internals Viewer is a visualisation tool for viewing the internals of the SQL Server Storage Engine.

## Installation

### Microsoft Store

The easiest way to install and recieve automatic updates is to use the Microsoft Store.

Click this link or search for Internals Viewer in the Microsoft Store:

<a href="https://apps.microsoft.com/detail/Internals%20Viewer/9MSW42CQMK2V?launch=true
	&mode=mini">
	<img src="https://get.microsoft.com/images/en-gb%20dark.svg" width="200"/>
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

### Compatability

- Windows 10 version 17763.0 or higher
- Tested on SQL Server 2019 - 2022

When I started doing this I found a couple of things. The first thing was it's not complicated! I was suprised at how accesible all of the information was. The second thing I found was the techniques to view internals were cumbersome. You have to query sys tables, take values and convert them from binary, run a DBCC command, view the results, follow to another page, run another command etc.

Internals Viewer started to make it easier to navigate around the internals of a database and view the data structures. Over time it has implemented more of the interpretation to help with explanation of structures in the user interface.
