using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Database;

public class Database: DatabaseInfo
{
    public const int AllocationInterval = 511232;
    public const int PfsInterval = 8088;

    private readonly Dictionary<int, Allocation> bcm = new();
    private readonly Dictionary<int, Allocation> dcm = new();
    private readonly Dictionary<int, Allocation> gam = new();
    private readonly Dictionary<int, Pfs> pfs = new();
    private readonly Dictionary<int, Allocation> sGam = new();

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

    public List<DatabaseFile> Files { get; set; } = new();

    public bool Compatible { get; }

    public string ConnectionString { get; set; }

    public Database(string connectionString, int databaseId, string name, int state, byte compatibilityLevel)
    {
        ConnectionString = connectionString;
        DatabaseId = databaseId;
        Name = name;
        CompatibilityLevel = compatibilityLevel;

        Compatible = (compatibilityLevel >= 90 && state == 0);
    }

    public void Refresh()
    {
        LoadAllocations();
    }

    public int FileSize(int fileId)
    {
        return Files.Find(file => file.FileId == fileId).Size;
    }

    private void LoadAllocations()
    {
        foreach (var file in Files)
        {
            gam.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 2)));
            sGam.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 3)));
            dcm.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 6)));
            bcm.Add(file.FileId, new Allocation(this, new PageAddress(file.FileId, 7)));
        }
    }

    private void LoadPfs()
    {
        foreach (var file in Files)
        {
            pfs.Add(file.FileId, new Pfs(this, file.FileId));
        }
    }

    public void RefreshPfs(int fileId)
    {
        pfs[fileId] = new Pfs(this, fileId);
    }
}