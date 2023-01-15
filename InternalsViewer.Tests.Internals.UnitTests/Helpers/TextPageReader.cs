using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Tests.Internals.UnitTests.Helpers
{
    public class FilePageReader
    {
        public static byte[] ReadPage(string path)
        {
            var pageString = File.ReadAllText(path);

            var reader = new TextPageReader(pageString);

            reader.Load();

            return reader.Data;
        }
    }
}
