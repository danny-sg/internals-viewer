using System.Collections;
using System.Data;
using System.Text;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Converters.Decoder;

public static class DataDecoder
{
    public static List<DecodeResult> Decode(string value) => Decode(value.CleanHex().ToByteArray());

    public static List<DecodeResult> Decode(byte[] binaryData)
    {
        var decodeResults = new List<DecodeResult>();

        switch (binaryData.Length)
        {
            case 1:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.TinyInt));
                break;
            case 2:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.SmallInt));
                decodeResults.Add(GetDecodeResult(binaryData, typeof(ushort)));
                break;
            case 3:
                decodeResults.Add(GetDecodeResult(Extend(binaryData, sizeof(int), isSigned: true), SqlDbType.Int));
                decodeResults.Add(GetDecodeResult(Extend(binaryData, sizeof(uint), isSigned: false), typeof(uint)));
                break;
            case 4:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.Int));
                decodeResults.Add(GetDecodeResult(binaryData, typeof(uint)));
                break;
            case 5:
            case 6:
            case 7:
                decodeResults.Add(GetDecodeResult(Extend(binaryData, sizeof(long), isSigned: true), SqlDbType.BigInt));
                decodeResults.Add(GetDecodeResult(Extend(binaryData, sizeof(ulong), isSigned: false), typeof(ulong)));
                break;
            case 8:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.BigInt));
                decodeResults.Add(GetDecodeResult(binaryData, typeof(ulong)));
                break;
        }

        if (binaryData.Length == 1)
        {
            var bitArray = new BitArray(binaryData);

            var stringBuilder = new StringBuilder();

            for (var i = 0; i < 8; i++)
            {
                stringBuilder.Insert(0, bitArray[i] ? "1" : "0");
            }
            
            decodeResults.Add(new DecodeResult("binary", stringBuilder.ToString()));
        }

        var varcharData = GetDecodeResult(binaryData, SqlDbType.VarChar);

        if (!string.IsNullOrEmpty(varcharData.Value))
        {
            decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.VarChar));
        }

        return decodeResults;
    }

    private static DecodeResult GetDecodeResult(byte[] data, SqlDbType sqlType)
    {
        string value;

        try
        {
            value = DataConverter.BinaryToString(data, sqlType);
        }
        catch
        {
            value = "(Error)";
        }

        return new DecodeResult(sqlType.ToString(), value);
    }

    private static DecodeResult GetDecodeResult(byte[] data, Type type)
        => new(type.Name, DataConverter.BinaryToString(data, type));

    private static byte[] Extend(byte[] data, int size, bool isSigned)
    {
        var extended = new byte[size];

        data.CopyTo(extended, 0);

        if (isSigned && data[^1] >= 0x80)
        {
            extended.AsSpan(data.Length).Fill(0xFF);
        }

        return extended;
    }
}
