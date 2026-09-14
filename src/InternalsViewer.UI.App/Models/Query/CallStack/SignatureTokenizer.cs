using System;
using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// Splits an undecorated C++ member signature into name, keyword, type and punctuation tokens
/// </summary>
public static class SignatureTokenizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "const", "volatile", "unsigned", "signed", "void", "int", "long", "short", "char", "bool", "float", "double",
        "__int64", "wchar_t", "char16_t", "char32_t", "static", "virtual", "enum", "class", "struct", "union",
        "__ptr64", "__cdecl", "__thiscall", "__stdcall", "__fastcall", "__clrcall", "__vectorcall"
    };

    public static IReadOnlyList<SignatureToken> Tokenize(string signature)
    {
        var tokens = new List<SignatureToken>();

        var index = 0;

        while (index < signature.Length)
        {
            var start = index;

            if (IsIdentifierStart(signature, index))
            {
                index = ReadIdentifier(signature, index);

                var text = signature[start..index];

                if (text == "operator")
                {
                    index = ReadOperatorName(signature, index);

                    text = signature[start..index];
                }

                tokens.Add(new SignatureToken(Keywords.Contains(text) ? SignatureTokenType.Keyword : SignatureTokenType.Type,
                                              text));

                continue;
            }

            index++;

            while (index < signature.Length && !IsIdentifierStart(signature, index))
            {
                index++;
            }

            tokens.Add(new SignatureToken(SignatureTokenType.Punctuation, signature[start..index]));
        }

        MarkName(tokens);

        return tokens;
    }

    private static bool IsIdentifierStart(string signature, int index)
    {
        var c = signature[index];

        if (c == '~')
        {
            return index + 1 < signature.Length && IsIdentifierStart(signature, index + 1);
        }

        return char.IsLetter(c) || c == '_';
    }

    private static int ReadIdentifier(string signature, int index)
    {
        if (signature[index] == '~')
        {
            index++;
        }

        index = ReadIdentifierPart(signature, index);

        while (index + 2 < signature.Length
               && signature[index] == ':'
               && signature[index + 1] == ':'
               && IsIdentifierStart(signature, index + 2))
        {
            index = ReadIdentifierPart(signature, signature[index + 2] == '~' ? index + 3 : index + 2);
        }

        return index;
    }

    private static int ReadIdentifierPart(string signature, int index)
    {
        while (index < signature.Length && (char.IsLetterOrDigit(signature[index]) || signature[index] == '_'))
        {
            index++;
        }

        return index;
    }

    private static int ReadOperatorName(string signature, int index)
    {
        while (index < signature.Length && signature[index] != '(')
        {
            index++;
        }

        return index;
    }

    private static void MarkName(List<SignatureToken> tokens)
    {
        var parameterList = tokens.FindIndex(t => t.Type == SignatureTokenType.Punctuation && t.Text.Contains('('));

        var searchFrom = parameterList < 0 ? tokens.Count - 1 : parameterList - 1;

        for (var index = searchFrom; index >= 0; index--)
        {
            if (tokens[index].Type == SignatureTokenType.Type)
            {
                tokens[index] = tokens[index] with { Type = SignatureTokenType.Name };

                return;
            }
        }
    }
}
