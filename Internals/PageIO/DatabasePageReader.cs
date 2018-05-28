using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.PageIO
{
    /// <summary>
    /// Reads a page using a database connection with DBCC PAGE
    /// </summary>
    public class DatabasePageReader : PageReader
    {
        private readonly Dictionary<string, string> headerData = new Dictionary<string, string>();

        public DatabasePageReader(string connectionString, PageAddress pageAddress, int databaseId)
            : base(pageAddress, databaseId)
        {
            ConnectionString = connectionString;
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
            string currentData;
            string currentRow;
            var offset = 0;
            var data = new byte[8192];

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
                                currentRow = reader[3].ToString();
                                currentData = currentRow.Replace(" ", "").Substring(currentRow.IndexOf(":") + 1, 40);

                                for (var i = 0; i < 40; i += 2)
                                {
                                    var byteString = currentData.Substring(i, 2);

                                    if (!byteString.Contains("†") && !byteString.Contains(".") && offset < 8192)
                                    {
                                        if (byte.TryParse(byteString,
                                                          NumberStyles.HexNumber,
                                                          CultureInfo.InvariantCulture, out data[offset]))
                                        {
                                            offset++;
                                        }
                                    }
                                }
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

        /// <summary>
        /// Loads the page  header.
        /// </summary>
        /// <returns></returns>
        public override bool LoadHeader()
        {
            var parsed = true;

            var header = new Header();

            parsed = DatabaseHeaderReader.LoadHeader(headerData, header);

            Header = header;

            return parsed;
        }

        public string ConnectionString { get; set; }
    }
}
