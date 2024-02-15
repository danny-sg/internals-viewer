using InternalsViewer.Internals.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Interfaces.Annotations;

public interface IDataStructure
{
    public List<DataStructureItem> MarkItems { get; }
}
