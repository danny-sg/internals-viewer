using InternalsViewer.Internals.Engine.Address;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Engine.Indexes;

public class IndexNode(PageAddress pageAddress)
{
    public PageAddress PageAddress { get; set; } = pageAddress;

    public List<PageAddress> Parents { get; set; } = new();

    public int Level { get; set; }

    public int Ordinal { get; set; }
}
