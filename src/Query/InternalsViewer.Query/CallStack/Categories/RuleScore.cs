namespace InternalsViewer.Query.CallStack.Categories;

/// <summary>
/// A matched rule's specificity — most specific wins, with function weighted highest and used as the tiebreak
/// </summary>
public readonly record struct RuleScore(int Function, int Class, int Module, int DefinitionOrder)
{
    private int Total => Function + Class + Module;

    /// <summary>
    /// Whether this match is more detailed than <paramref name="other"/> (total specificity, then function, class,
    /// module, then a later definition wins)
    /// </summary>
    public bool IsBetterThan(RuleScore other)
    {
        if (Total != other.Total)
        {
            return Total > other.Total;
        }

        if (Function != other.Function)
        {
            return Function > other.Function;
        }

        if (Class != other.Class)
        {
            return Class > other.Class;
        }

        if (Module != other.Module)
        {
            return Module > other.Module;
        }

        return DefinitionOrder > other.DefinitionOrder;
    }
}