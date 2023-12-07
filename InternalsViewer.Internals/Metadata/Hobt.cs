using System;
using System.Collections.Generic;
using System.Data;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Metadata;

/// <summary>
/// HOBT - Heap or B-Tree
/// </summary>
public class Hobt
{
    /// <summary>
    /// Returns if a HOBT is a heap or b-tree
    /// </summary>
    public static StructureType HobtType(string connectionString, string databaseName, string objectName)
    {
        if (Convert.ToBoolean(DataAccess.GetScalar(connectionString,
                databaseName,
                SqlCommands.ObjectHasClusteredIndex,
                CommandType.Text,
                new[] { new SqlParameter("TableName", objectName) })))
        {
            return StructureType.BTree;
        }

        return StructureType.Heap;
    }


    /// <summary>
    /// Returns entry points for a heap
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="database">The database.</param>
    /// <param name="objectName">Name of the object.</param>
    /// <returns></returns>
    public static List<HobtEntryPoint> HeapEntryPoints(string connectionString, string database, string objectName)
    {
        return EntryPoints(connectionString, database, objectName, "-1");
    }

    /// <summary>
    /// Returns the entry points for a HOBT
    /// </summary>
    public static List<HobtEntryPoint> EntryPoints(string connectionString, string database, string objectName, string indexName)
    {
        var entryPoints = new List<HobtEntryPoint>();

        var entryPointDataTable = DataAccess.GetDataTable(connectionString, 
            SqlCommands.EntryPoints,
            database,
            string.Empty,
            CommandType.Text,
            new[] { new SqlParameter("ObjectName", objectName), 
                new SqlParameter("IndexName", indexName) });

        foreach (DataRow row in entryPointDataTable.Rows)
        {
            var firstIam = new PageAddress((byte[])row["first_iam_page"]);
            var rootPage = new PageAddress((byte[])row["root_page"]);
            var firstPage = new PageAddress((byte[])row["first_page"]);
            var partitionNumber = Convert.ToInt32(row["partition_number"]);

            entryPoints.Add(new HobtEntryPoint(firstIam, rootPage, firstPage, partitionNumber));
        }

        return entryPoints;
    }
}