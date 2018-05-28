
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
            this.Database = database;
            this.FileId = fileId;
        }

        public int TotalExtents
        {
            get { return this.totalExtents; }
            set { this.totalExtents = value; }
        }

        public int TotalPages
        {
            get { return this.totalExtents * 8; }
        }

        public int UsedPages
        {
            get { return this.usedExtents * 8; }
        }

        public int UsedExtents
        {
            get { return this.usedExtents; }
            set { this.usedExtents = value; }
        }

        public float TotalMb
        {
            get { return ((this.totalExtents * 64) / 1024F); }
        }

        public float UsedMb
        {
            get { return ((this.usedExtents * 64) / 1024F); }
        }

        public int FileId
        {
            get { return this.fileId; }
            set { this.fileId = value; }
        }

        public string FileGroup
        {
            get { return this.fileGroup; }
            set { this.fileGroup = value; }
        }

        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        public string PhysicalName
        {
            get { return this.physicalName; }
            set { this.physicalName = value; }
        }

        public string FileName
        {
            get { return this.physicalName.Substring(physicalName.LastIndexOf(@"\") + 1); }
        }

        public int Size
        {
            get { return this.size; }
            set { this.size = value; }
        }

        public Database Database
        {
            get { return this.database; }
            set { this.database = value; }
        }

        public void RefreshSize()
        {
            this.Size = this.database.GetSize(this);
        }
    }
}
