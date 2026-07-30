namespace InternalsViewer.Internals.DataAccess.AccessPaths.Text;

/// <summary>
/// The role a piece of predicate text plays, used to drive formatting
/// </summary>
/// <remarks>
/// The role is what the writer knows about a token from the model, not how it should look. Choosing a colour or weight is left to the
/// presentation layer so the same token sequence can be rendered differently in different contexts.
/// </remarks>
public enum PredicateTokenType
{
    /// <summary>
    /// Whitespace separating other tokens
    /// </summary>
    Space,

    /// <summary>
    /// A language keyword such as AND, OR or BETWEEN
    /// </summary>
    Keyword,

    /// <summary>
    /// A reference to a column of the row being examined
    /// </summary>
    Column,

    /// <summary>
    /// A comparison or arithmetic operator
    /// </summary>
    Operator,

    /// <summary>
    /// A numeric literal
    /// </summary>
    Number,

    /// <summary>
    /// A string, binary or other quoted literal
    /// </summary>
    Literal,

    /// <summary>
    /// A NULL literal
    /// </summary>
    Null,

    /// <summary>
    /// A bracket or comma
    /// </summary>
    Punctuation,

    /// <summary>
    /// Text describing something that could not be translated from the plan
    /// </summary>
    Unknown
}
