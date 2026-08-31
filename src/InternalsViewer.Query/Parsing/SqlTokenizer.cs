using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace InternalsViewer.Query.Parsing;

public sealed class SqlTokenizer
{
    private TSqlParser Parser { get; } = new TSql150Parser(false);

    public IReadOnlyList<SqlToken> Tokenize(string sqlText)
    {
        if (string.IsNullOrEmpty(sqlText))
        {
            return [];
        }

        IList<TSqlParserToken> parsed;

        using (var reader = new StringReader(sqlText))
        {
            parsed = Parser.GetTokenStream(reader, out _);
        }

        var tokens = new List<SqlToken>(parsed.Count);

        var consumed = 0;

        foreach (var token in parsed)
        {
            if (token.TokenType == TSqlTokenType.EndOfFile || string.IsNullOrEmpty(token.Text))
            {
                continue;
            }

            tokens.Add(new SqlToken(GetTokenType(token), token.Text));

            consumed = Math.Max(consumed, token.Offset + token.Text.Length);
        }

        if (consumed < sqlText.Length)
        {
            tokens.Add(new SqlToken(SqlTokenType.Unknown, sqlText[consumed..]));
        }

        return tokens;
    }

    private static SqlTokenType GetTokenType(TSqlParserToken token)
    {
        if (token.IsKeyword())
        {
            return SqlTokenType.Keyword;
        }

        return token.TokenType switch
        {
            TSqlTokenType.WhiteSpace
                => SqlTokenType.Whitespace,

            TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment
                => SqlTokenType.Comment,

            // GO is a batch separator the lexer does not count as a keyword, but the editor colours it as one
            TSqlTokenType.Go
                => SqlTokenType.Keyword,

            TSqlTokenType.Integer or TSqlTokenType.Numeric or TSqlTokenType.Real or TSqlTokenType.Money
                or TSqlTokenType.HexLiteral
                => SqlTokenType.Number,

            TSqlTokenType.AsciiStringLiteral or TSqlTokenType.UnicodeStringLiteral
                or TSqlTokenType.AsciiStringOrQuotedIdentifier
                => SqlTokenType.Literal,

            TSqlTokenType.Identifier or TSqlTokenType.QuotedIdentifier or TSqlTokenType.Variable
                or TSqlTokenType.PseudoColumn or TSqlTokenType.DollarPartition or TSqlTokenType.SqlCommandIdentifier
                or TSqlTokenType.Label or TSqlTokenType.OdbcInitiator
                => SqlTokenType.Identifier,

            TSqlTokenType.Comma or TSqlTokenType.Semicolon or TSqlTokenType.ProcNameSemicolon or TSqlTokenType.Dot
                or TSqlTokenType.Colon or TSqlTokenType.DoubleColon or TSqlTokenType.LeftParenthesis
                or TSqlTokenType.RightParenthesis or TSqlTokenType.LeftCurly or TSqlTokenType.RightCurly
                => SqlTokenType.Punctuation,

            TSqlTokenType.Bang or TSqlTokenType.PercentSign or TSqlTokenType.Ampersand or TSqlTokenType.Star
                or TSqlTokenType.Plus or TSqlTokenType.Minus or TSqlTokenType.Divide or TSqlTokenType.LessThan
                or TSqlTokenType.EqualsSign or TSqlTokenType.GreaterThan or TSqlTokenType.Circumflex
                or TSqlTokenType.VerticalLine or TSqlTokenType.Tilde or TSqlTokenType.Concat
                or TSqlTokenType.LeftShift or TSqlTokenType.RightShift or TSqlTokenType.RightOuterJoin
                or TSqlTokenType.MultiplyEquals or TSqlTokenType.AddEquals or TSqlTokenType.SubtractEquals
                or TSqlTokenType.DivideEquals or TSqlTokenType.ModEquals or TSqlTokenType.BitwiseAndEquals
                or TSqlTokenType.BitwiseOrEquals or TSqlTokenType.BitwiseXorEquals or TSqlTokenType.ConcatEquals
                => SqlTokenType.Operator,

            _ => SqlTokenType.Unknown
        };
    }
}
