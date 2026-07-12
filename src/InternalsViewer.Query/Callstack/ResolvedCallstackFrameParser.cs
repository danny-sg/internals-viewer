using InternalsViewer.Query.Callstack.Categories;

namespace InternalsViewer.Query.Callstack;

public class ResolvedCallstackFrameParser
{
    public static ResolvedCallstackFrame Parse(string module, string value)
    {
        var plusIndex = value.LastIndexOf('+');

        var symbolPart = plusIndex >= 0
            ? value[..plusIndex]
            : value;

        var offsetPart = plusIndex >= 0
            ? value[(plusIndex + 1)..]
            : null;

        var separator = FindClassMethodSeparator(symbolPart);

        var className = separator >= 0
                        ? symbolPart[..separator].Replace("`", string.Empty).Replace("'", string.Empty)
                        : null;

        var methodName = separator >= 0 ? symbolPart[(separator + 2)..] : symbolPart;

        uint? offset = null;

        if (!string.IsNullOrEmpty(offsetPart))
        {
            offsetPart = offsetPart.Trim();

            if (offsetPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                offsetPart = offsetPart[2..];
            }

            if (uint.TryParse(offsetPart,
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out var offsetValue))
            {
                offset = offsetValue;
            }
        }

        var mappings = CategoryMappings.Default;

        var (symbolCategory, iterator) = mappings.Classify(module, className, methodName);

        return new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = methodName,
            Module = module,
            Offset = offset,
            RawSymbol = value,
            ModuleCategory = mappings.GetModuleCategory(module),
            SymbolCategory = symbolCategory,
            Iterator = iterator
        };
    }

    /// <summary>
    /// Finds the last "::" that separates the class from the method, ignoring any "::"
    /// nested inside template/lambda angle brackets (e.g. lambda names like
    /// CQDSManager::Method&lt;`Outer::Inner'::`2'::&lt;lambda_1&gt; &gt;).
    /// </summary>
    private static int FindClassMethodSeparator(string symbolPart)
    {
        var depth = 0;
        var lastSeparator = -1;

        for (var i = 0; i < symbolPart.Length; i++)
        {
            switch (symbolPart[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    if (depth > 0)
                    {
                        depth--;
                    }
                    break;
                case ':' when depth == 0 && i + 1 < symbolPart.Length && symbolPart[i + 1] == ':':
                    lastSeparator = i;
                    i++;
                    break;
            }
        }

        return lastSeparator;
    }
}