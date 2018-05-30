using System;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Tests.Internals.UnitTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InternalsViewer.Tests.Internals.UnitTests
{
    [TestClass]
    public class TextHeaderReaderUnitTests
    {
        [TestMethod]
        public void Can_Read_Data_Header()
        {
            var header = FileHeaderReader.ReadHeader(@".\TestPages\Records\SimpleHeapNullablePage1.txt");

            Assert.AreEqual(new PageAddress(1, 632), header.PageAddress);
            Assert.AreEqual(new PageAddress(0, 0), header.NextPage);
            Assert.AreEqual(new PageAddress(0, 0), header.PreviousPage);
            Assert.AreEqual(PageType.Data, header.PageType);
            Assert.AreEqual(72057594047168512, header.AllocationUnitId);
            Assert.AreEqual(0, header.Level);
            Assert.AreEqual(0, header.IndexId);
            Assert.AreEqual(11, header.SlotCount);
            Assert.AreEqual(6301, header.FreeCount);
            Assert.AreEqual(6273, header.FreeData);
            Assert.AreEqual(131, header.MinLen);
            Assert.AreEqual(0, header.ReservedCount);
            Assert.AreEqual(0, header.XactReservedCount);
            Assert.AreEqual(0, header.TornBits);
            Assert.AreEqual("0x8000", header.FlagBits);
            Assert.AreEqual(645577338, header.ObjectId);
            Assert.AreEqual(72057594041532416, header.PartitionId);
            Assert.AreEqual(new LogSequenceNumber("(34:1864:13)"), header.Lsn);
        }
    }
}
