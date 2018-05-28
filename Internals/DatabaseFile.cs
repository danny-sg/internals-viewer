
namespace InternalsViewer.Internals
{
    public class DatabaseFile
    {
        public DatabaseFile(int fileId, Database database)
        {
            Database = database;
            FileId = fileId;
        }

        public int TotalExtents { get; set; }

        public int TotalPages
        {
            get { return TotalExtents * 8; }
        }

        public int UsedPages
        {
            get { return UsedExtents * 8; }
        }

        public int UsedExtents { get; set; }

        public float TotalMb
        {
            get { return ((TotalExtents * 64) / 1024F); }
        }

        public float UsedMb
        {
            get { return ((UsedExtents * 64) / 1024F); }
        }

        public int FileId { get; set; }

        public string FileGroup { get; set; }

        public string Name { get; set; }

        public string PhysicalName { get; set; }

        public string FileName
        {
            get { return PhysicalName.Substring(PhysicalName.LastIndexOf(@"\") + 1); }
        }

        public int Size { get; set; }

        public Database Database { get; set; }

        public void RefreshSize()
        {
            Size = Database.GetSize(this);
        }
    }
}
