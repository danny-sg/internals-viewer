using System.Text;
using InternalsViewer.Connection.BackupFile.Mtf.Blocks.Attributes;

namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;

internal sealed class StartOfDataSetDescriptorBlock : DescriptorBlock
{
    public StartOfDataSetAttributes StartOfDataSetAttributes { get; }
    
    public ushort PasswordEncryptionAlgorithm { get; }
    
    public ushort SoftwareCompressionAlgorithm { get; }
    
    public ushort SoftwareVendorId { get; }
    
    public ushort DataSetNumber { get; }
    
    public string DataSetName { get; }
    
    public string DataSetDescription { get; }
    
    public string DataSetPassword { get; }
    
    public string UserName { get; }
    
    public ulong PhysicalBlockAddress { get; }
    
    public DateTime MediaWriteDate { get; }
    
    public byte SoftwareMajorVersion { get; }
    
    public byte SoftwareMinorVersion { get; }
    
    public sbyte MtfTimeZone { get; }
    
    public byte MtfMinorVersion { get; }
    
    public byte MediaCatalogVersion { get; }

    public StartOfDataSetDescriptorBlock(MtfReader reader): base(reader)
    {
        StartOfDataSetAttributes = (StartOfDataSetAttributes)reader.ReadUInt32();
        PasswordEncryptionAlgorithm = reader.ReadUInt16();
        SoftwareCompressionAlgorithm = reader.ReadUInt16();
        SoftwareVendorId = reader.ReadUInt16();
        DataSetNumber = reader.ReadUInt16();
        DataSetName = reader.ReadVariableLengthString(StartPosition, StringType);
        DataSetDescription = reader.ReadVariableLengthString(StartPosition, StringType);
        DataSetPassword = reader.ReadVariableLengthString(StartPosition, StringType);
        UserName = reader.ReadVariableLengthString(StartPosition, StringType);
        PhysicalBlockAddress = reader.ReadUInt64();
        MediaWriteDate = reader.ReadDate();
        SoftwareMajorVersion = reader.ReadByte();
        SoftwareMinorVersion = reader.ReadByte();
        MtfTimeZone = reader.ReadSByte();
        MtfMinorVersion = reader.ReadByte();
        MediaCatalogVersion = reader.ReadByte();

        ReadStreams(reader);
    }

    public override string ToString()
    {
        return ToString(string.Empty);
    }

    public string ToString(string prefix)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(CommonHeaderToString(prefix));

        stringBuilder.AppendLine($"{prefix}Start of Data Set Block");
        stringBuilder.AppendLine($"{prefix}=======================");

        stringBuilder.AppendLine($"{prefix}Start of Data Set Attributes:   {StartOfDataSetAttributes}");
        stringBuilder.AppendLine($"{prefix}Password Encryption Algorithm:  {PasswordEncryptionAlgorithm}");
        stringBuilder.AppendLine($"{prefix}Software Compression Algorithm: {SoftwareCompressionAlgorithm}");
        stringBuilder.AppendLine($"{prefix}Software Vendor Id:             {SoftwareVendorId}");
        stringBuilder.AppendLine($"{prefix}Data Set Number:                {DataSetNumber}");
        stringBuilder.AppendLine($"{prefix}Data Set Name:                  {DataSetName}");
        stringBuilder.AppendLine($"{prefix}Data Set Description:           {DataSetDescription}");
        stringBuilder.AppendLine($"{prefix}Data Set Password:              {DataSetPassword}");
        stringBuilder.AppendLine($"{prefix}User Name:                      {UserName}");
        stringBuilder.AppendLine($"{prefix}Physical Block Address:         {PhysicalBlockAddress}");
        stringBuilder.AppendLine($"{prefix}Media Write Date:               {MediaWriteDate}");
        stringBuilder.AppendLine($"{prefix}Software Major Version:         {SoftwareMajorVersion}");
        stringBuilder.AppendLine($"{prefix}Software Minor Version:         {SoftwareMinorVersion}");
        stringBuilder.AppendLine($"{prefix}MTF Time Zone:                  {MtfTimeZone}");
        stringBuilder.AppendLine($"{prefix}MTF Minor Version:              {MtfMinorVersion}");
        stringBuilder.AppendLine($"{prefix}Media Catalog Version:          {MediaCatalogVersion}");

        return stringBuilder.ToString();
    }
}