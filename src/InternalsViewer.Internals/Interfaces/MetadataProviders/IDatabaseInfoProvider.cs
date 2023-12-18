using InternalsViewer.Internals.Engine.Database;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IDatabaseInfoProvider
{
    Task<List<DatabaseSummary>> GetDatabases();

    Task<DatabaseSummary?> GetDatabase(string name);

    Task<List<AllocationUnit>> GetAllocationUnits();

    Task<byte> GetCompatibilityLevel(string name);
}