using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals
{
    public class Database
    {
        public const int AllocationInterval = 511230;
        public const int PfsInterval = 8088;
        private readonly Dictionary<int, Allocation> bcm = new Dictionary<int, Allocation>();
        private readonly int compatibilityLevel;
        private readonly bool compatible;
        private readonly int databaseId;
        private readonly string name;
        private readonly Dictionary<int, Allocation> dcm = new Dictionary<int, Allocation>();
        private readonly Dictionary<int, Allocation> gam = new Dictionary<int, Allocation>();
        private readonly Dictionary<int, Pfs> pfs = new Dictionary<int, Pfs>();
        private readonly Dictionary<int, Allocation> sGam = new Dictionary<int, Allocation>();
        private List<DatabaseFile> files = new List<DatabaseFile>();

        public Database(int databaseId, string name, int state, byte compatibilityLevel)
        {
            this.databaseId = databaseId;
            this.name = name;
            this.compatibilityLevel = compatibilityLevel;

            compatible = (compatibilityLevel >= 90 && state == 0);
            LoadFiles();
        }

        public void Refresh()
        {
            LoadAllocations();
        }

        public int FileSize(int fileId)
        {
            return files.Find(delegate(DatabaseFile file) { return file.FileId == fileId; }).Size;
        }

        private void LoadAllocations()
        {
            foreach (DatabaseFile file in files)
            {
                gam.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 2)));
                sGam.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 3)));
                dcm.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 6)));
                bcm.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 7)));
            }
        }

        private void LoadPfs()
        {
            foreach (DatabaseFile file in files)
            {
                pfs.Add(file.FileId, new Pfs(this, file.FileId));
            }
        }

        public void RefreshPfs(int fileId)
        {
            pfs[fileId] = new Pfs(this, fileId);
        }

        private void LoadFiles()
        {
            string sqlCommand = Properties.Resources.SQL_Files;

            DataTable filesDataTable = DataAccess.GetDataTable(SqlServerConnection.CurrentConnection().ConnectionString, sqlCommand, this.Name, "Files", CommandType.Text);

            foreach (DataRow r in filesDataTable.Rows)
            {
                DatabaseFile file = new DatabaseFile((int)r["file_id"], this);
                file.FileGroup = r["filegroup_name"].ToString();
                file.Name = r["name"].ToString();
                file.PhysicalName = r["physical_name"].ToString();
                file.Size = (int)r["size"];
                file.TotalExtents = (int)r["total_extents"];
                file.UsedExtents = (int)r["used_extents"];

                files.Add(file);
            }
        }

        public DataTable Tables()
        {
            string sqlCommand = Properties.Resources.SQL_Database_Tables;

            return DataAccess.GetDataTable(SqlServerConnection.CurrentConnection().ConnectionString, sqlCommand, Name, "Tables", CommandType.Text);
        }

        public DataTable AllocationUnits()
        {
            string sqlCommand = Properties.Resources.SQL_Allocation_Units;

            return DataAccess.GetDataTable(SqlServerConnection.CurrentConnection().ConnectionString, sqlCommand, Name, "Tables", CommandType.Text);
        }

        public DataTable TableInfo(int objectId)
        {
            return DataAccess.GetDataTable(SqlServerConnection.CurrentConnection().ConnectionString, Properties.Resources.SQL_Table_Info,
                                           Name,
                                           "Tables",
                                           CommandType.Text,
                                           new SqlParameter[1] { new SqlParameter("object_id", objectId) });
        }

        public DataTable TableColumns(int objectId)
        {
            return null;
        }

        internal int GetSize(DatabaseFile databaseFile)
        {
            return (int)DataAccess.GetScalar(Name, Properties.Resources.SQL_File_Size, CommandType.Text, new SqlParameter[1] { new SqlParameter("file_id", databaseFile.FileId) });
        }

        #region Properties

        public int DatabaseId
        {
            get { return databaseId; }
        }

        public string Name
        {
            get { return name; }
        }

        public Dictionary<int, Allocation> Gam
        {
            get
            {
                if (gam.Count == 0)
                {
                    LoadAllocations();
                }
                return gam;
            }
        }

        public Dictionary<int, Allocation> SGam
        {
            get
            {
                if (sGam.Count == 0)
                {
                    LoadAllocations();
                }
                return sGam;
            }
        }

        public Dictionary<int, Allocation> Dcm
        {
            get
            {
                if (dcm.Count == 0)
                {
                    LoadAllocations();
                }
                return dcm;
            }
        }

        public Dictionary<int, Allocation> Bcm
        {
            get
            {
                if (bcm.Count == 0)
                {
                    LoadAllocations();
                }
                return bcm;
            }
        }

        public Dictionary<int, Pfs> Pfs
        {
            get
            {
                if (pfs.Count == 0)
                {
                    LoadPfs();
                }
                return pfs;
            }
        }

        public List<DatabaseFile> Files
        {
            get { return files; }
            set { files = value; }
        }

        public bool Compatible
        {
            get { return compatible; }
        }

        public int CompatibilityLevel
        {
            get { return compatibilityLevel; }
        }

        #endregion
    }
}