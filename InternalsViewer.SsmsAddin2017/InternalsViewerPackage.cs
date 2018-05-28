using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InternalsViewer.SsmsAddin2017.Commands;
using Microsoft.VisualStudio.Shell;
using InternalsViewer.SsmsAddin2017.ToolWindowPanes;

namespace InternalsViewer.SsmsAddin2017
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(InternalsViewerPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(InternalsViewerToolWindow), Style = VsDockStyle.Tabbed, DocumentLikeTool = true)]
    [ProvideToolWindow(typeof(PageViewerToolWindow), Style = VsDockStyle.Tabbed, DocumentLikeTool = true, MultiInstances = true)]
    [ProvideAutoLoad("d114938f-591c-46cf-a785-500a82d97410")]
    public sealed class InternalsViewerPackage : Package
    {
        public const string PackageGuidString = "8fb4f60d-ef16-409e-9423-54ff173cb31f";

        public InternalsViewerPackage()
        {
        }

        protected override void Initialize()
        {
            base.Initialize();
            ViewCommand.Initialize(this);

            DelayAddSkipLoadingRegistryKey();
        }

        protected override int QueryClose(out bool canClose)
        {
            AddSkipLoadingRegistryKey();
            return base.QueryClose(out canClose);
        }

        /// <remarks>
        ///Method taken from https://github.com/nicholas-ross/SSMS-Schema-Folders
        /// </remarks>>
        private void DelayAddSkipLoadingRegistryKey()
        {
            var delay = new Timer();

            delay.Tick += (s, e) =>
            {
                delay.Stop();
                AddSkipLoadingRegistryKey();
            };

            delay.Interval = 1000;
            delay.Start();
        }

        private void AddSkipLoadingRegistryKey()
        {
            var key = UserRegistryRoot.CreateSubKey(@"Packages\{" + PackageGuidString + "}");

            key.SetValue("SkipLoading", 1);
        }
    }
}
