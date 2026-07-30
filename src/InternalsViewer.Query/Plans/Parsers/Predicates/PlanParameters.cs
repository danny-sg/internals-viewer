using System.Xml.Linq;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Query.Plans.Parsers.Predicates;

/// <summary>
/// Reads the compiled values of the parameters a plan was built for
/// </summary>
/// <remarks>
/// Simple queries are auto parameterized, so a literal written in the query text is replaced by a parameter such as @1 and its value is
/// recorded once in the plan's ParameterList. Resolving that value is what lets a seek on an auto parameterized query show the range it
/// actually sought.
/// </remarks>
public sealed class PlanParameters
{
    private Dictionary<string, AccessValue> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static PlanParameters Empty { get; } = new();

    /// <summary>
    /// Reads every parameter reachable from a plan element
    /// </summary>
    public static PlanParameters Parse(XElement? scope)
    {
        var parameters = new PlanParameters();

        if (scope is null)
        {
            return parameters;
        }

        var references = scope.Descendants()
                              .Where(e => e.Name.LocalName == ShowplanNames.ParameterList)
                              .SelectMany(e => e.Descendants())
                              .Where(e => e.Name.LocalName == ShowplanNames.ColumnReference);

        foreach (var reference in references)
        {
            var name = reference.Attribute(ShowplanNames.Column)?.Value;

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var literal = reference.Attribute(ShowplanNames.ParameterRuntimeValue)?.Value
                          ?? reference.Attribute(ShowplanNames.ParameterCompiledValue)?.Value;

            if (literal is null)
            {
                continue;
            }

            parameters.Values[name] = ConstValueParser.Parse(literal);
        }

        return parameters;
    }

    public AccessValue? Resolve(string parameterName)
    {
        return Values.TryGetValue(parameterName, out var value) ? value : null;
    }

    public int Count => Values.Count;
}
