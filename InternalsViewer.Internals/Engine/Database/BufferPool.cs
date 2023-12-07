using System.Collections.Generic;
using System.Data;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// Set of pages in the server's buffer bool
/// </summary>
public class BufferPool
{
    /// <summary>
    /// Gets the clean page addresses.
    /// </summary>
    public List<PageAddress> CleanPages { get; }

    /// <summary>
    /// Gets the dirty page addresses.
    /// </summary>
    public List<PageAddress> DirtyPages { get; }

    public BufferPool()
    {
        CleanPages = new List<PageAddress>();
        DirtyPages = new List<PageAddress>();

        Refresh();
    }

    /// <summary>
    /// Re-queries buffer pool information.
    /// </summary>
    public void Refresh()
    {
        CleanPages.Clear();
        DirtyPages.Clear();

        if (InternalsViewerConnection.CurrentConnection().CurrentDatabase == null)
        {
            return;
        }

        using var conn = new SqlConnection(InternalsViewerConnection.CurrentConnection().ConnectionString);

        var cmd = new SqlCommand(SqlCommands.BufferPool, conn);
        
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.AddWithValue("database", InternalsViewerConnection.CurrentConnection().CurrentDatabase.Name);

        conn.Open();

        var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            if (reader.GetBoolean(2))
            {
                DirtyPages.Add(new PageAddress(reader.GetInt32(0), reader.GetInt32(1)));
            }

            CleanPages.Add(new PageAddress(reader.GetInt32(0), reader.GetInt32(1)));
        }

        conn.Close();
    }
}