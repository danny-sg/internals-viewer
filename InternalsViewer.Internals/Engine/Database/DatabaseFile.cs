
namespace InternalsViewer.Internals.Engine.Database
{
    public class DatabaseFile
    {
        public DatabaseFile(int fileId, Database database)
        {
            Database = database;
            FileId = fileId;
        }

        public int TotalExtents { get; set; }

        public int TotalPages => TotalExtents * 8;

        public int UsedPages => UsedExtents * 8;

        public int UsedExtents { get; set; }

        public float TotalMb => ((TotalExtents * 64) / 1024F);

        public float UsedMb => ((UsedExtents * 64) / 1024F);

        public int FileId { get; set; }

        public string FileGroup { get; set; }

        public string Name { get; set; }

        public string PhysicalName { get; set; }

        public string FileName => PhysicalName.Substring(PhysicalName.LastIndexOf(@"\") + 1);

        public int Size { get; set; }

        public Database Database { get; set; }

        public void RefreshSize()
        {
            Size = Database.GetSize(this);
        }
    }
}
