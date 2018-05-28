using System;
using System.Data.SqlClient;
using InternalsViewer.Internals;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.SqlServer.Management.UI.VSIntegration;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;

namespace InternalsViewer.SSMSAddIn
{
    class ConnectionManager
    {
        private UIConnectionInfo currentUIConnection;
        private SqlConnectionInfo currentConnection;

        /// <summary>
        /// Gets the active window connection.
        /// </summary>
        /// <returns></returns>
        internal SqlConnectionInfo GetActiveWindowConnection()
        {
            SqlConnectionInfo info = null;

            try
            {
                UIConnectionInfo connInfo = null;

                if (ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo != null)
                {
                    connInfo = ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo.UIConnectionInfo;
                }

                if (connInfo != null)
                {
                    if (connInfo == currentUIConnection)
                    {
                        return currentConnection;
                    }
                    else
                    {
                        info = CreateSqlConnectionInfo(connInfo);

                        currentConnection = info;
                        currentUIConnection = connInfo;
                    }
                }

                if (info == null)
                {
                    INodeInformation[] nodes = GetObjectExplorerSelectedNodes();

                    if (nodes.Length > 0)
                    {
                        info = nodes[0].Connection as SqlConnectionInfo;
                    }
                }

                return info;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        /// <summary>
        /// Connects the internals viewer singleton
        /// </summary>
        /// <param name="connectionInfo">The connection info.</param>
        internal static void ConnectInternalsViewer(UIConnectionInfo connectionInfo)
        {
            ConnectInternalsViewer(ConnectionManager.CreateSqlConnectionInfo(connectionInfo));
        }

        /// <summary>
        /// Connects the internals viewer singleton
        /// </summary>
        /// <param name="connection">The connection.</param>
        internal static void ConnectInternalsViewer(SqlConnectionInfo connection)
        {
            InternalsViewerConnection.CurrentConnection().SetCurrentServer(connection.ServerName, 
                                                                     connection.UseIntegratedSecurity, 
                                                                     connection.UserName, 
                                                                     connection.Password);
        }

        /// <summary>
        /// Gets the connection string from a UIConnectionInfo object
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
        internal static string GetConnectionString(UIConnectionInfo connection)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = connection.ServerName;
            builder.IntegratedSecurity = string.IsNullOrEmpty(connection.Password);
            builder.Password = connection.Password;
            builder.UserID = connection.UserName;
            builder.InitialCatalog = connection.AdvancedOptions["DATABASE"] ?? "master";
            builder.ApplicationName = "Internals Viewer";

            return builder.ToString();
        }

        /// <summary>
        /// Gets the object explorer selected nodes.
        /// </summary>
        /// <returns></returns>
        private INodeInformation[] GetObjectExplorerSelectedNodes()
        {
            IObjectExplorerService objExplorer = ServiceCache.GetObjectExplorer();
            int arraySize;
            INodeInformation[] nodes;

            objExplorer.GetSelectedNodes(out arraySize, out nodes);
            
            return nodes;
        }

        /// <summary>
        /// Creates a SqlConnectionInfo object from a UIConnectionInfo object
        /// </summary>
        /// <param name="connectionInfo">The connection info.</param>
        /// <returns></returns>
        private static SqlConnectionInfo CreateSqlConnectionInfo(UIConnectionInfo connectionInfo)
        {
            SqlConnectionInfo sqlConnInfo = new SqlConnectionInfo();

            sqlConnInfo.ServerName = connectionInfo.ServerName;
            sqlConnInfo.UserName = connectionInfo.UserName;

            if (string.IsNullOrEmpty(connectionInfo.Password))
            {
                sqlConnInfo.UseIntegratedSecurity = true;
            }
            else
            {
                sqlConnInfo.Password = connectionInfo.Password;
            }

            return sqlConnInfo;
        }


    }
}
