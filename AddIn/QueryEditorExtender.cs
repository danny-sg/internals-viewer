using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using InternalsViewer.Internals;
using InternalsViewer.UI;
using Microsoft.SqlServer.Management.UI.VSIntegration;

namespace InternalsViewer
{
    class QueryEditorExtender
    {
        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr i);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        CommandEvents queryExecute;
        private DTE2 dte;
        private LogSequenceNumber startLsn;

        public QueryEditorExtender(DTE2 dte)
        {
            this.dte = dte;

            queryExecute = dte.Events.get_CommandEvents("{52692960-56BC-4989-B5D3-94C47A513E8D}", 1);

            queryExecute.BeforeExecute -= new _dispCommandEvents_BeforeExecuteEventHandler(QueryExecute_BeforeExecute);
            queryExecute.AfterExecute -= new _dispCommandEvents_AfterExecuteEventHandler(QueryExecute_AfterExecute);

            queryExecute.BeforeExecute += new _dispCommandEvents_BeforeExecuteEventHandler(QueryExecute_BeforeExecute);
            queryExecute.AfterExecute += new _dispCommandEvents_AfterExecuteEventHandler(QueryExecute_AfterExecute);
        }

        public static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            List<IntPtr> windowPointers = new List<IntPtr>();

            GCHandle listHandle = GCHandle.Alloc(windowPointers);

            try
            {
                EnumWindowProc childProc = new EnumWindowProc(EnumWindow);

                EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                {
                    listHandle.Free();
                }
            }

            return windowPointers;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (list == null)
            {
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            }

            list.Add(handle);

            return true;
        }

        public delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

        void QueryExecute_AfterExecute(string Guid, int ID, object CustomIn, object CustomOut)
        {
            string database = ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo.UIConnectionInfo.AdvancedOptions["DATABASE"];
            string connectionString = ConnectionManager.GetConnectionString(ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo.UIConnectionInfo);
            // Get all window pointers for the app
            List<IntPtr> pointers = GetChildWindows(ServiceCache.MainShellWindow.Handle);

            // Get the active doc window
            Window w = dte.ActiveDocument.Windows.Item(1);

            if (w.Document != null)
            {
                foreach (IntPtr ptr in pointers)
                {
                    // Try and match the windows based on the caption
                    if (w.Caption.StartsWith(GetText(ptr)))
                    {
                        // Enumerate through the window pointers to find the tab control
                        foreach (IntPtr controlPtr in GetChildWindows(ptr))
                        {
                            if (ClassName(controlPtr).StartsWith("WindowsForms10.SysTabControl32.app.0."))
                            {
                                TabControl c = (TabControl)Control.FromHandle(controlPtr);

                                TransactionLogTabPage transactionLogTabPage = new TransactionLogTabPage();

                                transactionLogTabPage.SetTransactionLogData(TransactionLog.StopMonitoring(database, startLsn, connectionString));
                                c.TabPages.Add(transactionLogTabPage);

                                return;
                            }
                        }
                    }
                }
            }
        }

        void QueryExecute_BeforeExecute(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            string database = ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo.UIConnectionInfo.AdvancedOptions["DATABASE"];
            string connectionString = ConnectionManager.GetConnectionString(ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo.UIConnectionInfo);

            this.startLsn = TransactionLog.StartMonitoring(connectionString, database);
        }

        public string GetText(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public string ClassName(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(100);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
