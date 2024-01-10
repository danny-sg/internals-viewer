using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using InternalsViewer.Internals.Tests.Helpers;
using Newtonsoft.Json.Linq;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Readers;

public class BackupReaderTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [Fact]
    public void Can_Parse_File()
    {
        var parser = new MtfParser(TestLogger.GetLogger<MtfParser>(TestOutput));

        parser.Parse(@"C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\AdventureWorks2022.bak");
    }
}

public class MtfParser(ILogger<MtfParser> logger)
{
    public ILogger<MtfParser> Logger { get; } = logger;

    private const int HeaderSize = 512;
    private const int TapeBlockSize = 1024;

    public void Parse(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        byte[] buffer = new byte[HeaderSize];
        
        int bytesRead = fileStream.Read(buffer, 0, HeaderSize);

        if (bytesRead != HeaderSize)
        {
            throw new Exception("Invalid MTF file. Header is incomplete.");
        }

        ParseHeader(buffer);

        buffer = new byte[TapeBlockSize];

        while ((bytesRead = fileStream.Read(buffer, 0, TapeBlockSize)) > 0)
        {
            ParseTapeBlock(buffer);
        }
    }

    private void ParseHeader(byte[] buffer)
    {
        string signature = Encoding.ASCII.GetString(buffer, 0, 4);
        if (signature != "TAPE")
        {
            throw new Exception("Invalid MTF file. Signature does not match.");
        }

        // Continue parsing the rest of the header as per the MTF specification...
    }

    private void ParseTapeBlock(byte[] buffer)
    {
        var blockType = BitConverter.ToUInt32(buffer, 0);

        if (Enum.IsDefined(typeof(BlockType), blockType))
        {
            Logger.LogInformation($"Block Type: {(BlockType)blockType}");
        }
    }

    enum BlockType : uint
    {
        /// <summary>
        /// TAPE descriptor block
        /// </summary>
        MTF_TAPE = 0x45504154,
        /// <summary>
        /// Start of data SET descriptor block
        /// </summary>
        MTF_SSET = 0x54455353,
        /// <summary>
        /// VOLume descriptor Block
        /// </summary>
        MTF_VOLB = 0x424C4F56,
        /// <summary>
        /// DIRectory descriptor Block
        /// </summary>
        MTF_DIRB = 0x42524944,
        /// <summary>
        /// FILE descriptor block
        /// </summary>
        MTF_FILE = 0x454C4946,
        /// <summary>
        /// Corrupt object descriptor block
        /// </summary>
        MTF_CFIL = 0x4C494643,
        /// <summary>
        /// End of Set Pad descriptor Block
        /// </summary>
        MTF_ESPB = 0x42505345,
        /// <summary>
        /// End of SET descriptor block
        /// </summary>
        MTF_ESET = 0x54455345,
        /// <summary>
        /// End Of Tape Marker descriptor block
        /// </summary>
        MTF_EOTM = 0x4D544F45,
        /// <summary>
        /// Soft FileMark descriptor Block
        /// </summary>
        MTF_SFMB = 0x424D4653,
    }
}