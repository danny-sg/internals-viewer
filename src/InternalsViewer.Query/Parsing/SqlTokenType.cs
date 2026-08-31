namespace InternalsViewer.Query.Parsing;

/// <summary>
/// The role a piece of SQL text plays, used to drive formatting
/// </summary>
/// <remarks>
/// The categories are the ones the SQL editor colours, so text rendered outside the editor can be given the same
/// palette. They are deliberately coarser than the parser's token types, which distinguish every keyword and every
/// operator from each other.
/// </remarks>
public enum SqlTokenType
{
    /// <summary>
    /// Whitespace separating other tokens
    /// </summary>
    Whitespace,

    /// <summary>
    /// A line or block comment
    /// </summary>
    Comment,

    /// <summary>
    /// A language keyword such as SELECT, FROM or AND
    /// </summary>
    Keyword,

    /// <summary>
    /// A name, quoted or not, including variables
    /// </summary>
    Identifier,

    /// <summary>
    /// A numeric literal, decimal, money or hexadecimal
    /// </summary>
    Number,

    /// <summary>
    /// A string literal
    /// </summary>
    Literal,

    /// <summary>
    /// A comparison, arithmetic or bitwise operator
    /// </summary>
    Operator,

    /// <summary>
    /// A bracket, comma, dot or semicolon
    /// </summary>
    Punctuation,

    /// <summary>
    /// Text the lexer could not make a token from
    /// </summary>
    Unknown
}
