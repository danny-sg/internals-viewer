using System;
using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.TransactionLog;

public class LogMonitor
{
    public static LogSequenceNumber StartMonitoring(string connectionString, string database)
    {
        Checkpoint(connectionString, database);

        var bootPage = new BootPage();

        return bootPage.CheckpointLsn;
    }

    public static DataTable StopMonitoring(string database, LogSequenceNumber startLsn, string connectionString)
    {
        //var logTable = DataAccess.GetDataTable(connectionString,
        //    SqlCommands.TransactionLog,
        //    database,
        //    "Transaction Log",
        //    CommandType.Text,
        //    new SqlParameter[] { new("begin", startLsn.ToDecimal()) });

        //logTable.Columns.Add(new DataColumn("PageAddress", typeof(PageAddress)));

        //foreach (DataRow row in logTable.Rows)
        //{
        //    var pageAddress = row["PageId"].ToString();

        //    if (!string.IsNullOrEmpty(pageAddress))
        //    {
        //        var page = PageAddress.Parse(row["PageId"].ToString());

        //        row["PageAddress"] = page;
        //    }
        //}

        //return logTable;
        throw new NotImplementedException();
    }

    private static void Checkpoint(string connectionString, string database)
    {
        // DataAccess.ExecuteNonQuery(connectionString, SqlCommands.Checkpoint, database, CommandType.Text);
    }
}