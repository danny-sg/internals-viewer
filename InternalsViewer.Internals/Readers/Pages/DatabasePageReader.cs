using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Readers.Headers;

namespace InternalsViewer.Internals.Readers.Pages
{
    /// <inheritdoc />
    /// <summary>
    /// Reads a page using a database connection with DBCC PAGE
    /// </summary>
    public class DatabasePageReader : PageReader
    {
        private readonly Dictionary<string, string> headerData = new Dictionary<string, string>();

        public string ConnectionString { get; set; }

        public DatabasePageReader(string connectionString, PageAddress pageAddress, int databaseId)
            : base(pageAddress, databaseId)
        {
            ConnectionString = connectionString;

            HeaderReader = new DictionaryHeaderReader(headerData);
        }

        /// <summary>
        /// Loads this instance.
        /// </summary>
        public override void Load()
        {
            headerData.Clear();
            Data = LoadDatabasePage();
            LoadHeader();
        }

        /// <summary>
        /// Loads the database page.
        /// </summary>
        /// <returns>
        /// Byte array containing the page data
        /// </returns>
        private byte[] LoadDatabasePage()
        {
            var pageCommand = string.Format(Properties.Resources.SQL_Page,
                                            DatabaseId,
                                            PageAddress.FileId,
                                            PageAddress.PageId,
                                            2);
            var offset = 0;
            var data = new byte[Page.Size];

            using (var conn = new SqlConnection(ConnectionString))
            {
                var cmd = new SqlCommand(pageCommand, conn);
                cmd.CommandType = CommandType.Text;

                try
                {
                    conn.Open();
                    var reader = cmd.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (reader[0].ToString() == "DATA:" && reader[1].ToString().StartsWith("Memory Dump"))
                            {
                                var currentRow = reader[3].ToString();

                                offset = ReadData(currentRow, offset, data);
                            }
                            else if (reader[0].ToString() == "PAGE HEADER:")
                            {
                                headerData.Add(reader[2].ToString(), reader[3].ToString());
                            }
                        }

                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Print(ex.ToString());
                }

                cmd.Dispose();
            }

            return data;
        }
    }
}
