using System.Text;
using InternalsViewer.Connection.BackupFile.Mtf.Blocks.Attributes;

namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;

internal sealed class TapeHeaderDescriptorBlock : DescriptorBlock
{
    public uint MediaFamilyId { get; }
    
    public TapeAttributes TapeAttributes { get; }
    
    public ushort MediaSequenceNumber { get; }
    
    public ushort PasswordEncryptionAlgorithm { get; }
    
    public ushort SoftFilemarkBlockSize { get; }
    
    public ushort MediaBasedCatalogType { get; }
    
    public string MediaName { get; }
    
    public string MediaDescription { get; }
    
    public string MediaPassword { get; }
    
    public string SoftwareName { get; }
    
    public ushort FormatLogicalBlockSize { get; }
    
    public ushort SoftwareVendorId { get; }
    
    public DateTime MediaDate { get; }
    
    public byte MtfMajorVersion { get; }

    public TapeHeaderDescriptorBlock(MtfReader reader): base(reader)
    {
        MediaFamilyId = reader.ReadUInt32();
        TapeAttributes = (TapeAttributes)reader.ReadUInt32();
        MediaSequenceNumber = reader.ReadUInt16();
        PasswordEncryptionAlgorithm = reader.ReadUInt16();
        SoftFilemarkBlockSize = reader.ReadUInt16();
        MediaBasedCatalogType = reader.ReadUInt16();
        MediaName = reader.ReadVariableLengthString(StartPosition, StringType);
        MediaDescription = reader.ReadVariableLengthString(StartPosition, StringType);
        MediaPassword = reader.ReadVariableLengthString(StartPosition, StringType);
        SoftwareName = reader.ReadVariableLengthString(StartPosition, StringType);
        FormatLogicalBlockSize = reader.ReadUInt16();
        SoftwareVendorId = reader.ReadUInt16();
        MediaDate = reader.ReadDate();
        MtfMajorVersion = reader.ReadByte();

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

        stringBuilder.AppendLine($"{prefix}Tape Header");
        stringBuilder.AppendLine($"{prefix}===========");

        stringBuilder.AppendLine($"{prefix}Media Family Id:               {MediaFamilyId}");
        stringBuilder.AppendLine($"{prefix}Tape Attributes:               {TapeAttributes}");
        stringBuilder.AppendLine($"{prefix}Media Sequence Number:         {MediaSequenceNumber}");
        stringBuilder.AppendLine($"{prefix}Password Encryption Algorithm: {PasswordEncryptionAlgorithm}");
        stringBuilder.AppendLine($"{prefix}Soft Filemark Block Size:      {SoftFilemarkBlockSize}");
        stringBuilder.AppendLine($"{prefix}Media Based Catalog Type:      {MediaBasedCatalogType}");
        stringBuilder.AppendLine($"{prefix}Media Name:                    {MediaName}");
        stringBuilder.AppendLine($"{prefix}Media Description:             {MediaDescription}");
        stringBuilder.AppendLine($"{prefix}Media Password:                {MediaPassword}");
        stringBuilder.AppendLine($"{prefix}Software Name:                 {SoftwareName}");
        stringBuilder.AppendLine($"{prefix}Format Logical Block Size:     {FormatLogicalBlockSize}");
        stringBuilder.AppendLine($"{prefix}Software Vendor Id:            {SoftwareVendorId}");
        stringBuilder.AppendLine($"{prefix}Media Date:                    {MediaDate}");
        stringBuilder.AppendLine($"{prefix}Mtf Major Version:             {MtfMajorVersion}");

        return stringBuilder.ToString();
    }
}