using System;
using System.Collections.Generic;
using System.Text;
using EnvDTE80;
using EnvDTE;
using InternalsViewer.Internals.Pages;
using System.Reflection;

namespace InternalsViewer.SSMSAddIn
{
    class WindowManager
    {
        private DTE2 applicationObject;
        private EnvDTE.AddIn addInInstance;

        public WindowManager(DTE2 applicationObject, EnvDTE.AddIn addInInstance)
        {
            this.addInInstance = addInInstance;
            this.applicationObject = applicationObject;
        }

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

        public PageViewerContainer CreatePageViewerWindow(PageAddress pageAddress)
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

                toolWindow.Linkable = false;
                toolWindow.IsFloating = false;

                PageViewerContainer pageViewerContainer = (controlObject as PageViewerContainer);

                pageViewerContainer.Window = toolWindow;
                pageViewerContainer.PageViewerWindow.LoadPage(pageAddress);

                toolWindow.Visible = true;

                return pageViewerContainer;
            }
            else
            {
                return null;
            }
        }

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
