using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals
{
    public class Pfs
    {
        private readonly List<PfsPage> pfsPages;

        public Pfs(Database database, int fileId)
        {
            pfsPages = new List<PfsPage>();

            int pfsCount = (int)Math.Ceiling(database.FileSize(fileId) / (decimal)Database.PfsInterval);

            pfsPages.Add(new PfsPage(database, new PageAddress(fileId, 1)));

            if (pfsCount > 1)
            {
                for (int i = 1; i < pfsCount; i++)
                {
                    pfsPages.Add(new PfsPage(database, new PageAddress(fileId, i * Database.PfsInterval)));
                }
            }
        }

        public Pfs(PfsPage page)
        {
            pfsPages = new List<PfsPage>();
            pfsPages.Add(page);
        }

        public PfsByte PagePfsByte(int page)
        {
            return pfsPages[page / Database.PfsInterval].PfsBytes[page % Database.PfsInterval];
        }
    }
}
