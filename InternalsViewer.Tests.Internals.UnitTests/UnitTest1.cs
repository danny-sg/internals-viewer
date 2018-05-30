using System;
using InternalsViewer.Tests.Internals.UnitTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InternalsViewer.Tests.Internals.UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var d = FilePageReader.ReadPage(@".\TestPages\Records\SimpleHeapNullablePage1.txt");

        }
    }
}
