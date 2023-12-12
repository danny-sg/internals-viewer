using System;
using System.Collections.Generic;
using System.Data;
using System.Security;
using InternalsViewer.Internals.Engine.Database;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals;

/// <summary>
/// Manages the connection to the SQL Server
/// </summary>
public class InternalsViewerConnection
{
    private static InternalsViewerConnection currentServer;

    public List<Database> Databases { get; } = new();

    private Database currentDatabase;

    public Database CurrentDatabase
    {
        get
        {
            return currentDatabase;
        }
        set
        {
            currentDatabase = value;

            var builder = new SqlConnectionStringBuilder(ConnectionString);

            builder.InitialCatalog = currentDatabase.Name;

            ConnectionString = builder.ToString();
        }
    }

    public int Version { get; set; } = 9;

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
        var databaseName = "master";

        var builder = new SqlConnectionStringBuilder();

        builder.DataSource = serverName;
        builder.ApplicationName = "Internals Viewer";
        builder.InitialCatalog = databaseName;
        builder.TrustServerCertificate = true;

        if (!integratedSecurity)
        {
            builder.UserID = userName;
            builder.Password = password;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        ConnectionString = builder.ConnectionString;


        using (var conn = new SqlConnection(builder.ConnectionString))
        {
            conn.Open();

            CheckVersion(conn);

            CheckSysAdmin(conn);

            conn.Close();
        }

        //var databasesDataTable = DataAccess.GetDataTable(CurrentConnection().ConnectionString,
        //    SqlCommands.Databases,
        //    "master",
        //    "Databases",
        //    CommandType.Text);
        //Databases.Clear();

        //foreach (DataRow r in databasesDataTable.Rows)
        //{
        //    Databases.Add(new Database(ConnectionString,
        //        (int)r["database_id"],
        //        (string)r["name"],
        //        (byte)r["state"],
        //        (byte)r["compatibility_level"]));
        //}

        //currentDatabase = Databases.Find(d => d.Name == databaseName);
    }

    private static void CheckSysAdmin(SqlConnection conn)
    {
        var cmd = new SqlCommand(SqlCommands.SysAdminCheck, conn);

        cmd.CommandType = CommandType.Text;
        
        var hasSysadmin = (bool)cmd.ExecuteScalar();

        if (!hasSysadmin)
        {
            throw new SecurityException("The specified login does not have the required sysadmin role.");
        }
    }

    private void CheckVersion(SqlConnection conn)
    {
        var cmd = new SqlCommand(SqlCommands.Version, conn);

        var reader = cmd.ExecuteReader(CommandBehavior.SingleRow);

        if (reader.Read())
        {
            Version = int.Parse(reader[0].ToString().Split('.')[0]);
        }

        reader.Close();

        if (Version < 9)
        {
            throw new NotSupportedException("This application currently only supports SQL Server 2005+");
        }
    }

    /// <summary>
    /// Gets or sets the current server connection string.
    /// </summary>
    /// <value>The connection string.</value>
    public string ConnectionString { get; set; }
}