using System;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.SSMSAddIn
{
    /// <summary>
    /// Manages SSMS ToolWindow/Document integration
    /// </summary>
    class WindowManager
    {
        private DTE2 applicationObject;
        private EnvDTE.AddIn addInInstance;
        private WindowEvents events;
        private DecodeContainer decodeContainer;

        public WindowManager(DTE2 applicationObject, EnvDTE.AddIn addInInstance)
        {
            this.addInInstance = addInInstance;
            this.applicationObject = applicationObject;

            this.events = this.applicationObject.DTE.Events.get_WindowEvents(null);

            this.events.WindowActivated += new _dispWindowEvents_WindowActivatedEventHandler(WindowManager_WindowActivated);
        }

        void WindowManager_WindowActivated(Window gotFocus, Window lostFocus)
        {
            if (gotFocus.Object!=null && gotFocus.Object.GetType() == typeof(PageViewerContainer))
            {
                if (this.decodeContainer != null)
                {
                    // If the activated window is a Page Viewer and the decode window is visible link it
                    this.decodeContainer.DecodeWindow.ParentWindow = (gotFocus.Object as PageViewerContainer).PageViewerWindow;
                }
            }
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
        public PageViewerContainer CreatePageViewerWindow(RowIdentifier rowIdentifier)
        {
            return CreatePageViewerWindow(InternalsViewerConnection.CurrentConnection().ConnectionString, rowIdentifier);
        }

        /// <summary>
        /// Creates a page viewer window with a given connection string and page address
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="pageAddress">The page address.</param>
        /// <returns></returns>
        public PageViewerContainer CreatePageViewerWindow(string connectionString, RowIdentifier rowIdentifier)
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
                                                               "Page Viewer " + rowIdentifier.PageAddress.ToString(), "{" + id.ToString() + "}",
                                                               ref controlObject);

                // Make the window a tabbed document
                toolWindow.Linkable = false;
                toolWindow.IsFloating = false;

                PageViewerContainer pageViewerContainer = (controlObject as PageViewerContainer);

                pageViewerContainer.Window = toolWindow;
                pageViewerContainer.WindowManager = this;

                pageViewerContainer.PageViewerWindow.LoadPage(connectionString, rowIdentifier);

                toolWindow.Visible = true;

                pageViewerContainer.PageViewerWindow.SetSlot(rowIdentifier.SlotId);

                return pageViewerContainer;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Creates the allocation window.
        /// </summary>
        /// <returns></returns>
        public DecodeContainer CreateDecodeWindow()
        {
            Guid id = new Guid("{fd35b1e4-c1ca-4b63-8be5-0b005a6def4d}");

            Windows2 windows2 = applicationObject.Windows as Windows2;

            if (windows2 != null)
            {
                object controlObject = null;
                Assembly asm = Assembly.GetExecutingAssembly();

                Window toolWindow = windows2.CreateToolWindow2(this.addInInstance,
                                                               asm.Location,
                                                               "InternalsViewer.SSMSAddIn.DecodeContainer",
                                                               "Internals Viewer", "{" + id.ToString() + "}",
                                                               ref controlObject);
           

                toolWindow.Visible = true;

                toolWindow.Width = 376;
                toolWindow.Height = 135;
                toolWindow.Caption = "Decode and Find";

                this.decodeContainer = controlObject as DecodeContainer; ;

                return this.decodeContainer;
            }
            else
            {
                return null;
            }
        }

    }
}
