# Manual Installation

The releases on GitHub are built from its source code.

The application is packaged as an .msix file. Windows will only install a package that has been signed. The version published in the Microsoft Store is signed with a Microsoft certificate as it has been through a verification process.

The version on GitHub uses a self-signing certificate that needs to be installed first before the application is installed.

The script `Install.ps1` installs the certificate and then installs the .msix package.

Steps:

1. Download the latest release from the [Releases](https://github.com/danny-sg/internals-viewer/releases) page - `internals-viewer-msix-x64.zip` for 64-bit Windows
2. Extract the files to a folder and navigate to `artifacts\msix-package-x64\InternalsViewer.UI.App_<version>_x64_Test\`, which holds `Install.ps1`, the certificate and the .msix package
3. Run `powershell -ExecutionPolicy Bypass -File Install.ps1`
4. You will be prompted to install the certificate. Accept the prompts to continue

::: warning
`Install.ps1` adds the release's certificate to the trusted certificates on the machine, so only run it for a package downloaded from this repository's Releases page.
:::

An x86 package is also published, but use the x64 package where you can - the symbol resolution behind the [Call Stack](/docs/user-guide/query/CallStack) only ships for x64.
