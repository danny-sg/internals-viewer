using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using InternalsViewer.Internals.Properties;
using InternalsViewer.Internals.Pages;
using System.Data;

namespace InternalsViewer.Internals
{
    /// <summary>
    /// Set of pages in the server's buffer bool
    /// </summary>
    public class BufferPool
    {
        private readonly List<PageAddress> cleanPages;
        private readonly List<PageAddress> dirtyPages;

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferPool"/> class.
        /// </summary>
        public BufferPool()
        {
            this.cleanPages = new List<PageAddress>();
            this.dirtyPages = new List<PageAddress>();

            this.Refresh();
        }

        /// <summary>
        /// Requeries buffer pool information.
        /// </summary>
        public void Refresh()
        {
            this.cleanPages.Clear();
            this.dirtyPages.Clear();

            if (InternalsViewerConnection.CurrentConnection().CurrentDatabase == null)
            {
                return;
            }

            using (SqlConnection conn = new SqlConnection(InternalsViewerConnection.CurrentConnection().ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(Resources.SQL_Buffer_Pool, conn);
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("database", InternalsViewerConnection.CurrentConnection().CurrentDatabase.Name);

                conn.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (reader.GetBoolean(2))
                    {
                        this.dirtyPages.Add(new PageAddress(reader.GetInt32(0), reader.GetInt32(1)));
                    }

                    this.cleanPages.Add(new PageAddress(reader.GetInt32(0), reader.GetInt32(1)));
                }

                conn.Close();
            }
        }

        /// <summary>
        /// Gets the clean page addresses.
        /// </summary>
        /// <value>The clean page addresses.</value>
        public List<PageAddress> CleanPages
        {
            get { return this.cleanPages; }
        }

        /// <summary>
        /// Gets the dirty page addresses.
        /// </summary>
        /// <value>The dirty page addresses.</value>
        public List<PageAddress> DirtyPages
        {
            get { return this.dirtyPages; }
        }
    }
}
