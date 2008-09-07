using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Structures
{
    /// <summary>
    /// Heap or B-Tree
    /// </summary>
    public class Hobt
    {
        /// <summary>
        /// Returns if a HOBT is a heap or b-tree
        /// </summary>
        /// <param name="databaseName">Name of the database.</param>
        /// <param name="objectName">Name of the object.</param>
        /// <returns></returns>
        public static StructureType HobtType(string databaseName, string objectName)
        {
            if (Convert.ToBoolean(DataAccess.GetScalar(databaseName,
                                                       Properties.Resources.SQL_ObjectHasClusteredIndex,
                                                       System.Data.CommandType.Text,
                                                       new System.Data.SqlClient.SqlParameter[] { new SqlParameter("TableName", objectName) })))
            {
                return StructureType.BTree;
            }
            else
            {
                return StructureType.Heap;
            }
        }


        /// <summary>
        /// Returns entry points for a heap
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="objectName">Name of the object.</param>
        /// <returns></returns>
        public static List<HobtEntryPoint> HeapEntryPoints(string database, string objectName)
        {
            return EntryPoints(database, objectName, "-1");
        }

        /// <summary>
        /// Returns the entry points for a HOBT
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="objectName">Name of the object.</param>
        /// <param name="indexName">Name of the index.</param>
        /// <returns></returns>
        public static List<HobtEntryPoint> EntryPoints(string database, string objectName, string indexName)
        {
            List<HobtEntryPoint> entryPoints = new List<HobtEntryPoint>();

            DataTable entryPointDataTable = DataAccess.GetDataTable(SqlServerConnection.CurrentConnection().ConnectionString, 
                                                                    Properties.Resources.SQL_EntryPoints,
                                                                    database,
                                                                    string.Empty,
                                                                    CommandType.Text,
                                                                    new SqlParameter[] { new SqlParameter("ObjectName", objectName), 
                                                                                         new SqlParameter("IndexName", indexName) });

            foreach (DataRow row in entryPointDataTable.Rows)
            {
                PageAddress firstIam = new PageAddress((byte[])row["first_iam_page"]);
                PageAddress rootPage = new PageAddress((byte[])row["root_page"]);
                PageAddress firstPage = new PageAddress((byte[])row["first_page"]);
                int partitionNumber = Convert.ToInt32(row["partition_number"]);

                entryPoints.Add(new HobtEntryPoint(firstIam, rootPage, firstPage, partitionNumber));
            }

            return entryPoints;
        }
    }
}

