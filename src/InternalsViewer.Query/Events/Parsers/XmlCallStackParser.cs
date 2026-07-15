using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.Events.Parsers;

public static class XmlCallStackParser
{
    public static List<CallstackFrame> ParseCallstack(string decoded)
    {
        var frames = new List<CallstackFrame>();
        var xml = decoded.AsSpan();
        var i = 0;

        while (i < xml.Length)
        {
            var offset = xml[i..].IndexOf("<frame".AsSpan(), StringComparison.Ordinal);

            if (offset < 0)
            {
                break;
            }

            i += offset;

            var tagEnd = XmlEventTagParser.FindTagEnd(xml, i);

            if (tagEnd < 0)
            {
                break;
            }

            var tag = xml[i..(tagEnd + 1)];
            
            var module = XmlEventAttributeParser.GetAttribute(tag, "module");

            var addressSpan = XmlEventAttributeParser.GetAttribute(tag, "address");

            var pdb = XmlEventAttributeParser.GetAttribute(tag, "pdb");
            
            var guid = XmlEventAttributeParser.GetAttribute(tag, "guid");
            
            var ageSpan = XmlEventAttributeParser.GetAttribute(tag, "age");

            var rvaSpan = XmlEventAttributeParser.GetAttribute(tag, "rva");

            if (!module.IsEmpty)
            {
                int.TryParse(ageSpan, out var age);

                var rvaValue = !rvaSpan.IsEmpty && rvaSpan.Length > 2 && rvaSpan[1] is 'x' or 'X'
                    ? uint.TryParse(rvaSpan[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex) 
                        ? hex 
                        : 0U
                    : uint.TryParse(rvaSpan, out var dec) ? dec : 0U;

                var address = !addressSpan.IsEmpty && addressSpan.Length > 2 && addressSpan[1] is 'x' or 'X'
                    ? ulong.TryParse(addressSpan[2..], System.Globalization.NumberStyles.HexNumber, null, out var addr)
                        ? addr
                        : 0UL
                    : 0UL;

                frames.Add(new CallstackFrame
                {
                    Module = module.ToString(),
                    Address = address,
                    Pdb = pdb.ToString(),
                    Guid = guid.ToString(),
                    Age = age,
                    Rva = rvaValue
                });
            }

            i = tagEnd + 1;
        }

        return frames;
    }
}