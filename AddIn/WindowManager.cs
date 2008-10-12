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

        public AllocationWindow CreateAllocationWindow()
        {
            Guid id = new Guid("{65a48117-79b3-4863-b268-eb7eafc21feb}");

            Windows2 windows2 = applicationObject.Windows as Windows2;

            if (windows2 != null)
            {
                object controlObject = null;
                Assembly asm = Assembly.GetExecutingAssembly();

                Window toolWindow = windows2.CreateToolWindow2(this.addInInstance,
                                                               asm.Location,
                                                               "InternalsViewer.SSMSAddIn.AllocationWindow",
                                                               "Internals Viewer", "{" + id.ToString() + "}",
                                                               ref controlObject);
                toolWindow.Linkable = false;
                toolWindow.IsFloating = false;

                toolWindow.Visible = true;

                return controlObject as AllocationWindow;
            }
            else
            {
                return null;
            }
        }

        public PageViewerWindow CreatePageViewerWindow(PageAddress pageAddress)
        {
            Guid id = Guid.NewGuid();

            Windows2 windows2 = applicationObject.Windows as Windows2;

            if (windows2 != null)
            {
                object controlObject = null;
                Assembly asm = Assembly.GetExecutingAssembly();

                Window toolWindow = windows2.CreateToolWindow2(this.addInInstance,
                                                               asm.Location,
                                                               "InternalsViewer.SSMSAddIn.PageViewerWindow",
                                                               "Page Viewer " + pageAddress.ToString(), "{" + id.ToString() + "}",
                                                               ref controlObject);

                toolWindow.Linkable = false;
                toolWindow.IsFloating = false;

                PageViewerWindow pageViewerWindow = (controlObject as PageViewerWindow);

                pageViewerWindow.Window = toolWindow;
                pageViewerWindow.LoadPage(pageAddress);

                toolWindow.Visible = true;

                return pageViewerWindow;
            }
            else
            {
                return null;
            }
        }
    }
}
