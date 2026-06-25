namespace InternalsViewer.Internals.Tests.Helpers;

/// <summary>
/// xUnit Theory attribute that skips the test when the named connection string is not configured in user secrets
/// </summary>
public sealed class RequiresConnectionStringTheoryAttribute(string name) : TheoryAttribute
{
    public override string? Skip
    {
        get
        {
            var connectionString = ConnectionStringHelper.GetConnectionString(name);

            return string.IsNullOrWhiteSpace(connectionString)
                ? $"Requires connection string '{name}' in user secrets"
                : null;
        }
    }
}
