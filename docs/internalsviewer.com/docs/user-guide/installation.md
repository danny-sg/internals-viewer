# Manual Installation

The releases on GitHub are built from its source code.

The application is packaged as an .msix file. Windows will only install a package that has been signed. The version published in the Microsoft Store is signed with a Microsoft certificate as it has been through a verification process.

The version on Github uses a self-signing certificate that needs to be installed first before the application is installed.

The script Install.ps1 installs the certificate and then installs the .msix package.

Steps:

1. Download the latest release artifacts from the [Releases](https://github.com/danny-sg/internals-viewer/releases) page
2. Extract the files to a folder and navigate to `\internals-viewer-msix-platform\artifacts\msix-package-platform\InternalsViewer.UI.App_version\`
3. Run `powershell -ExecutionPolicy Bypass -File Install.ps1`
- You will be prompted to install the certificate. Accept prompts to continue.