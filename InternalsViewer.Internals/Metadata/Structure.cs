using System.Collections.Generic;
using System.Data;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Metadata
{
    public abstract class Structure
    {
        protected Structure(long allocationUnitId, Database database)
        {
            Columns = new List<Column>();
            AllocationUnitId = allocationUnitId;
            StructureDataTable = GetStructure(allocationUnitId, database);
        }

        internal abstract void AddColumns(DataTable structure);

        public abstract DataTable GetStructure(long allocationUnitId, Database database);

        public long AllocationUnitId { get; set; }

        public DataTable StructureDataTable { get; set; }

        public List<Column> Columns { get; }

        public bool HasSparseColumns { get; set; }
    }
}
