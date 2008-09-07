
namespace InternalsViewer.Internals
{
    public class DatabaseFile
    {
        private string fileGroup;
        private int fileId;
        private string name;
        private string physicalName;
        private int size;
        private int totalExtents;
        private int usedExtents;
        private Database database;

        public DatabaseFile(int fileId, Database database)
        {
            Database = database;
            FileId = fileId;
        }

        public int TotalExtents
        {
            get { return totalExtents; }
            set { totalExtents = value; }
        }

        public int TotalPages
        {
            get { return totalExtents * 8; }
        }

        public int UsedPages
        {
            get { return usedExtents * 8; }
        }

        public int UsedExtents
        {
            get { return usedExtents; }
            set { usedExtents = value; }
        }

        public float TotalMb
        {
            get { return ((totalExtents * 64) / 1024F); }
        }

        public float UsedMb
        {
            get { return ((usedExtents * 64) / 1024F); }
        }

        public int FileId
        {
            get { return fileId; }
            set { fileId = value; }
        }

        public string FileGroup
        {
            get { return fileGroup; }
            set { fileGroup = value; }
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public string PhysicalName
        {
            get { return physicalName; }
            set { physicalName = value; }
        }

        public string FileName
        {
            get { return physicalName.Substring(physicalName.LastIndexOf(@"\") + 1); }
        }

        public int Size
        {
            get { return size; }
            set { size = value; }
        }

        public Database Database
        {
            get { return database; }
            set { database = value; }
        }

        public void RefreshSize()
        {
            this.Size = database.GetSize(this);
        }
    }
}
