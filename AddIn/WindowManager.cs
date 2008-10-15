using System;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals;

namespace InternalsViewer.SSMSAddIn
{
    /// <summary>
    /// Manages SSMS ToolWindow/Document integration
    /// </summary>
    class WindowManager
    {
        private DTE2 applicationObject;
        private EnvDTE.AddIn addInInstance;

        public WindowManager(DTE2 applicationObject, EnvDTE.AddIn addInInstance)
        {
            this.addInInstance = addInInstance;
            this.applicationObject = applicationObject;
        }

        /// <summary>
        /// Creates the allocation window.
        /// </summary>
        /// <returns></returns>
        public AllocationContainer CreateAllocationWindow()
        {
            Guid id = new Guid("{65a48117-79b3-4863-b268-eb7eafc21feb}");

            Windows2 windows2 = applicationObject.Windows as Windows2;

            if (windows2 != null)
            {
                object controlObject = null;
                Assembly asm = Assembly.GetExecutingAssembly();

                Window toolWindow = windows2.CreateToolWindow2(this.addInInstance,
                                                               asm.Location,
                                                               "InternalsViewer.SSMSAddIn.AllocationContainer",
                                                               "Internals Viewer", "{" + id.ToString() + "}",
                                                               ref controlObject);
                // Make the window a tabbed document
                toolWindow.Linkable = false;
                toolWindow.IsFloating = false;

                toolWindow.Visible = true;

                return controlObject as AllocationContainer;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a page viewer window with the application level connection string
        /// </summary>
        /// <param name="pageAddress">The page address.</param>
        /// <returns></returns>
        public PageViewerContainer CreatePageViewerWindow(PageAddress pageAddress)
        {
            return CreatePageViewerWindow(InternalsViewerConnection.CurrentConnection().ConnectionString, pageAddress);
        }

        /// <summary>
        /// Creates a page viewer window with a given connection string and page address
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="pageAddress">The page address.</param>
        /// <returns></returns>
        public PageViewerContainer CreatePageViewerWindow(string connectionString, PageAddress pageAddress)
        {
            Guid id = Guid.NewGuid();

            Windows2 windows2 = applicationObject.Windows as Windows2;

            if (windows2 != null)
            {
                object controlObject = null;
                Assembly asm = Assembly.GetExecutingAssembly();

                Window toolWindow = windows2.CreateToolWindow2(this.addInInstance,
                                                               asm.Location,
                                                               "InternalsViewer.SSMSAddIn.PageViewerContainer",
                                                               "Page Viewer " + pageAddress.ToString(), "{" + id.ToString() + "}",
                                                               ref controlObject);

                // Make the window a tabbed document
                toolWindow.Linkable = false;
                toolWindow.IsFloating = false;

                PageViewerContainer pageViewerContainer = (controlObject as PageViewerContainer);

                pageViewerContainer.Window = toolWindow;
                pageViewerContainer.PageViewerWindow.LoadPage(connectionString, pageAddress);

                toolWindow.Visible = true;

                return pageViewerContainer;
            }
            else
            {
                return null;
            }
        }
    }
}
