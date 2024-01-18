using System.Collections;
using System.Data;
using System.Text;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Converters.Decoder;

public static class DataDecoder
{
    public static List<DecodeResult> Decode(string value)
    {
        var decodeResults = new List<DecodeResult>();

        var binaryData = value.CleanHex().ToByteArray();

        switch (binaryData.Length)
        {
            case 1:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.TinyInt));
                break;
            case 2:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.SmallInt));
                break;
            case 4:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.Int));
                break;
            case 8:
                decodeResults.Add(GetDecodeResult(binaryData, SqlDbType.BigInt));
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
}
