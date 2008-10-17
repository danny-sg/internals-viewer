using System;
using System.Data;

namespace InternalsViewer.Internals.Structures
{
    internal class TableStructure : Structure
    {
        internal TableStructure(long allocationUnitId, DataTable structure)
            : base(allocationUnitId, structure)
        {
        }

        internal override void AddColumns(DataTable structure)
        {
            throw new NotImplementedException();
        }

        public override DataTable LoadStructure(long allocationUnitId, Database database)
        {
            throw new NotImplementedException();
        }
    }
}
