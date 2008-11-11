using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace InternalsViewer.Internals
{
    /// <summary>
    /// Manages the connection to the SQL Server
    /// </summary>
    public class InternalsViewerConnection
    {
        private static InternalsViewerConnection currentServer;
        private string connectionString;
        private int version = 9;
        private readonly List<Database> databases = new List<Database>();

        public List<Database> Databases
        {
            get { return databases; }
        }

        private Database currentDatabase;

        public Database CurrentDatabase
        {
            get
            {
                return currentDatabase;
            }
            set
            {
                this.currentDatabase = value;

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(this.ConnectionString);

                builder.InitialCatalog = this.currentDatabase.Name;

                this.ConnectionString = builder.ToString();
            }
        }

        public int Version
        {
            get { return version; }
            set { version = value; }
        }

        /// <summary>
        /// Returns the current connection, if it exists or creates a new connection object
        /// </summary>
        /// <returns></returns>
        public static InternalsViewerConnection CurrentConnection()
        {
            if (null == currentServer)
            {
                currentServer = new InternalsViewerConnection();
            }

            return currentServer;
        }

        /// <summary>
        /// Sets the current server.
        /// </summary>
        /// <param name="serverName">Name of the server.</param>
        /// <param name="integratedSecurity">if set to <c>true</c> [integrated security].</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        public void SetCurrentServer(string serverName, bool integratedSecurity, string userName, string password)
        {
            string databaseName = "master";

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = serverName;
            builder.ApplicationName = "Internals Viewer";
            builder.InitialCatalog = databaseName;

            if (!integratedSecurity)
            {
                builder.UserID = userName;
                builder.Password = password;
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            this.ConnectionString = builder.ConnectionString;


            using (SqlConnection conn = new SqlConnection(builder.ConnectionString))
            {
                conn.Open();

                CheckVersion(conn);

                CheckSysAdmin(conn);

                conn.Close();
            }

            DataTable databasesDataTable = DataAccess.GetDataTable(InternalsViewerConnection.CurrentConnection().ConnectionString,
                                                                   Properties.Resources.SQL_Databases,
                                                                   "master",
                                                                   "Databases",
                                                                   CommandType.Text);
            databases.Clear();

            foreach (DataRow r in databasesDataTable.Rows)
            {
                databases.Add(new Database(this.ConnectionString,
                                           (int)r["database_id"],
                                           (string)r["name"],
                                           (byte)r["state"],
                                           (byte)r["compatibility_level"]));
            }

            currentDatabase = databases.Find(delegate(Database d) { return d.Name == databaseName; });
        }

        private static void CheckSysAdmin(SqlConnection conn)
        {
            SqlCommand cmd = new SqlCommand(Properties.Resources.SQL_Sysadmin_Check, conn);

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = Properties.Resources.SQL_Sysadmin_Check;

            bool hasSysadmin = (bool)cmd.ExecuteScalar();

            if (!hasSysadmin)
            {
                throw new System.Security.SecurityException("The specified login does not have the required sysadmin role.");
            }
        }

        private void CheckVersion(SqlConnection conn)
        {
            SqlCommand cmd = new SqlCommand(Properties.Resources.SQL_Version, conn);

            SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow);

            if (reader.Read())
            {
                this.Version = int.Parse(reader[0].ToString().Split('.')[0]);
            }

            reader.Close();

            if (version < 9)
            {
                throw new NotSupportedException("This application currently only supports SQL Server 2005 and 2008.");
            }
        }

        /// <summary>
        /// Gets or sets the current server connection string.
        /// </summary>
        /// <value>The connection string.</value>
        public string ConnectionString
        {
            get { return this.connectionString; }
            set { this.connectionString = value; }
        }
    }
}
